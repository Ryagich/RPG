using System.Collections.Generic;
using GameModes;
using Localization;
using MessagePipe;
using Messages;
using Quests;
using Quests.Graph.Model;
using TMPro;
using UI.Configs;
using UI.Map;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace UI.Pages
{
    public sealed class QuestPage : BasePage, ITickable
    {
        private sealed class QuestBinding
        {
            public QuestBinding(RectTransform rect, QuestProgress questProgress)
            {
                Rect = rect;
                QuestProgress = questProgress;
            }

            public RectTransform Rect { get; }
            public QuestProgress QuestProgress { get; }
        }

        private sealed class TaskBinding
        {
            public TaskBinding(RectTransform rect, QuestProgress questProgress, QuestNodeData node, bool isCompleted)
            {
                Rect = rect;
                QuestProgress = questProgress;
                Node = node;
                IsCompleted = isCompleted;
            }

            public RectTransform Rect { get; }
            public QuestProgress QuestProgress { get; }
            public QuestNodeData Node { get; }
            public bool IsCompleted { get; }
        }

        public override PageType Type { get; } = PageType.Quest;

        private readonly UIConfig uiConfig;
        private readonly LocalizationConfig localizationConfig;
        private readonly QuestController questController;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;
        private readonly List<QuestBinding> questBindings = new();
        private readonly List<TaskBinding> taskBindings = new();

        private RectTransform pageRect;
        private QuestPageHolder holder;
        private Title title;
        private ScrollRect questionsScroll;
        private ScrollRect tasksScroll;
        private ScrollRect descriptionScroll;
        private RectTransform questionsContent;
        private RectTransform tasksContent;
        private RectTransform descriptionContent;
        private TMP_Text descriptionText;
        private Image showCompletedTasksBack;
        private TMP_Text showCompletedTasksText;
        private QuestProgress selectedQuest;
        private QuestProgress displayedQuest;
        private TaskBinding pinnedTask;
        private QuestBinding hoveredQuest;
        private TaskBinding hoveredTask;
        private bool showCompletedTasks;

        public QuestPage(
            UIConfig uiConfig,
            LocalizationConfig localizationConfig,
            QuestController questController,
            Canvas canvas,
            IObjectResolver resolver,
            IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher)
        {
            this.uiConfig = uiConfig;
            this.localizationConfig = localizationConfig;
            this.questController = questController;
            this.resolver = resolver;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;
            canvasRect = canvas.GetComponent<RectTransform>();
        }

        public override void Draw()
        {
            if (uiConfig.QuestPage == null || uiConfig.Quest == null || uiConfig.Task == null)
            {
                Debug.LogError("Quest Page, Quest, or Task prefab is not assigned in UIConfig.");
                changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
                return;
            }

            pageRect = resolver.Instantiate(uiConfig.QuestPage, canvasRect);
            pageRect.name = $"{uiConfig.QuestPage.name} | {Type}";
            ConfigureUnscaledAnimators(pageRect.gameObject);

            holder = pageRect.GetComponent<QuestPageHolder>();
            if (holder == null || holder.Title == null || holder.QuestionsScroll == null ||
                holder.TasksScroll == null || holder.DescriptionScroll == null || holder.DescriptionText == null ||
                holder.Title.ShowCompletedTasksBackground == null ||
                holder.Title.ShowCompletedTasksText == null)
            {
                Debug.LogError("Quest Page prefab is missing QuestPageHolder references.");
                return;
            }

            title = holder.Title;
            questionsScroll = holder.QuestionsScroll;
            tasksScroll = holder.TasksScroll;
            descriptionScroll = holder.DescriptionScroll;
            questionsContent = questionsScroll.content;
            tasksContent = tasksScroll.content;
            descriptionContent = descriptionScroll.content;
            descriptionText = holder.DescriptionText;
            showCompletedTasksBack = title.ShowCompletedTasksBackground;
            showCompletedTasksText = title.ShowCompletedTasksText;

            if (title.TitleName != null)
            {
                title.TitleName.text = localizationConfig.QuestsTitle.GetLocalizedStringCached();
            }

            title.ExitButton?.onClick.AddListener(Close);
            title.LeftButton?.onClick.AddListener(OpenMap);
            title.RightButton?.onClick.AddListener(OpenMap);

            if (questionsContent == null || tasksContent == null || descriptionContent == null)
            {
                Debug.LogError("Quest Page holder has scroll views without Content references.");
                return;
            }

            showCompletedTasks = false;
            descriptionText.raycastTarget = false;
            descriptionText.alignment = TextAlignmentOptions.TopLeft;
            descriptionText.enableWordWrapping = true;
            UpdateShowCompletedTasksVisual();

            selectedQuest = questController.CurrentQuest;
            questController.Changed += OnQuestChanged;
            RefreshQuestList();
            ShowDefaultQuestDetails();
        }

        public override void Hide()
        {
            if (questController != null)
            {
                questController.Changed -= OnQuestChanged;
            }

            if (title != null)
            {
                title.ExitButton?.onClick.RemoveListener(Close);
                title.LeftButton?.onClick.RemoveListener(OpenMap);
                title.RightButton?.onClick.RemoveListener(OpenMap);
                title = null;
            }

            ClearBindings(questBindings);
            ClearTaskBindings();

            if (pageRect != null)
            {
                Object.Destroy(pageRect.gameObject);
                pageRect = null;
            }

            questionsScroll = null;
            tasksScroll = null;
            descriptionScroll = null;
            questionsContent = null;
            tasksContent = null;
            descriptionContent = null;
            descriptionText = null;
            showCompletedTasksBack = null;
            showCompletedTasksText = null;
            holder = null;
            selectedQuest = null;
            displayedQuest = null;
            pinnedTask = null;
            hoveredQuest = null;
            hoveredTask = null;
        }

        public void Tick()
        {
            if (pageRect == null || Pointer.current == null)
            {
                return;
            }

            Vector2 screenPoint = Pointer.current.position.ReadValue();
            if (Pointer.current.press.wasPressedThisFrame &&
                IsPointerInside(title?.ShowCompletedTasksBackground?.rectTransform, screenPoint))
            {
                ToggleShowCompletedTasks();
                return;
            }

            TaskBinding taskUnderPointer = FindTaskAt(screenPoint);
            QuestBinding questUnderPointer = taskUnderPointer == null ? FindQuestAt(screenPoint) : null;

            if (!ReferenceEquals(taskUnderPointer, hoveredTask) || !ReferenceEquals(questUnderPointer, hoveredQuest))
            {
                hoveredTask = taskUnderPointer;
                hoveredQuest = questUnderPointer;

                if (hoveredTask != null)
                {
                    SetDescription(hoveredTask.Node.Description.GetLocalizedStringCached());
                }
                else if (hoveredQuest != null)
                {
                    pinnedTask = null;
                    ShowQuestDetails(hoveredQuest.QuestProgress);
                }
                else
                {
                    ShowDefaultQuestDetails();
                }
            }

            if (!Pointer.current.press.wasPressedThisFrame)
            {
                return;
            }

            if (taskUnderPointer != null)
            {
                pinnedTask = ReferenceEquals(pinnedTask, taskUnderPointer) ? null : taskUnderPointer;
                ShowDefaultQuestDetails();
                return;
            }

            if (questUnderPointer == null)
            {
                return;
            }

            if (ReferenceEquals(selectedQuest, questUnderPointer.QuestProgress))
            {
                questController.TrySetCurrentQuest(selectedQuest.QuestGraph);
            }
            else
            {
                selectedQuest = questUnderPointer.QuestProgress;
            }

            pinnedTask = null;
            ShowQuestDetails(selectedQuest);
        }

        private void RefreshQuestList()
        {
            ClearBindings(questBindings);
            MapIconDefinition questIconDefinition = null;
            bool hasQuestIcon = uiConfig.MapIconsConfig != null &&
                                uiConfig.MapIconsConfig.TryGetIcon(MapIconsConfig.QuestIconName, out questIconDefinition);
            if (!hasQuestIcon)
            {
                Debug.LogError($"Map Icons Config does not contain '{MapIconsConfig.QuestIconName}' icon definition for Quest Page.");
            }

            foreach (QuestProgress questProgress in GetVisibleQuests())
            {
                RectTransform questRect = resolver.Instantiate(uiConfig.Quest, questionsContent);
                questRect.name = $"{uiConfig.Quest.name} | {questProgress.QuestGraph.name}";

                QuestListItem questItem = questRect.GetComponent<QuestListItem>();
                if (questItem == null)
                {
                    Debug.LogError("Quest prefab is missing QuestListItem references.");
                    Object.Destroy(questRect.gameObject);
                    continue;
                }

                TMP_Text questText = questItem.Text;
                if (questText != null)
                {
                    questText.text = questProgress.QuestGraph.Title.GetLocalizedStringCached();
                    ResizeListItemToText(questRect, questText);
                }

                Image questIcon = questItem.Icon;
                if (questIcon != null)
                {
                    questIcon.sprite = hasQuestIcon ? questIconDefinition.Sprite : null;
                    questIcon.color = ReferenceEquals(questProgress, questController.CurrentQuest) ? Color.yellow : Color.white;
                    questIcon.preserveAspect = true;
                }

                questBindings.Add(new QuestBinding(questRect, questProgress));
            }

            if (!IsVisibleQuest(selectedQuest))
            {
                selectedQuest = questController.CurrentQuest;
            }

            ResizeScrollContent(questionsScroll, questionsContent);
        }

        private IEnumerable<QuestProgress> GetVisibleQuests()
        {
            QuestProgress currentQuest = questController.CurrentQuest;
            if (currentQuest != null)
            {
                yield return currentQuest;
            }

            foreach (QuestProgress questProgress in questController.Progress)
            {
                if (questProgress == null || ReferenceEquals(questProgress, currentQuest) || questProgress.IsCompleted)
                {
                    continue;
                }

                yield return questProgress;
            }

            if (!showCompletedTasks)
            {
                yield break;
            }

            foreach (QuestProgress questProgress in questController.Progress)
            {
                if (questProgress is { IsCompleted: true })
                {
                    yield return questProgress;
                }
            }
        }

        private void ShowQuestDetails(QuestProgress questProgress)
        {
            if (questProgress == null)
            {
                ClearTaskBindings();
                SetDescription(string.Empty);
                displayedQuest = null;
                return;
            }

            if (!ReferenceEquals(displayedQuest, questProgress))
            {
                displayedQuest = questProgress;
                RebuildTasks(questProgress);
            }

            SetDescription(questProgress.QuestGraph.Description.GetLocalizedStringCached());
        }

        private void ShowDefaultQuestDetails()
        {
            if (pinnedTask != null)
            {
                if (!ReferenceEquals(displayedQuest, pinnedTask.QuestProgress))
                {
                    displayedQuest = pinnedTask.QuestProgress;
                    RebuildTasks(displayedQuest);
                }

                SetDescription(pinnedTask.Node.Description.GetLocalizedStringCached());
                return;
            }

            ShowQuestDetails(selectedQuest ?? questController.CurrentQuest);
        }

        private void RebuildTasks(QuestProgress questProgress)
        {
            ClearTaskBindings();

            foreach (QuestNodeData completedNode in questProgress.CompletedNodes)
            {
                CreateTask(questProgress, completedNode, true);
            }

            if (!questProgress.IsCompleted && questProgress.CurrentNode != null)
            {
                CreateTask(questProgress, questProgress.CurrentNode, false);
            }

            ResizeScrollContent(tasksScroll, tasksContent);
        }

        private void CreateTask(QuestProgress questProgress, QuestNodeData node, bool isCompleted)
        {
            if (node == null)
            {
                return;
            }

            RectTransform taskRect = resolver.Instantiate(uiConfig.Task, tasksContent);
            taskRect.name = $"{uiConfig.Task.name} | {node.name}";

            QuestTaskListItem taskItem = taskRect.GetComponent<QuestTaskListItem>();
            if (taskItem == null)
            {
                Debug.LogError("Task prefab is missing QuestTaskListItem references.");
                Object.Destroy(taskRect.gameObject);
                return;
            }

            TMP_Text taskText = taskItem.Text;
            if (taskText != null)
            {
                taskText.text = node.Name.GetLocalizedStringCached();
                ResizeListItemToText(taskRect, taskText);
            }

            Image taskBack = taskItem.Background;
            if (taskBack != null)
            {
                if (isCompleted)
                {
                    taskBack.color = uiConfig.MapIconsConfig != null
                        ? uiConfig.MapIconsConfig.CompletedTaskColor
                        : Color.green;
                }
                else
                {
                    Color transparent = taskBack.color;
                    transparent.a = 0f;
                    taskBack.color = transparent;
                }
            }

            taskBindings.Add(new TaskBinding(taskRect, questProgress, node, isCompleted));
        }

        private void SetDescription(string description)
        {
            if (descriptionText == null || descriptionContent == null)
            {
                return;
            }

            description ??= string.Empty;
            descriptionText.text = description;
            Canvas.ForceUpdateCanvases();

            float width = descriptionContent.rect.width;
            float height = width > 0f
                ? Mathf.Ceil(descriptionText.GetPreferredValues(descriptionText.text, width, 0f).y)
                : 0f;

            descriptionContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            RectTransform textRect = descriptionText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            LayoutRebuilder.ForceRebuildLayoutImmediate(descriptionContent);
            Canvas.ForceUpdateCanvases();
            if (descriptionScroll != null)
            {
                descriptionScroll.verticalNormalizedPosition = 1f;
            }
        }

        private void ToggleShowCompletedTasks()
        {
            showCompletedTasks = !showCompletedTasks;
            pinnedTask = null;
            displayedQuest = null;
            UpdateShowCompletedTasksVisual();
            RefreshQuestList();
            ShowDefaultQuestDetails();
        }

        private void UpdateShowCompletedTasksVisual()
        {
            if (showCompletedTasksBack != null)
            {
                showCompletedTasksBack.color = showCompletedTasks ? Color.green : Color.red;
            }

            if (showCompletedTasksText != null)
            {
                showCompletedTasksText.text = showCompletedTasks
                    ? localizationConfig.AllQuests.GetLocalizedStringCached()
                    : localizationConfig.ActiveQuestsOnly.GetLocalizedStringCached();
            }
        }

        private void OnQuestChanged(QuestChangeInfo _)
        {
            if (pageRect == null)
            {
                return;
            }

            pinnedTask = null;
            displayedQuest = null;
            RefreshQuestList();
            ShowDefaultQuestDetails();
        }

        private QuestBinding FindQuestAt(Vector2 screenPoint)
        {
            for (int i = questBindings.Count - 1; i >= 0; i--)
            {
                if (IsPointerInside(questBindings[i].Rect, screenPoint))
                {
                    return questBindings[i];
                }
            }

            return null;
        }

        private TaskBinding FindTaskAt(Vector2 screenPoint)
        {
            for (int i = taskBindings.Count - 1; i >= 0; i--)
            {
                if (IsPointerInside(taskBindings[i].Rect, screenPoint))
                {
                    return taskBindings[i];
                }
            }

            return null;
        }

        private bool IsVisibleQuest(QuestProgress questProgress)
        {
            if (questProgress == null)
            {
                return false;
            }

            foreach (QuestBinding binding in questBindings)
            {
                if (ReferenceEquals(binding.QuestProgress, questProgress))
                {
                    return true;
                }
            }

            return false;
        }

        private void ResizeListItemToText(RectTransform itemRect, TMP_Text text)
        {
            if (itemRect == null || text == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            float width = text.rectTransform.rect.width;
            if (width <= 0f)
            {
                return;
            }

            float targetHeight = Mathf.Max(itemRect.rect.height, Mathf.Ceil(text.GetPreferredValues(text.text, width, 0f).y));
            itemRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
            text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);

            LayoutElement layoutElement = itemRect.GetComponent<LayoutElement>() ?? itemRect.gameObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = targetHeight;
            layoutElement.preferredHeight = targetHeight;
        }

        private void ResizeScrollContent(ScrollRect scrollRect, RectTransform content)
        {
            if (scrollRect == null || content == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            float height = 0f;
            int childCount = 0;
            VerticalLayoutGroup layoutGroup = content.GetComponent<VerticalLayoutGroup>();
            for (int i = 0; i < content.childCount; i++)
            {
                if (content.GetChild(i) is not RectTransform child || !child.gameObject.activeSelf)
                {
                    continue;
                }

                height += Mathf.Max(child.rect.height, LayoutUtility.GetPreferredHeight(child));
                childCount++;
            }

            if (layoutGroup != null)
            {
                height += layoutGroup.padding.top + layoutGroup.padding.bottom + layoutGroup.spacing * Mathf.Max(0, childCount - 1);
            }

            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private bool IsPointerInside(RectTransform rect, Vector2 screenPoint)
        {
            return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, GetEventCamera());
        }

        private Camera GetEventCamera()
        {
            Canvas canvas = pageRect != null ? pageRect.GetComponentInParent<Canvas>() : null;
            return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        private static void ClearBindings(List<QuestBinding> bindings)
        {
            foreach (QuestBinding binding in bindings)
            {
                if (binding.Rect != null)
                {
                    binding.Rect.SetParent(null);
                    Object.Destroy(binding.Rect.gameObject);
                }
            }

            bindings.Clear();
        }

        private void ClearTaskBindings()
        {
            foreach (TaskBinding binding in taskBindings)
            {
                if (binding.Rect != null)
                {
                    binding.Rect.SetParent(null);
                    Object.Destroy(binding.Rect.gameObject);
                }
            }

            taskBindings.Clear();
        }

        private void Close()
        {
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
        }

        private void OpenMap()
        {
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Map));
        }

        private static void ConfigureUnscaledAnimators(GameObject root)
        {
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            {
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
        }
    }
}
