using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dialogs.Graph.Model;
using Dialogue;
using EditorTools;
using Quests.Editor;
using Quests.Graph;
using Quests.Graph.Model;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Localization;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Dialogs.Graph.Editor
{
    /// <summary>Owns IMGUI editing of dialog phrases, answers, conditions, and quest references.</summary>
    public partial class DialogEditorWindow
    {
        private void DrawNodeWindow(DialogNode node, bool allowWindowDrag = true)
        {
            if (TryHandleTargetPhraseSelection(node))
            {
                return;
            }

            if (!targetSelection.IsActive &&
                Event.current.rawType == EventType.MouseDown &&
                Event.current.button == 0 &&
                activeConnectionNode != node)
            {
                activeConnectionNode = node;
                toolkitCanvas?.RefreshConnectionHighlights();
            }

            EditorGUI.BeginDisabledGroup(targetSelection.IsActive);

            Rect removeButtonRect = new Rect(298f, 5f, 16f, 16f);
            if (allowWindowDrag && DrawMiniButton(removeButtonRect, "x"))
            {
                DeleteNode(node);
                EditorGUI.EndDisabledGroup();
                return;
            }

            EditorGUI.BeginChangeCheck();
            List<EditorStyleTextOverride> objectFieldOverrides = useLightTheme && Event.current.type == EventType.Repaint
                ? CreateTemporaryObjectFieldTextOverrides(Color.white)
                : null;
            DialogPhrase newPhrase;

            try
            {
                newPhrase = (DialogPhrase)EditorGUILayout.ObjectField(node.Phrase, typeof(DialogPhrase), false);
            }
            finally
            {
                RestoreTemporaryStyleTextOverrides(objectFieldOverrides);
            }

            if (EditorGUI.EndChangeCheck())
            {
                if (newPhrase != null && currentGraph.Nodes.Exists(n => n != node && n.Phrase == newPhrase))
                {
                    EditorUtility.DisplayDialog(
                        "Duplicate Phrase Detected",
                        $"Phrase \"{newPhrase.name}\" is already assigned to another node.",
                        "OK");
                }
                else
                {
                    if (currentGraph.IsEntryPhrase(node.Phrase))
                    {
                        currentGraph.SetEntryPhrase(newPhrase);
                    }

                    InvalidatePhraseDisplayName(node.Phrase);
                    InvalidatePhraseDisplayName(newPhrase);
                    ReplacePhraseReferences(node.Phrase, newPhrase);
                    node.Phrase = newPhrase;
                    MarkDirty(currentGraph);
                    InvalidateGraphStructure();
                }
            }

            if (node.Phrase == null)
            {
                EditorGUILayout.HelpBox("No phrase assigned.", MessageType.Warning);
                if (allowWindowDrag)
                {
                    GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
                }

                EditorGUI.EndDisabledGroup();
                return;
            }

            DrawPhraseEditor(node.Phrase);

            if (DrawButton(currentGraph.IsEntryPhrase(node.Phrase) ? "Start Phrase" : "Set As Start"))
            {
                currentGraph.SetEntryPhrase(node.Phrase);
                MarkDirty(currentGraph);
                InvalidateGraphCaches();
            }

            if (node.Phrase.IsQuestPhrase || node.Phrase.IsConversationTopic || node.Phrase.IsConversationReturnAction || node.Phrase.IsDialogueExitAction)
            {
                EditorGUILayout.HelpBox(
                    node.Phrase.IsQuestPhrase
                        ? "Quest phrase appears as an answer at regular conversation choice points and does not require incoming links."
                        : node.Phrase.IsConversationTopic
                            ? "Conversation topic appears as an answer at regular conversation choice points and does not require incoming links."
                            : node.Phrase.IsConversationReturnAction
                                ? "Conversation return action appears automatically inside conversation topic branches when its conditions are satisfied and does not require incoming links."
                                : "Dialogue exit action appears automatically at regular choice points and as the safe exit from terminal phrases when its conditions are satisfied and does not require incoming links.",
                    MessageType.Info);
            }

            if (IsOrphanPhrase(node.Phrase))
            {
                EditorGUILayout.HelpBox(
                    "This phrase has no incoming answers and is not the entry phrase.",
                    MessageType.Warning);
            }

            EditorGUI.EndDisabledGroup();
            if (allowWindowDrag)
            {
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
            }
        }

        private bool TryHandleTargetPhraseSelection(DialogNode node)
        {
            if (!targetSelection.IsActive || targetSelection.PendingAnswer == null)
            {
                return false;
            }

            if (node.Phrase == null)
            {
                return false;
            }

            if (Event.current.type != EventType.MouseDown || Event.current.button != 0)
            {
                return false;
            }

            targetSelection.PendingAnswer.SetNextPhrase(node.Phrase);
            MarkDirty(targetSelection.SourcePhrase);
            InvalidateGraphCaches();
            CancelTargetSelection(false);
            Event.current.Use();
            GUIUtility.ExitGUI();
            return true;
        }

        private void DrawPhraseEditor(DialogPhrase phrase)
        {
            SerializedObject phraseObject = new SerializedObject(phrase);
            phraseObject.Update();

            SerializedProperty textProperty = phraseObject.FindProperty("text");
            SerializedProperty alternativeTextsProperty = phraseObject.FindProperty("alternativeTexts");
            SerializedProperty isForcedDialoguePhraseProperty = phraseObject.FindProperty("isForcedDialoguePhrase");
            SerializedProperty forcedDialoguePriorityProperty = phraseObject.FindProperty("forcedDialoguePriority");
            SerializedProperty restoresExitAbilityProperty = phraseObject.FindProperty("restoresExitAbility");
            SerializedProperty isQuestPhraseProperty = phraseObject.FindProperty("isQuestPhrase");
            SerializedProperty questAnswerProperty = phraseObject.FindProperty("questAnswer");
            SerializedProperty isConversationTopicProperty = phraseObject.FindProperty("isConversationTopic");
            SerializedProperty conversationAnswerProperty = phraseObject.FindProperty("conversationAnswer");
            SerializedProperty isConversationReturnActionProperty = phraseObject.FindProperty("isConversationReturnAction");
            SerializedProperty conversationReturnAnswerProperty = phraseObject.FindProperty("conversationReturnAnswer");
            SerializedProperty isDialogueExitActionProperty = phraseObject.FindProperty("isDialogueExitAction");
            SerializedProperty dialogueExitAnswerProperty = phraseObject.FindProperty("dialogueExitAnswer");
            SerializedProperty hasGameplayEventsProperty = phraseObject.FindProperty("hasGameplayEvents");
            SerializedProperty gameplayEventsProperty = phraseObject.FindProperty("gameplayEvents");
            SerializedProperty answersProperty = phraseObject.FindProperty("answers");

            if (answersProperty == null)
            {
                EditorGUILayout.HelpBox("Answers property was not found.", MessageType.Error);
                return;
            }

            if (textProperty != null)
            {
                DrawLocalizedStringSelector(textProperty, "Phrase");
            }

            DrawAlternativePhraseTexts(alternativeTextsProperty);

            if (isForcedDialoguePhraseProperty != null)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.PropertyField(isForcedDialoguePhraseProperty, new GUIContent("Forced Dialogue Phrase"));

                if (isForcedDialoguePhraseProperty.boolValue)
                {
                    if (forcedDialoguePriorityProperty != null)
                    {
                        EditorGUILayout.PropertyField(
                            forcedDialoguePriorityProperty,
                            new GUIContent(
                                "Priority",
                                "When several forced dialogue phrases are available, the phrase with the lower value starts first. Equal priorities keep graph order."));
                    }

                    if (restoresExitAbilityProperty != null)
                    {
                        restoresExitAbilityProperty.boolValue = false;
                    }
                }
                else if (restoresExitAbilityProperty != null)
                {
                    bool canRestoreExitAbility = CanRestoreExitAbility(phrase);
                    if (!canRestoreExitAbility)
                    {
                        restoresExitAbilityProperty.boolValue = false;
                    }

                    EditorGUI.BeginDisabledGroup(!canRestoreExitAbility);
                    EditorGUILayout.PropertyField(restoresExitAbilityProperty, new GUIContent("Restores Ability To Exit"));
                    EditorGUI.EndDisabledGroup();

                    if (!canRestoreExitAbility)
                    {
                        EditorGUILayout.HelpBox(
                            "This option is available only on a branch after a forced dialogue phrase, before exit has already been restored.",
                            MessageType.Info);
                    }
                }
            }

            if (isQuestPhraseProperty != null)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.PropertyField(isQuestPhraseProperty, new GUIContent("Quest Phrase"));

                if (isQuestPhraseProperty.boolValue)
                {
                    if (isConversationTopicProperty != null)
                    {
                        isConversationTopicProperty.boolValue = false;
                    }

                    if (isConversationReturnActionProperty != null)
                    {
                        isConversationReturnActionProperty.boolValue = false;
                    }

                    if (isDialogueExitActionProperty != null)
                    {
                        isDialogueExitActionProperty.boolValue = false;
                    }

                    DrawQuestPhraseSettings(questAnswerProperty);
                }
            }

            if (isConversationTopicProperty != null)
            {
                EditorGUILayout.Space(4f);
                if (isQuestPhraseProperty?.boolValue == true || isConversationReturnActionProperty?.boolValue == true || isDialogueExitActionProperty?.boolValue == true)
                {
                    isConversationTopicProperty.boolValue = false;
                }

                EditorGUI.BeginDisabledGroup(
                    isQuestPhraseProperty?.boolValue == true || isConversationReturnActionProperty?.boolValue == true || isDialogueExitActionProperty?.boolValue == true);
                EditorGUILayout.PropertyField(
                    isConversationTopicProperty,
                    new GUIContent(
                        "Conversation Topic",
                        "Shows this phrase as a reusable topic answer at every regular conversation choice point."));
                EditorGUI.EndDisabledGroup();

                if (isConversationTopicProperty.boolValue)
                {
                    DrawConversationTopicSettings(conversationAnswerProperty);
                }
            }

            if (isConversationReturnActionProperty != null)
            {
                EditorGUILayout.Space(4f);
                if (isConversationReturnActionProperty.boolValue)
                {
                    if (isQuestPhraseProperty != null)
                    {
                        isQuestPhraseProperty.boolValue = false;
                    }

                    if (isConversationTopicProperty != null)
                    {
                        isConversationTopicProperty.boolValue = false;
                    }
                }

                EditorGUI.BeginDisabledGroup(
                    isQuestPhraseProperty?.boolValue == true || isConversationTopicProperty?.boolValue == true || isDialogueExitActionProperty?.boolValue == true);
                EditorGUILayout.PropertyField(
                    isConversationReturnActionProperty,
                    new GUIContent(
                        "Conversation Return Action",
                        "Adds this answer automatically to phrases within a reusable conversation topic branch when its conditions are satisfied."));
                EditorGUI.EndDisabledGroup();

                if (isConversationReturnActionProperty.boolValue)
                {
                    DrawConversationReturnActionSettings(conversationReturnAnswerProperty);
                }
            }

            if (isDialogueExitActionProperty != null)
            {
                EditorGUILayout.Space(4f);
                if (isDialogueExitActionProperty.boolValue)
                {
                    if (isQuestPhraseProperty != null)
                    {
                        isQuestPhraseProperty.boolValue = false;
                    }

                    if (isConversationTopicProperty != null)
                    {
                        isConversationTopicProperty.boolValue = false;
                    }

                    if (isConversationReturnActionProperty != null)
                    {
                        isConversationReturnActionProperty.boolValue = false;
                    }
                }

                EditorGUI.BeginDisabledGroup(
                    isQuestPhraseProperty?.boolValue == true || isConversationTopicProperty?.boolValue == true || isConversationReturnActionProperty?.boolValue == true);
                EditorGUILayout.PropertyField(
                    isDialogueExitActionProperty,
                    new GUIContent(
                        "Dialogue Exit Action",
                        "Adds this answer automatically at regular choice points and when a phrase would otherwise have no available answer."));
                EditorGUI.EndDisabledGroup();

                if (isDialogueExitActionProperty.boolValue)
                {
                    DrawDialogueExitActionSettings(dialogueExitAnswerProperty);
                }
            }

            DrawGameplayEvents(hasGameplayEventsProperty, gameplayEventsProperty);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Answers", MiniBoldLabelStyle);

            int removeAnswerIndex = -1;
            DialogNode ownerNode = currentGraph.Nodes.FirstOrDefault(n => n.Phrase == phrase);

            for (int i = 0; i < answersProperty.arraySize; i++)
            {
                SerializedProperty answerProperty = answersProperty.GetArrayElementAtIndex(i);
                SerializedProperty answerTextProperty = answerProperty.FindPropertyRelative("text");
                SerializedProperty nextPhraseProperty = answerProperty.FindPropertyRelative("nextPhrase");
                SerializedProperty forceExitAfterAnswerProperty = answerProperty.FindPropertyRelative("forceExitAfterAnswer");
                SerializedProperty continueForcedDialogueAfterExitProperty = answerProperty.FindPropertyRelative("continueForcedDialogueAfterExit");
                SerializedProperty hasGameplayEventsForAnswerProperty = answerProperty.FindPropertyRelative("hasGameplayEvents");
                SerializedProperty gameplayEventsForAnswerProperty = answerProperty.FindPropertyRelative("gameplayEvents");
                SerializedProperty hasConditionsProperty = answerProperty.FindPropertyRelative("hasConditions");
                SerializedProperty conditionsProperty = answerProperty.FindPropertyRelative("conditions");
                DialogPhrase nextPhrase = nextPhraseProperty?.objectReferenceValue as DialogPhrase;

                bool forceExitAfterAnswer = forceExitAfterAnswerProperty != null &&
                                            forceExitAfterAnswerProperty.boolValue &&
                                            nextPhrase == null;
                if (nextPhrase != null && forceExitAfterAnswerProperty?.boolValue == true)
                {
                    forceExitAfterAnswerProperty.boolValue = false;
                }

                bool missingLink = nextPhrase == null && !forceExitAfterAnswer;
                bool targetOutsideGraph = nextPhrase != null && !ContainsPhrase(nextPhrase);
                bool hasConditions = hasConditionsProperty != null && hasConditionsProperty.boolValue;
                int conditionCount = conditionsProperty?.arraySize ?? 0;
                string foldoutKey = GetAnswerFoldoutKey(phrase, i);
                bool isExpanded = GetAnswerFoldoutState(foldoutKey);
                Color accentColor = GetAnswerAccentColor(
                    missingLink,
                    forceExitAfterAnswer,
                    targetOutsideGraph,
                    hasConditions);
                string statusLabel = GetAnswerStatusLabel(
                    missingLink,
                    forceExitAfterAnswer,
                    targetOutsideGraph,
                    hasConditions,
                    conditionCount);

                EditorGUILayout.BeginHorizontal(HelpBoxStyle);

                Rect accentRect = GUILayoutUtility.GetRect(
                    AccentLineWidth,
                    AccentLineWidth,
                    GUILayout.Width(AccentLineWidth),
                    GUILayout.ExpandHeight(true));
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(accentRect, accentColor);
                }

                EditorGUILayout.BeginVertical();
                EditorGUILayout.BeginHorizontal();
                bool newExpanded = EditorGUILayout.Foldout(isExpanded, $"Answer {i + 1}", true, FoldoutStyle);
                if (newExpanded != isExpanded)
                {
                    SetAnswerFoldoutState(foldoutKey, newExpanded);
                    isExpanded = newExpanded;
                    InvalidateNodeLayout(phrase);
                }

                GUILayout.Label(statusLabel, CenteredMiniLabelStyle, GUILayout.Width(110f));
                GUILayout.FlexibleSpace();

                if (DrawMiniButton("X", GUILayout.Width(22f)))
                {
                    removeAnswerIndex = i;
                }

                Color previousBackground = GUI.backgroundColor;
                GUI.backgroundColor = missingLink ? DangerButtonColor : LinkButtonColor;
                bool pickPressed = DrawMiniButton("O", GUILayout.Width(22f));
                GUI.backgroundColor = previousBackground;

                if (pickPressed && i < phrase.Answers.Count)
                {
                    targetSelection.Begin(phrase, phrase.Answers[i]);
                    toolkitCanvas?.RefreshTargetSelection();
                }

                if (isExpanded)
                {
                    EditorGUILayout.EndHorizontal();
                    DrawAnswerDivider(StrongDividerColor);
                    EditorGUILayout.Space(3f);

                    if (answerTextProperty != null)
                    {
                        DrawLocalizedStringSelector(answerTextProperty, "Text");
                    }

                    if (hasConditionsProperty != null)
                    {
                        EditorGUILayout.PropertyField(hasConditionsProperty, new GUIContent("Condition"));
                        if (hasConditionsProperty.boolValue && conditionsProperty != null)
                        {
                            DrawDialogAnswerConditions(conditionsProperty);
                            DrawAnswerQuestLinksSummary(conditionsProperty);
                        }
                    }

                    if (nextPhraseProperty != null)
                    {
                        DrawPropertyFieldWithCustomLabel(nextPhraseProperty, "Next Phrase");
                    }

                    bool hasNextPhrase = nextPhraseProperty?.objectReferenceValue != null;
                    if (hasNextPhrase && forceExitAfterAnswerProperty?.boolValue == true)
                    {
                        forceExitAfterAnswerProperty.boolValue = false;
                    }
                    else if (!hasNextPhrase && forceExitAfterAnswerProperty != null)
                    {
                        EditorGUILayout.PropertyField(
                            forceExitAfterAnswerProperty,
                            new GUIContent("Force Exit After Player Answer"));

                        if (forceExitAfterAnswerProperty.boolValue && continueForcedDialogueAfterExitProperty != null)
                        {
                            EditorGUILayout.PropertyField(
                                continueForcedDialogueAfterExitProperty,
                                new GUIContent("Continue Forced Dialogue After Exit"));
                        }
                    }

                    if (!hasNextPhrase && forceExitAfterAnswerProperty?.boolValue != true)
                    {
                        EditorGUILayout.HelpBox("Next phrase is not assigned for this answer.", MessageType.Error);
                    }
                    else if (hasNextPhrase && targetOutsideGraph)
                    {
                        EditorGUILayout.HelpBox("Target phrase is not added to the current dialog graph.", MessageType.Warning);
                    }

                    DrawGameplayEvents(hasGameplayEventsForAnswerProperty, gameplayEventsForAnswerProperty);
                }
                else
                {
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();

                if (i < answersProperty.arraySize - 1)
                {
                    EditorGUILayout.Space(3f);
                    DrawAnswerDivider(SoftDividerColor);
                    EditorGUILayout.Space(5f);
                }
                else
                {
                    EditorGUILayout.Space(4f);
                }
            }

            if (DrawButton("+ Add Answer"))
            {
                answersProperty.arraySize++;
                ClearAnswerFoldoutStates(phrase);
                InvalidateNodeLayout(phrase);
            }

            if (removeAnswerIndex >= 0)
            {
                answersProperty.DeleteArrayElementAtIndex(removeAnswerIndex);
                ClearAnswerFoldoutStates(phrase);
                InvalidateNodeLayout(phrase);
            }

            if (phraseObject.hasModifiedProperties)
            {
                phraseObject.ApplyModifiedProperties();
                MarkDirty(phrase);
                InvalidatePhraseDisplayName(phrase);
                InvalidateGraphCaches();
                InvalidateNodeLayout(phrase);
            }
        }

        private void DrawAlternativePhraseTexts(SerializedProperty alternativeTextsProperty)
        {
            if (alternativeTextsProperty == null)
            {
                return;
            }

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Random Text Variants", MiniBoldLabelStyle);
            EditorGUILayout.BeginVertical(HelpBoxStyle);
            EditorGUILayout.LabelField(
                "One of the main phrase or these variants is chosen once whenever the phrase is shown.",
                WordWrappedMiniLabelStyle);

            int removeIndex = -1;
            for (int i = 0; i < alternativeTextsProperty.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                DrawLocalizedStringSelector(alternativeTextsProperty.GetArrayElementAtIndex(i), $"Variant {i + 1}");
                if (DrawMiniButton("X", GUILayout.Width(22f)))
                {
                    removeIndex = i;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (DrawButton("+ Add Text Variant"))
            {
                alternativeTextsProperty.arraySize++;
            }

            if (removeIndex >= 0)
            {
                alternativeTextsProperty.DeleteArrayElementAtIndex(removeIndex);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawConversationTopicSettings(SerializedProperty conversationAnswerProperty)
        {
            if (conversationAnswerProperty == null)
            {
                EditorGUILayout.HelpBox("Conversation topic answer property was not found.", MessageType.Error);
                return;
            }

            SerializedProperty answerTextProperty = conversationAnswerProperty.FindPropertyRelative("text");
            SerializedProperty hasGameplayEventsProperty = conversationAnswerProperty.FindPropertyRelative("hasGameplayEvents");
            SerializedProperty gameplayEventsProperty = conversationAnswerProperty.FindPropertyRelative("gameplayEvents");
            SerializedProperty hasConditionsProperty = conversationAnswerProperty.FindPropertyRelative("hasConditions");
            SerializedProperty conditionsProperty = conversationAnswerProperty.FindPropertyRelative("conditions");

            EditorGUILayout.Space(2f);
            EditorGUILayout.BeginVertical(HelpBoxStyle);
            EditorGUILayout.LabelField(
                "This answer is shown alongside quest branches whenever the player is free to choose a conversation topic.",
                WordWrappedMiniLabelStyle);
            DrawAnswerDetails(answerTextProperty, hasConditionsProperty, conditionsProperty, "Topic Answer");
            DrawGameplayEvents(hasGameplayEventsProperty, gameplayEventsProperty);
            EditorGUILayout.EndVertical();
        }

        private void DrawConversationReturnActionSettings(SerializedProperty returnAnswerProperty)
        {
            if (returnAnswerProperty == null)
            {
                EditorGUILayout.HelpBox("Conversation return answer property was not found.", MessageType.Error);
                return;
            }

            SerializedProperty answerTextProperty = returnAnswerProperty.FindPropertyRelative("text");
            SerializedProperty nextPhraseProperty = returnAnswerProperty.FindPropertyRelative("nextPhrase");
            SerializedProperty hasConditionsProperty = returnAnswerProperty.FindPropertyRelative("hasConditions");
            SerializedProperty conditionsProperty = returnAnswerProperty.FindPropertyRelative("conditions");

            EditorGUILayout.Space(2f);
            EditorGUILayout.BeginVertical(HelpBoxStyle);
            EditorGUILayout.LabelField(
                "This answer is shown automatically on every phrase reachable from a conversation topic when its conditions are satisfied.",
                WordWrappedMiniLabelStyle);
            DrawAnswerDetails(answerTextProperty, hasConditionsProperty, conditionsProperty, "Return Answer");
            EditorGUILayout.PropertyField(
                nextPhraseProperty,
                new GUIContent("Return Target", "The phrase shown after the player leaves the conversation topic branch."));
            EditorGUILayout.EndVertical();
        }

        private void DrawDialogueExitActionSettings(SerializedProperty exitAnswerProperty)
        {
            if (exitAnswerProperty == null)
            {
                EditorGUILayout.HelpBox("Dialogue exit answer property was not found.", MessageType.Error);
                return;
            }

            SerializedProperty answerTextProperty = exitAnswerProperty.FindPropertyRelative("text");
            SerializedProperty hasConditionsProperty = exitAnswerProperty.FindPropertyRelative("hasConditions");
            SerializedProperty conditionsProperty = exitAnswerProperty.FindPropertyRelative("conditions");
            SerializedProperty continueForcedDialogueAfterExitProperty = exitAnswerProperty.FindPropertyRelative("continueForcedDialogueAfterExit");

            EditorGUILayout.Space(2f);
            EditorGUILayout.BeginVertical(HelpBoxStyle);
            EditorGUILayout.LabelField(
                "This answer ends the dialogue through the normal dialogue-exit lifecycle. It is added automatically at regular choice points and when no other answer is available.",
                WordWrappedMiniLabelStyle);
            DrawAnswerDetails(answerTextProperty, hasConditionsProperty, conditionsProperty, "Exit Answer");
            if (continueForcedDialogueAfterExitProperty != null)
            {
                EditorGUILayout.PropertyField(
                    continueForcedDialogueAfterExitProperty,
                    new GUIContent("Continue Forced Dialogue After Exit"));
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawQuestPhraseSettings(SerializedProperty questAnswerProperty)
        {
            if (questAnswerProperty == null)
            {
                EditorGUILayout.HelpBox("Quest answer property was not found.", MessageType.Error);
                return;
            }

            SerializedProperty answerTextProperty = questAnswerProperty.FindPropertyRelative("text");
            SerializedProperty hasGameplayEventsProperty = questAnswerProperty.FindPropertyRelative("hasGameplayEvents");
            SerializedProperty gameplayEventsProperty = questAnswerProperty.FindPropertyRelative("gameplayEvents");
            SerializedProperty hasConditionsProperty = questAnswerProperty.FindPropertyRelative("hasConditions");
            SerializedProperty conditionsProperty = questAnswerProperty.FindPropertyRelative("conditions");

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Quest Entry Answer", MiniBoldLabelStyle);
            EditorGUILayout.BeginVertical(HelpBoxStyle);
            EditorGUILayout.LabelField(
                "This answer is shown on the start phrase automatically when its conditions are satisfied.",
                WordWrappedMiniLabelStyle);
            EditorGUILayout.Space(3f);

            DrawAnswerDetails(
                answerTextProperty,
                hasConditionsProperty,
                conditionsProperty,
                "Answer Text");

            DrawGameplayEvents(hasGameplayEventsProperty, gameplayEventsProperty);

            EditorGUILayout.EndVertical();
        }

        private void DrawAnswerDetails(
            SerializedProperty answerTextProperty,
            SerializedProperty hasConditionsProperty,
            SerializedProperty conditionsProperty,
            string textLabel)
        {
            if (answerTextProperty != null)
            {
                DrawLocalizedStringSelector(answerTextProperty, textLabel);
            }

            if (hasConditionsProperty == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(hasConditionsProperty, new GUIContent("Condition"));
            if (!hasConditionsProperty.boolValue || conditionsProperty == null)
            {
                return;
            }

            DrawDialogAnswerConditions(conditionsProperty);
            DrawAnswerQuestLinksSummary(conditionsProperty);
        }

        private static void DrawGameplayEvents(
            SerializedProperty hasGameplayEventsProperty,
            SerializedProperty gameplayEventsProperty)
        {
            if (hasGameplayEventsProperty == null)
            {
                return;
            }

            EditorGUILayout.Space(3f);
            EditorGUILayout.PropertyField(
                hasGameplayEventsProperty,
                new GUIContent("Gameplay Events", "Publishes the selected typed gameplay events after this phrase or answer is shown."));

            if (hasGameplayEventsProperty.boolValue && gameplayEventsProperty != null)
            {
                EditorGUILayout.PropertyField(gameplayEventsProperty, new GUIContent("Events"), true);
            }
        }

        private string GetAnswerFoldoutKey(DialogPhrase phrase, int answerIndex)
        {
            return $"{phrase.GetInstanceID()}:{answerIndex}";
        }

        private bool GetAnswerFoldoutState(string foldoutKey)
        {
            return answerFoldoutState.Get(foldoutKey);
        }

        private bool CanRestoreExitAbility(DialogPhrase phrase)
        {
            if (phrase == null || currentGraph == null)
            {
                return false;
            }

            return exitAbilityCache.Get(currentGraph, phrase);
        }

        private void SetAnswerFoldoutState(string foldoutKey, bool isExpanded)
        {
            answerFoldoutState.Set(foldoutKey, isExpanded);
        }

        private void InvalidateNodeLayout(DialogPhrase phrase)
        {
            if (phrase == null)
            {
                return;
            }

            phrasesWithDirtyLayout.Add(phrase);
            phrasesAwaitingRepaintAfterLayout.Remove(phrase);
            toolkitCanvas?.InvalidateNodeLayout(phrase);
            Repaint();
        }

        private void ClearAnswerFoldoutStates(DialogPhrase phrase)
        {
            if (phrase == null)
            {
                return;
            }

            answerFoldoutState.Clear(phrase);
        }

        private Color GetAnswerAccentColor(
            bool missingLink,
            bool forceExitAfterAnswer,
            bool targetOutsideGraph,
            bool hasConditions)
        {
            if (missingLink)
            {
                return DangerAccentColor;
            }

            if (forceExitAfterAnswer)
            {
                return RewardAccentColor;
            }

            if (targetOutsideGraph)
            {
                return WarningAccentColor;
            }

            if (hasConditions)
            {
                return ConditionAccentColor;
            }

            return RewardAccentColor;
        }

        private static string GetAnswerStatusLabel(
            bool missingLink,
            bool forceExitAfterAnswer,
            bool targetOutsideGraph,
            bool hasConditions,
            int conditionCount)
        {
            if (missingLink)
            {
                return "Missing Next";
            }

            if (forceExitAfterAnswer)
            {
                return "Exit Dialogue";
            }

            if (targetOutsideGraph)
            {
                return "Outside Graph";
            }

            if (hasConditions)
            {
                return conditionCount > 0
                    ? $"Conditions: {conditionCount}"
                    : "Has Conditions";
            }

            return "Linked";
        }

        private void DrawAnswerDivider(Color color)
        {
            Rect dividerRect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(dividerRect, color);
        }

        private Color GetConditionAccentColor(DialogAnswerConditionType conditionType)
        {
            return ItemAccentColor;
        }

        private static string GetConditionTitle(SerializedProperty typeProperty)
        {
            if (typeProperty == null || typeProperty.propertyType != SerializedPropertyType.Enum)
            {
                return "Condition";
            }

            string[] displayNames = typeProperty.enumDisplayNames;
            int index = typeProperty.enumValueIndex;
            if (displayNames == null || index < 0 || index >= displayNames.Length)
            {
                return "Condition";
            }

            return displayNames[index];
        }

        private void DrawDialogAnswerConditions(SerializedProperty conditionsProperty)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Conditions / Actions", MiniBoldLabelStyle);

            int removeIndex = -1;
            for (int i = 0; i < conditionsProperty.arraySize; i++)
            {
                SerializedProperty conditionProperty = conditionsProperty.GetArrayElementAtIndex(i);
                SerializedProperty typeProperty = conditionProperty.FindPropertyRelative("type");
                SerializedProperty moneyAmountProperty = conditionProperty.FindPropertyRelative("moneyAmount");
                SerializedProperty itemConfigProperty = conditionProperty.FindPropertyRelative("itemConfig");
                SerializedProperty itemCountProperty = conditionProperty.FindPropertyRelative("itemCount");
                SerializedProperty questGraphProperty = conditionProperty.FindPropertyRelative("questGraph");
                SerializedProperty questSourceNodeProperty = conditionProperty.FindPropertyRelative("questSourceNode");
                SerializedProperty questTransitionProperty = conditionProperty.FindPropertyRelative("questTransition");
                SerializedProperty questNodeProperty = conditionProperty.FindPropertyRelative("questNode");
                SerializedProperty runtimeFlagProperty = conditionProperty.FindPropertyRelative("runtimeFlag");
                DialogAnswerConditionType conditionType = (DialogAnswerConditionType)typeProperty.enumValueIndex;
                Color accentColor = GetConditionAccentColor(conditionType);
                string conditionTitle = GetConditionTitle(typeProperty);

                EditorGUILayout.BeginHorizontal(HelpBoxStyle);
                Rect accentRect = GUILayoutUtility.GetRect(
                    AccentLineWidth,
                    AccentLineWidth,
                    GUILayout.Width(AccentLineWidth),
                    GUILayout.ExpandHeight(true));
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(accentRect, accentColor);
                }

                EditorGUILayout.BeginVertical();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Entry {i + 1}", MiniBoldLabelStyle, GUILayout.Width(52f));
                GUILayout.Label(conditionTitle, CenteredMiniLabelStyle);
                GUILayout.FlexibleSpace();
                if (DrawMiniButton("X", GUILayout.Width(22f)))
                {
                    removeIndex = i;
                }

                EditorGUILayout.EndHorizontal();
                DrawAnswerDivider(SectionDividerColor);
                EditorGUILayout.Space(3f);

                DrawEnumPropertyField(typeProperty, "Type");

                switch (conditionType)
                {
                    case DialogAnswerConditionType.GiveMoney:
                    case DialogAnswerConditionType.TakeMoney:
                    case DialogAnswerConditionType.TakeMoneyMax:
                        DrawPropertyFieldWithCustomLabel(moneyAmountProperty, "Money");
                        break;
                    case DialogAnswerConditionType.TakeItemIfHas:
                        DrawPropertyFieldWithCustomLabel(itemConfigProperty, "Item");
                        DrawPropertyFieldWithCustomLabel(itemCountProperty, "Count");
                        break;
                    case DialogAnswerConditionType.CheckQuestStep:
                    case DialogAnswerConditionType.DoQuestStep:
                        DrawQuestTransitionSelector(
                            conditionsProperty,
                            i,
                            questGraphProperty,
                            questSourceNodeProperty,
                            questTransitionProperty);
                        break;
                    case DialogAnswerConditionType.AddQuest:
                    case DialogAnswerConditionType.CanAddQuest:
                        DrawQuestGraphSelector(conditionsProperty, i, questGraphProperty);
                        QuestPreviewUtility.DrawQuestGraphPreview(questGraphProperty.objectReferenceValue as QuestGraph, "Quest");
                        break;
                    case DialogAnswerConditionType.DoQuestEnd:
                        DrawTerminalQuestNodeSelector(conditionsProperty, i, questGraphProperty, questNodeProperty);
                        break;
                    case DialogAnswerConditionType.RequireRuntimeFlag:
                    case DialogAnswerConditionType.ClearRuntimeFlag:
                    case DialogAnswerConditionType.RequireInactiveRuntimeFlag:
                    case DialogAnswerConditionType.SetRuntimeFlag:
                        DrawPropertyFieldWithCustomLabel(runtimeFlagProperty, "Runtime flag");
                        break;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();

                if (i < conditionsProperty.arraySize - 1)
                {
                    EditorGUILayout.Space(2f);
                    DrawAnswerDivider(SoftestDividerColor);
                    EditorGUILayout.Space(4f);
                }
                else
                {
                    EditorGUILayout.Space(2f);
                }
            }

            if (removeIndex >= 0)
            {
                conditionsProperty.DeleteArrayElementAtIndex(removeIndex);
            }

            if (DrawButton("+ Add Condition / Action"))
            {
                conditionsProperty.arraySize++;
            }
        }

        private void DrawAnswerQuestLinksSummary(SerializedProperty conditionsProperty)
        {
            List<string> lines = new();
            for (int i = 0; i < conditionsProperty.arraySize; i++)
            {
                string summary = GetQuestConditionSummary(conditionsProperty.GetArrayElementAtIndex(i));
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    lines.Add(summary);
                }
            }

            if (lines.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Quest Links", MiniBoldLabelStyle);
            EditorGUILayout.BeginVertical(HelpBoxStyle);
            foreach (string line in lines)
            {
                EditorGUILayout.LabelField(line, WordWrappedMiniLabelStyle);
            }

            EditorGUILayout.EndVertical();
        }

        private string GetQuestConditionSummary(SerializedProperty conditionProperty)
        {
            SerializedProperty typeProperty = conditionProperty.FindPropertyRelative("type");
            SerializedProperty questGraphProperty = conditionProperty.FindPropertyRelative("questGraph");
            SerializedProperty questSourceNodeProperty = conditionProperty.FindPropertyRelative("questSourceNode");
            SerializedProperty questTransitionProperty = conditionProperty.FindPropertyRelative("questTransition");
            SerializedProperty questNodeProperty = conditionProperty.FindPropertyRelative("questNode");
            SerializedProperty runtimeFlagProperty = conditionProperty.FindPropertyRelative("runtimeFlag");

            DialogAnswerConditionType type = (DialogAnswerConditionType)typeProperty.enumValueIndex;
            QuestGraph questGraph = questGraphProperty.objectReferenceValue as QuestGraph;
            QuestNodeData questSourceNode = questSourceNodeProperty.objectReferenceValue as QuestNodeData;
            QuestTransition transition = questTransitionProperty.objectReferenceValue as QuestTransition;
            QuestNodeData questNode = questNodeProperty.objectReferenceValue as QuestNodeData;
            DialogueRuntimeFlag runtimeFlag = runtimeFlagProperty.objectReferenceValue as DialogueRuntimeFlag;

            return type switch
            {
                DialogAnswerConditionType.AddQuest when questGraph != null =>
                    $"Add Quest -> {QuestPreviewUtility.GetQuestDisplayName(questGraph)}",
                DialogAnswerConditionType.CanAddQuest when questGraph != null =>
                    $"Require Addable Quest -> {QuestPreviewUtility.GetQuestDisplayName(questGraph)}",
                DialogAnswerConditionType.CheckQuestStep when questGraph != null && transition != null =>
                    $"Check Transition -> {QuestPreviewUtility.GetQuestDisplayName(questGraph)}: {GetQuestTransitionLabel(questGraph, questSourceNode, transition)}",
                DialogAnswerConditionType.DoQuestStep when questGraph != null && transition != null =>
                    $"Execute Transition -> {QuestPreviewUtility.GetQuestDisplayName(questGraph)}: {GetQuestTransitionLabel(questGraph, questSourceNode, transition)}",
                DialogAnswerConditionType.DoQuestEnd when questGraph != null && questNode != null =>
                    $"Execute Final Node -> {QuestPreviewUtility.GetQuestDisplayName(questGraph)}: {QuestPreviewUtility.GetNodeDisplayName(questNode)}",
                DialogAnswerConditionType.RequireRuntimeFlag when runtimeFlag != null =>
                    $"Require Runtime Flag -> {runtimeFlag.name}",
                DialogAnswerConditionType.ClearRuntimeFlag when runtimeFlag != null =>
                    $"Clear Runtime Flag -> {runtimeFlag.name}",
                DialogAnswerConditionType.RequireInactiveRuntimeFlag when runtimeFlag != null =>
                    $"Require Inactive Runtime Flag -> {runtimeFlag.name}",
                DialogAnswerConditionType.SetRuntimeFlag when runtimeFlag != null =>
                    $"Set Runtime Flag -> {runtimeFlag.name}",
                _ => null
            };
        }

        private void DrawQuestGraphSelector(
            SerializedProperty conditionsProperty,
            int currentIndex,
            SerializedProperty questGraphProperty,
            string label = "Quest")
        {
            DrawRelatedQuestShortcuts(conditionsProperty, currentIndex, questGraphProperty);

            List<QuestGraph> questGraphs = GetAllQuestGraphs();
            if (questGraphs.Count == 0)
            {
                questGraphProperty.objectReferenceValue = null;
                EditorGUILayout.HelpBox("No quest graphs were found.", MessageType.Warning);
                return;
            }

            UnityEngine.Object targetObject = conditionsProperty.serializedObject.targetObject;
            QuestGraph currentQuestGraph = questGraphProperty.objectReferenceValue as QuestGraph;
            if (currentQuestGraph != null && !questGraphs.Contains(currentQuestGraph))
            {
                questGraphProperty.objectReferenceValue = null;
                currentQuestGraph = null;
            }

            string buttonLabel = currentQuestGraph == null
                ? $"Select {label}"
                : QuestPreviewUtility.GetQuestDisplayName(currentQuestGraph);

            Rect selectorRect = EditorGUILayout.GetControlRect();
            if (GUI.Button(selectorRect, buttonLabel, PopupStyle))
            {
                OpenQuestGraphSelector(
                    selectorRect,
                    targetObject,
                    questGraphProperty.propertyPath,
                    questGraphs,
                    currentQuestGraph,
                    label);
            }
        }

        private void DrawRelatedQuestShortcuts(
            SerializedProperty conditionsProperty,
            int currentIndex,
            SerializedProperty questGraphProperty)
        {
            List<QuestGraph> relatedGraphs = GetRelatedQuestGraphs(conditionsProperty, currentIndex);
            if (relatedGraphs.Count == 0)
            {
                return;
            }

            EditorGUILayout.LabelField("Used In This Answer", MiniLabelStyle);
            EditorGUILayout.BeginHorizontal();
            foreach (QuestGraph relatedGraph in relatedGraphs)
            {
                string label = QuestPreviewUtility.GetQuestDisplayName(relatedGraph);
                if (DrawMiniButton(label))
                {
                    questGraphProperty.objectReferenceValue = relatedGraph;
                    QuestPreviewPopup.ShowQuest(GUILayoutUtility.GetLastRect(), relatedGraph);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private List<QuestGraph> GetRelatedQuestGraphs(SerializedProperty conditionsProperty, int currentIndex)
        {
            var graphs = new List<QuestGraph>();
            for (int i = 0; i < conditionsProperty.arraySize; i++)
            {
                if (i == currentIndex)
                {
                    continue;
                }

                SerializedProperty graphProperty = conditionsProperty
                    .GetArrayElementAtIndex(i)
                    .FindPropertyRelative("questGraph");

                QuestGraph questGraph = graphProperty?.objectReferenceValue as QuestGraph;
                if (questGraph != null && !graphs.Contains(questGraph))
                {
                    graphs.Add(questGraph);
                }
            }

            return graphs;
        }

        private void DrawQuestTransitionSelector(
            SerializedProperty conditionsProperty,
            int currentIndex,
            SerializedProperty questGraphProperty,
            SerializedProperty questSourceNodeProperty,
            SerializedProperty questTransitionProperty)
        {
            List<QuestNodeData> sourceNodes = GetAllQuestSourceNodes();
            if (sourceNodes.Count == 0)
            {
                questGraphProperty.objectReferenceValue = null;
                questSourceNodeProperty.objectReferenceValue = null;
                questTransitionProperty.objectReferenceValue = null;
                EditorGUILayout.HelpBox("No quest nodes with transitions were found.", MessageType.Warning);
                return;
            }

            UnityEngine.Object targetObject = conditionsProperty.serializedObject.targetObject;
            QuestNodeData currentSourceNode = questSourceNodeProperty.objectReferenceValue as QuestNodeData;
            if (currentSourceNode != null && !sourceNodes.Contains(currentSourceNode))
            {
                questSourceNodeProperty.objectReferenceValue = null;
                currentSourceNode = null;
            }

            string sourceNodeLabel = currentSourceNode == null
                ? "Select Source Node"
                : GetQuestNodeOptionLabel(currentSourceNode);

            Rect sourceNodeRect = EditorGUILayout.GetControlRect();
            if (GUI.Button(sourceNodeRect, sourceNodeLabel, PopupStyle))
            {
                OpenSourceNodeSelector(
                    sourceNodeRect,
                    targetObject,
                    questGraphProperty.propertyPath,
                    questSourceNodeProperty.propertyPath,
                    questTransitionProperty.propertyPath,
                    sourceNodes,
                    currentSourceNode);
            }

            currentSourceNode = questSourceNodeProperty.objectReferenceValue as QuestNodeData;
            if (currentSourceNode == null)
            {
                return;
            }

            QuestGraph questGraph = currentSourceNode.OwnerGraph;
            questGraphProperty.objectReferenceValue = questGraph;
            QuestPreviewUtility.DrawQuestNodePreview(currentSourceNode, "Source Node");

            List<QuestTransition> transitions = currentSourceNode.Transitions?
                .Where(transition => transition != null)
                .ToList() ?? new List<QuestTransition>();

            if (transitions.Count == 0)
            {
                questTransitionProperty.objectReferenceValue = null;
                EditorGUILayout.HelpBox("This node has no transitions.", MessageType.Warning);
                return;
            }

            QuestTransition currentTransition = questTransitionProperty.objectReferenceValue as QuestTransition;
            if (currentTransition != null && !transitions.Contains(currentTransition))
            {
                questTransitionProperty.objectReferenceValue = null;
                currentTransition = null;
            }

            string transitionLabel = currentTransition == null
                ? "Select Transition"
                : GetQuestTransitionLabel(questGraph, currentSourceNode, currentTransition);

            Rect transitionRect = EditorGUILayout.GetControlRect();
            if (GUI.Button(transitionRect, transitionLabel, PopupStyle))
            {
                OpenTransitionSelector(
                    transitionRect,
                    targetObject,
                    questTransitionProperty.propertyPath,
                    questGraph,
                    currentSourceNode,
                    transitions,
                    currentTransition);
            }

            QuestPreviewUtility.DrawQuestTransitionPreview(questGraph, questTransitionProperty.objectReferenceValue as QuestTransition, "Transition");
        }

        private void DrawTerminalQuestNodeSelector(
            SerializedProperty conditionsProperty,
            int currentIndex,
            SerializedProperty questGraphProperty,
            SerializedProperty questNodeProperty)
        {
            List<QuestNodeData> terminalNodes = GetAllTerminalQuestNodes();
            if (terminalNodes.Count == 0)
            {
                questGraphProperty.objectReferenceValue = null;
                questNodeProperty.objectReferenceValue = null;
                EditorGUILayout.HelpBox("No terminal quest nodes were found.", MessageType.Warning);
                return;
            }

            UnityEngine.Object targetObject = conditionsProperty.serializedObject.targetObject;
            QuestNodeData currentNode = questNodeProperty.objectReferenceValue as QuestNodeData;
            if (currentNode != null && !terminalNodes.Contains(currentNode))
            {
                questNodeProperty.objectReferenceValue = null;
                currentNode = null;
            }

            string terminalNodeLabel = currentNode == null
                ? "Select Terminal Node"
                : GetQuestNodeOptionLabel(currentNode);

            Rect terminalNodeRect = EditorGUILayout.GetControlRect();
            if (GUI.Button(terminalNodeRect, terminalNodeLabel, PopupStyle))
            {
                OpenTerminalNodeSelector(
                    terminalNodeRect,
                    targetObject,
                    questGraphProperty.propertyPath,
                    questNodeProperty.propertyPath,
                    terminalNodes,
                    currentNode);
            }

            QuestPreviewUtility.DrawQuestNodePreview(questNodeProperty.objectReferenceValue as QuestNodeData, "Quest Node");
        }

        private static QuestNodeData GetOwnerNodeDataForTransition(QuestGraph questGraph, QuestTransition transition)
        {
            if (questGraph == null || transition == null)
            {
                return null;
            }

            return questGraph.Nodes
                .FirstOrDefault(node =>
                    node?.NodeData != null &&
                    node.NodeData.Transitions != null &&
                    node.NodeData.Transitions.Contains(transition))
                ?.NodeData;
        }

        private void OpenSourceNodeSelector(
            Rect activatorRect,
            UnityEngine.Object targetObject,
            string questGraphPropertyPath,
            string questSourceNodePropertyPath,
            string questTransitionPropertyPath,
            List<QuestNodeData> sourceNodes,
            QuestNodeData currentSourceNode)
        {
            var entries = new List<QuestCardSelectorPopup.Entry>
            {
                new()
                {
                    Title = "<None>",
                    Subtitle = "Clear source node selection",
                    IsSelected = currentSourceNode == null,
                    OnSelect = () =>
                    {
                        ApplySourceNodeSelection(targetObject, questGraphPropertyPath, questSourceNodePropertyPath, questTransitionPropertyPath, null);
                    }
                }
            };

            entries.AddRange(sourceNodes.Select(nodeData => new QuestCardSelectorPopup.Entry
            {
                Title = QuestPreviewUtility.GetNodeDisplayName(nodeData),
                Subtitle = nodeData.OwnerGraph != null ? QuestPreviewUtility.GetQuestDisplayName(nodeData.OwnerGraph) : "Unknown Quest",
                Sprite = nodeData.Icon,
                IsSelected = nodeData == currentSourceNode,
                OnSelect = () =>
                {
                    ApplySourceNodeSelection(targetObject, questGraphPropertyPath, questSourceNodePropertyPath, questTransitionPropertyPath, nodeData);
                }
            }));

            QuestCardSelectorPopup.Show(activatorRect, "Select Source Node", entries);
        }

        private void OpenQuestGraphSelector(
            Rect activatorRect,
            UnityEngine.Object targetObject,
            string questGraphPropertyPath,
            List<QuestGraph> questGraphs,
            QuestGraph currentQuestGraph,
            string label)
        {
            var entries = new List<QuestCardSelectorPopup.Entry>
            {
                new()
                {
                    Title = "<None>",
                    Subtitle = "Clear quest selection",
                    IsSelected = currentQuestGraph == null,
                    OnSelect = () =>
                    {
                        ApplyQuestGraphSelection(targetObject, questGraphPropertyPath, null);
                    }
                }
            };

            entries.AddRange(questGraphs.Select(questGraph => new QuestCardSelectorPopup.Entry
            {
                Title = QuestPreviewUtility.GetQuestDisplayName(questGraph),
                Subtitle = QuestPreviewUtility.GetQuestDescription(questGraph),
                Sprite = questGraph.GetEntryNode()?.Icon,
                IsSelected = questGraph == currentQuestGraph,
                OnSelect = () =>
                {
                    ApplyQuestGraphSelection(targetObject, questGraphPropertyPath, questGraph);
                }
            }));

            QuestCardSelectorPopup.Show(activatorRect, $"Select {label}", entries);
        }

        private void OpenTransitionSelector(
            Rect activatorRect,
            UnityEngine.Object targetObject,
            string questTransitionPropertyPath,
            QuestGraph questGraph,
            QuestNodeData sourceNodeData,
            List<QuestTransition> transitions,
            QuestTransition currentTransition)
        {
            var entries = new List<QuestCardSelectorPopup.Entry>
            {
                new()
                {
                    Title = "<None>",
                    Subtitle = "Clear transition selection",
                    IsSelected = currentTransition == null,
                    OnSelect = () =>
                    {
                        ApplyTransitionSelection(targetObject, questTransitionPropertyPath, null);
                    }
                }
            };

            entries.AddRange(transitions.Select(transition => new QuestCardSelectorPopup.Entry
            {
                Title = GetQuestTransitionLabel(questGraph, sourceNodeData, transition),
                Subtitle = sourceNodeData != null ? QuestPreviewUtility.GetNodeDisplayName(sourceNodeData) : string.Empty,
                Sprite = transition.TargetNode != null ? transition.TargetNode.Icon : null,
                IsSelected = transition == currentTransition,
                OnSelect = () =>
                {
                    ApplyTransitionSelection(targetObject, questTransitionPropertyPath, transition);
                }
            }));

            QuestCardSelectorPopup.Show(activatorRect, "Select Transition", entries);
        }

        private void OpenTerminalNodeSelector(
            Rect activatorRect,
            UnityEngine.Object targetObject,
            string questGraphPropertyPath,
            string questNodePropertyPath,
            List<QuestNodeData> terminalNodes,
            QuestNodeData currentNode)
        {
            var entries = new List<QuestCardSelectorPopup.Entry>
            {
                new()
                {
                    Title = "<None>",
                    Subtitle = "Clear terminal node selection",
                    IsSelected = currentNode == null,
                    OnSelect = () =>
                    {
                        ApplyTerminalNodeSelection(targetObject, questGraphPropertyPath, questNodePropertyPath, null);
                    }
                }
            };

            entries.AddRange(terminalNodes.Select(nodeData => new QuestCardSelectorPopup.Entry
            {
                Title = QuestPreviewUtility.GetNodeDisplayName(nodeData),
                Subtitle = nodeData.OwnerGraph != null ? QuestPreviewUtility.GetQuestDisplayName(nodeData.OwnerGraph) : "Unknown Quest",
                Sprite = nodeData.Icon,
                IsSelected = nodeData == currentNode,
                OnSelect = () =>
                {
                    ApplyTerminalNodeSelection(targetObject, questGraphPropertyPath, questNodePropertyPath, nodeData);
                }
            }));

            QuestCardSelectorPopup.Show(activatorRect, "Select Terminal Node", entries);
        }

        private void ApplySourceNodeSelection(
            UnityEngine.Object targetObject,
            string questGraphPropertyPath,
            string questSourceNodePropertyPath,
            string questTransitionPropertyPath,
            QuestNodeData selectedNode)
        {
            SerializedObject serializedObject = new SerializedObject(targetObject);
            SerializedProperty questGraphProperty = serializedObject.FindProperty(questGraphPropertyPath);
            SerializedProperty questSourceNodeProperty = serializedObject.FindProperty(questSourceNodePropertyPath);
            SerializedProperty questTransitionProperty = serializedObject.FindProperty(questTransitionPropertyPath);

            questGraphProperty.objectReferenceValue = selectedNode != null ? selectedNode.OwnerGraph : null;
            questSourceNodeProperty.objectReferenceValue = selectedNode;
            questTransitionProperty.objectReferenceValue = null;

            serializedObject.ApplyModifiedProperties();
            MarkDirty(targetObject);
            Repaint();
        }

        private void ApplyQuestGraphSelection(
            UnityEngine.Object targetObject,
            string questGraphPropertyPath,
            QuestGraph selectedQuestGraph)
        {
            SerializedObject serializedObject = new SerializedObject(targetObject);
            SerializedProperty questGraphProperty = serializedObject.FindProperty(questGraphPropertyPath);
            questGraphProperty.objectReferenceValue = selectedQuestGraph;
            serializedObject.ApplyModifiedProperties();
            MarkDirty(targetObject);
            Repaint();
        }

        private void ApplyTransitionSelection(
            UnityEngine.Object targetObject,
            string questTransitionPropertyPath,
            QuestTransition transition)
        {
            SerializedObject serializedObject = new SerializedObject(targetObject);
            SerializedProperty questTransitionProperty = serializedObject.FindProperty(questTransitionPropertyPath);
            questTransitionProperty.objectReferenceValue = transition;
            serializedObject.ApplyModifiedProperties();
            MarkDirty(targetObject);
            Repaint();
        }

        private void ApplyTerminalNodeSelection(
            UnityEngine.Object targetObject,
            string questGraphPropertyPath,
            string questNodePropertyPath,
            QuestNodeData selectedNode)
        {
            SerializedObject serializedObject = new SerializedObject(targetObject);
            SerializedProperty questGraphProperty = serializedObject.FindProperty(questGraphPropertyPath);
            SerializedProperty questNodeProperty = serializedObject.FindProperty(questNodePropertyPath);

            questGraphProperty.objectReferenceValue = selectedNode != null ? selectedNode.OwnerGraph : null;
            questNodeProperty.objectReferenceValue = selectedNode;

            serializedObject.ApplyModifiedProperties();
            MarkDirty(targetObject);
            Repaint();
        }

        private static List<QuestNodeData> GetAllQuestSourceNodes()
        {
            cachedQuestSourceNodes ??= AssetDatabase.FindAssets("t:QuestNodeData")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<QuestNodeData>)
                .Where(nodeData =>
                    nodeData != null &&
                    nodeData.OwnerGraph != null &&
                    nodeData.Transitions != null &&
                    nodeData.Transitions.Any(transition => transition != null))
                .ToList();

            return cachedQuestSourceNodes;
        }

        private static List<QuestGraph> GetAllQuestGraphs()
        {
            cachedQuestGraphs ??= AssetDatabase.FindAssets("t:QuestGraph")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<QuestGraph>)
                .Where(questGraph => questGraph != null)
                .ToList();

            return cachedQuestGraphs;
        }

        private static List<QuestNodeData> GetAllTerminalQuestNodes()
        {
            cachedTerminalQuestNodes ??= AssetDatabase.FindAssets("t:QuestNodeData")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<QuestNodeData>)
                .Where(nodeData =>
                    nodeData != null &&
                    nodeData.OwnerGraph != null &&
                    nodeData.OwnerGraph.IsTerminalNode(nodeData))
                .ToList();

            return cachedTerminalQuestNodes;
        }

        private static string GetQuestNodeOptionLabel(QuestNodeData nodeData)
        {
            if (nodeData == null)
            {
                return "<None>";
            }

            string questName = nodeData.OwnerGraph != null
                ? QuestPreviewUtility.GetQuestDisplayName(nodeData.OwnerGraph)
                : "Unknown Quest";

            return $"{questName} / {QuestPreviewUtility.GetNodeDisplayName(nodeData)}";
        }

        private static string GetQuestTransitionLabel(QuestGraph questGraph, QuestNodeData sourceNodeData, QuestTransition transition)
        {
            if (questGraph == null || transition == null)
            {
                return "<None>";
            }

            QuestNodeData ownerNodeData = sourceNodeData ?? GetOwnerNodeDataForTransition(questGraph, transition);

            string sourceName = ownerNodeData != null
                ? QuestPreviewUtility.GetNodeDisplayName(ownerNodeData)
                : "Unknown";
            string targetName = transition.TargetNode != null
                ? QuestPreviewUtility.GetNodeDisplayName(transition.TargetNode)
                : "Missing Target";
            return $"{sourceName} -> {targetName}";
        }

    }
}
