using System.Collections.Generic;
using Localization;
using Quests;
using UI.Configs;
using UI.UIElements;
using UnityEngine;
using VContainer.Unity;

namespace UI
{
    public sealed class QuestNotificationService : ITickable, System.IDisposable
    {
        private enum Phase { Hidden, FadingIn, Holding, FadingOut }

        private readonly QuestNotificationConfig config;
        private readonly LocalizationConfig localization;
        private readonly QuestController quests;
        private readonly QuestObjectiveOverrideContext questObjectiveOverride;
        private readonly Queue<QuestChangeInfo> queue = new();
        private readonly Dictionary<Quests.Graph.QuestGraph, QuestState> knownStates = new();
        private QuestChangeInfo? current;
        private Phase phase = Phase.Hidden;
        private float elapsed;
        private QuestNotificationView view;

        private readonly struct QuestState
        {
            public Quests.Graph.Model.QuestNodeData Node { get; }
            public bool Completed { get; }
            public QuestState(Quests.Graph.Model.QuestNodeData node, bool completed) { Node = node; Completed = completed; }
        }

        public QuestNotificationService(
            QuestController quests,
            UIConfig uiConfig,
            LocalizationConfig localization,
            QuestObjectiveOverrideContext questObjectiveOverride)
        {
            this.quests = quests;
            config = uiConfig != null ? uiConfig.QuestNotificationConfig : null;
            this.localization = localization;
            this.questObjectiveOverride = questObjectiveOverride;
            quests.Changed += Enqueue;
            CaptureCurrentStates();
        }

        public void Attach(QuestNotificationView notificationView)
        {
            view = notificationView;
            if (current == null)
            {
                view.HideImmediately();
                return;
            }

            RefreshView();
        }

        public void Detach(QuestNotificationView notificationView)
        {
            if (view == notificationView) view = null;
        }

        public void Tick()
        {
            DetectQuestStateChanges();

            // The queue must continue receiving quest changes on every page, but its timers
            // and visual state are intentionally paused until the main gameplay HUD is attached.
            if (view == null || config == null) return;

            if (current == null && queue.Count > 0)
            {
                current = queue.Dequeue();
                phase = Phase.FadingIn;
                elapsed = 0f;
                view.ResetForShow();
                RefreshView();
            }
            if (current == null) return;

            var deltaTime = Time.deltaTime;
            elapsed += deltaTime;
            var duration = phase switch
            {
                Phase.FadingIn => config.FadeInTime,
                Phase.Holding => config.HoldTime,
                Phase.FadingOut => config.FadeOutTime,
                _ => 0f
            };
            if (duration <= 0f || elapsed >= duration)
            {
                AdvancePhase();
            }
            RefreshView();
        }

        private void Enqueue(QuestChangeInfo change)
        {
            queue.Enqueue(change);
            if (change.Quest != null)
            {
                var progress = FindProgress(change.Quest);
                if (progress == null) knownStates.Remove(change.Quest);
                else knownStates[change.Quest] = new QuestState(progress.CurrentNode, progress.IsCompleted);
            }
        }

        private void CaptureCurrentStates()
        {
            foreach (var progress in quests.Progress)
            {
                if (progress?.QuestGraph != null)
                    knownStates[progress.QuestGraph] = new QuestState(progress.CurrentNode, progress.IsCompleted);
            }
        }

        private void DetectQuestStateChanges()
        {
            var active = new HashSet<Quests.Graph.QuestGraph>();
            foreach (var progress in quests.Progress)
            {
                if (progress?.QuestGraph == null) continue;
                active.Add(progress.QuestGraph);
                var state = new QuestState(progress.CurrentNode, progress.IsCompleted);
                if (!knownStates.TryGetValue(progress.QuestGraph, out var old))
                    queue.Enqueue(new QuestChangeInfo(QuestChangeType.Added, progress.QuestGraph));
                else if (!old.Completed && state.Completed)
                    queue.Enqueue(new QuestChangeInfo(QuestChangeType.Completed, progress.QuestGraph));
                else if (old.Node != state.Node)
                    queue.Enqueue(new QuestChangeInfo(QuestChangeType.Updated, progress.QuestGraph));
                knownStates[progress.QuestGraph] = state;
            }

            foreach (var quest in new List<Quests.Graph.QuestGraph>(knownStates.Keys))
            {
                if (active.Contains(quest)) continue;
                knownStates.Remove(quest);
                queue.Enqueue(new QuestChangeInfo(QuestChangeType.Removed, quest));
            }
        }

        private QuestProgress FindProgress(Quests.Graph.QuestGraph quest)
        {
            foreach (var progress in quests.Progress)
                if (progress?.QuestGraph == quest) return progress;
            return null;
        }

        private void AdvancePhase()
        {
            elapsed = 0f;
            switch (phase)
            {
                case Phase.FadingIn: phase = Phase.Holding; break;
                case Phase.Holding: phase = Phase.FadingOut; break;
                case Phase.FadingOut:
                    current = null;
                    phase = Phase.Hidden;
                    view?.HideImmediately();
                    break;
            }

        }

        private void RefreshView()
        {
            if (view == null) return;
            if (current == null) { view.SetAlpha(0f); return; }
            view.SetContent(GetStateText(current.Value.Type), GetQuestName(current.Value.Quest));
            float duration = phase == Phase.FadingIn ? config.FadeInTime : config.FadeOutTime;
            float alpha = phase switch
            {
                Phase.FadingIn => duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration),
                Phase.Holding => 1f,
                Phase.FadingOut => duration <= 0f ? 0f : 1f - Mathf.Clamp01(elapsed / duration),
                _ => 0f
            };
            view.SetAlpha(alpha);
        }

        private string GetStateText(QuestChangeType type) => type switch
        {
            QuestChangeType.Added => localization.QuestNew.GetLocalizedStringCached(),
            QuestChangeType.Updated => localization.QuestUpdate.GetLocalizedStringCached(),
            QuestChangeType.Completed => localization.QuestCompleted.GetLocalizedStringCached(),
            QuestChangeType.Failed => localization.QuestFailed.GetLocalizedStringCached(),
            QuestChangeType.Removed => localization.QuestCanceled.GetLocalizedStringCached(),
            _ => string.Empty
        };

        private string GetQuestName(Quests.Graph.QuestGraph quest)
        {
            if (questObjectiveOverride != null
                && questObjectiveOverride.AppliesTo(quest)
                && !string.IsNullOrWhiteSpace(questObjectiveOverride.Title))
            {
                return questObjectiveOverride.Title;
            }

            return quest == null ? string.Empty : quest.Title.GetLocalizedStringCached();
        }

        public void Dispose()
        {
            quests.Changed -= Enqueue;
        }
    }
}
