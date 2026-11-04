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
using UnityEngine.UIElements;

namespace Dialogs.Graph.Editor
{
    public partial class DialogEditorWindow : GraphEditorWindowBase, IDialogPhraseNodeInspectorHost
    {
        private const string PreferredPreviewLocale = "ru";
        private const string DialogsPathKey = "DialogEditor_DialogsPath";
        private const string PhrasesPathKey = "DialogEditor_PhrasesPath";
        private const string ThemeKey = "DialogEditor_Theme";
        private const float DialogNodeWidth = 320f;
        private const float LocalizedPreviewMinHeight = 48f;
        private const float LocalizedPreviewWidth = 268f;
        private const float WorkspaceWidth = 10000f;
        private const float WorkspaceHeight = 10000f;
        private const float ZoomMin = 0.25f;
        private const float ZoomMax = 2f;
        private const float OverlayPanelWidth = 320f;
        private const float AccentLineWidth = 1.5f;

        private DialogGraph currentGraph;
        private string dialogsFolderPath;
        private string phrasesFolderPath;
        private readonly Dictionary<DialogNode, Rect> nodeRects = new();
        private readonly GraphEditorNodeLayoutSynchronizer<DialogNode> nodeLayoutSynchronizer = new();
        private readonly DialogGraphIndex graphIndex = new();
        private readonly DialogPhraseDisplayNameCache phraseDisplayNames = new();
        private readonly DialogExitAbilityCache exitAbilityCache = new();
        private readonly HashSet<DialogPhrase> phrasesWithDirtyLayout = new();
        private readonly HashSet<DialogPhrase> phrasesAwaitingRepaintAfterLayout = new();
        private readonly GUIContent localizedPreviewContent = new();
        private readonly DialogAnswerFoldoutState answerFoldoutState = new();
        private bool graphStructureDirty = true;
        private readonly DialogGraphConnectionPresenter connectionPresenter = new();

        private static System.Collections.ObjectModel.ReadOnlyCollection<StringTableCollection> cachedStringTableCollections;
        private static string[] cachedStringTableOptions;
        private static readonly Dictionary<string, CachedLocalizedEntryOptions> localizedEntryOptionsCache = new();
        private static List<QuestGraph> cachedQuestGraphs;
        private static List<QuestNodeData> cachedQuestSourceNodes;
        private static List<QuestNodeData> cachedTerminalQuestNodes;

        private readonly DialogTargetSelectionState targetSelection = new();
        private DialogNode activeConnectionNode;
        private GraphEditorTheme theme;
        private readonly GraphEditorOverlayPanelState controlsPanel = new();

        private RetainedGraphCanvas<DialogNode, DialogCanvasConnection> toolkitCanvas;
        private DelegatingRetainedGraphCanvasAdapter<DialogNode, DialogCanvasConnection> canvasAdapter;
        private IMGUIContainer toolkitControls;

        private GraphEditorTheme Theme => theme ??= new GraphEditorTheme(() => useLightTheme);

        [MenuItem("Tools/Dialog Editor")]
        public static void Open()
        {
            GetWindow<DialogEditorWindow>("Dialog Editor");
        }

        private void CreateGUI()
        {
            canvasAdapter = CreateCanvasAdapter();
            toolkitCanvas = new RetainedGraphCanvas<DialogNode, DialogCanvasConnection>(
                canvasAdapter,
                canvasAdapter,
                canvasAdapter,
                canvasAdapter,
                canvasAdapter,
                canvasAdapter);
            toolkitControls = GraphEditorWindowUi.BuildRoot(
                rootVisualElement,
                toolkitCanvas,
                "dialog-editor-controls",
                OverlayPanelWidth,
                WindowBackgroundColor,
                DrawToolkitControls);

            UpdateControlsInputWidth();
            toolkitCanvas.RebuildNow();
        }

        private void OnEnable()
        {
            dialogsFolderPath = GraphEditorPreferences.LoadFolder(DialogsPathKey, "Assets/Dialogs");
            phrasesFolderPath = GraphEditorPreferences.LoadFolder(PhrasesPathKey, "Assets/DialogPhrases");
            useLightTheme = GraphEditorPreferences.LoadTheme(ThemeKey);
            GraphEditorAssetChangeTracker.AssetsChanged += HandleTrackedAssetsChanged;
        }

        private void OnDisable()
        {
            GraphEditorAssetChangeTracker.AssetsChanged -= HandleTrackedAssetsChanged;
            theme?.Dispose();
            theme = null;
            toolkitCanvas = null;
            canvasAdapter = null;
            toolkitControls = null;
        }

        private void DrawToolkitControls()
        {
            using (BeginThemedGuiScope())
            {
                DrawControlsOverlay();
            }
        }

        private void DrawToolkitNode(DialogNode node)
        {
            using (BeginThemedGuiScope())
            {
                DrawNodeWindow(node, false);
            }
        }

        private void RefreshToolkitCanvas(bool rebuild = false)
        {
            GraphEditorWindowUi.Refresh(
                rootVisualElement,
                toolkitCanvas,
                toolkitControls,
                WindowBackgroundColor,
                rebuild);
        }

        private void DrawEmptyState()
        {
            Rect contentRect = new Rect(12f, 12f, position.width - 24f, 52f);
            EditorGUI.DrawRect(contentRect, PanelBackgroundColor);
            GUI.Box(contentRect, GUIContent.none, HelpBoxStyle);
            EditorGUI.LabelField(
                new Rect(contentRect.x + 10f, contentRect.y + 10f, contentRect.width - 20f, 32f),
                "Create or load a dialog graph.");
        }

        private void DrawControlsOverlay()
        {
            const float toggleButtonWidth = 24f;
            const float toggleButtonHeight = 64f;
            const float spacing = 6f;
            const float padding = 10f;
            const float collapsedToggleLeftOffset = 6f;
            float panelHeight = Mathf.Max(120f, position.height);
            float panelY = position.height - panelHeight;
            float panelX = 0f;
            Rect panelRect = new Rect(panelX, panelY, OverlayPanelWidth, panelHeight);

            Rect toggleRect = controlsPanel.GetToggleRect(
                panelRect,
                toggleButtonWidth,
                toggleButtonHeight,
                collapsedToggleLeftOffset);

            if (!controlsPanel.IsExpanded)
            {
                if (DrawButton(toggleRect, ">"))
                {
                    SetControlsPanelExpanded(true);
                }

                return;
            }

            const float buttonHeight = 28f;
            float y = padding;

            EditorGUI.DrawRect(panelRect, PanelBackgroundColor);
            GUI.Box(panelRect, GUIContent.none, HelpBoxStyle);
            GUILayout.BeginArea(panelRect, GUIContent.none, HelpBoxStyle);
            float contentWidth = OverlayPanelWidth - padding * 2f;

            EditorGUI.LabelField(new Rect(padding, padding, contentWidth, 18f), "Dialogs Folder Path:");
            y += 18f;

            dialogsFolderPath = EditorGUI.TextField(new Rect(padding, y, contentWidth - 160f, 20f), dialogsFolderPath, TextFieldStyle);
            if (DrawButton(new Rect(padding + contentWidth - 155f, y, 70f, 20f), "Pick"))
            {
                PickFolder("Select folder for Dialogs", ref dialogsFolderPath, DialogsPathKey);
            }

            if (DrawButton(new Rect(padding + contentWidth - 80f, y, 70f, 20f), "Save"))
            {
                GraphEditorPreferences.SaveFolder(DialogsPathKey, dialogsFolderPath);
            }

            y += 28f;

            EditorGUI.LabelField(new Rect(padding, y, contentWidth, 18f), "Phrases Folder Path:");
            y += 18f;

            phrasesFolderPath = EditorGUI.TextField(new Rect(padding, y, contentWidth - 160f, 20f), phrasesFolderPath, TextFieldStyle);
            if (DrawButton(new Rect(padding + contentWidth - 155f, y, 70f, 20f), "Pick"))
            {
                PickFolder("Select folder for Dialog Phrases", ref phrasesFolderPath, PhrasesPathKey);
            }

            if (DrawButton(new Rect(padding + contentWidth - 80f, y, 70f, 20f), "Save"))
            {
                GraphEditorPreferences.SaveFolder(PhrasesPathKey, phrasesFolderPath);
            }

            y += 36f;

            if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), GetThemeToggleLabel()))
            {
                useLightTheme = !useLightTheme;
                GraphEditorPreferences.SaveTheme(ThemeKey, useLightTheme);
                RefreshToolkitCanvas();
                Repaint();
            }

            y += buttonHeight + spacing;

            if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), "New Dialog"))
            {
                CreateNewGraph();
            }

            y += buttonHeight + spacing;

            if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), "Load Dialog"))
            {
                LoadGraph();
            }

            y += buttonHeight + spacing;

            EditorGUI.BeginDisabledGroup(currentGraph == null);
            if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), "New Phrase"))
            {
                CreateNewPhrase();
            }
            EditorGUI.EndDisabledGroup();

            y += buttonHeight + spacing;

            if (currentGraph != null)
            {
                if (currentGraph.EntryPhrase == null)
                {
                    EditorGUI.HelpBox(
                        new Rect(padding, y, contentWidth, 40f),
                        "Entry phrase is not selected. The dialog will not work without it.",
                        MessageType.Warning);
                    y += 46f;
                }

                EditorGUI.BeginDisabledGroup(currentGraph.EntryPhrase == null);
                if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), "Ping Entry Phrase"))
                {
                    EditorGUIUtility.PingObject(currentGraph.EntryPhrase);
                    Selection.activeObject = currentGraph.EntryPhrase;
                }

                EditorGUI.EndDisabledGroup();
                y += buttonHeight + spacing;
                y += GraphEditorValidationGui.DrawSummary(
                    padding, y, contentWidth, DialogGraphValidator.Validate(currentGraph));
            }

            if (targetSelection.IsActive)
            {
                Color previousColor = GUI.backgroundColor;
                GUI.backgroundColor = DangerButtonColor;
                if (DrawButton(new Rect(padding, y, contentWidth, buttonHeight), "Cancel Selection"))
                {
                    CancelTargetSelection();
                }

                GUI.backgroundColor = previousColor;
            }

            GUILayout.EndArea();

            if (DrawButton(toggleRect, "<"))
            {
                SetControlsPanelExpanded(false);
            }
        }

        private void SetControlsPanelExpanded(bool isExpanded)
        {
            controlsPanel.SetExpanded(isExpanded, toolkitControls, OverlayPanelWidth);
        }

        private void UpdateControlsInputWidth()
        {
            controlsPanel.ApplyInputWidth(toolkitControls, OverlayPanelWidth);
        }

        private void PickFolder(string title, ref string folderPath, string prefsKey)
        {
            if (!GraphEditorAssetService.TryPickAssetsFolder(title, folderPath, out string selectedPath))
            {
                return;
            }

            folderPath = selectedPath;
            GraphEditorPreferences.SaveFolder(prefsKey, folderPath);
        }

        private void CreateNewGraph()
        {
            if (DialogGraphEditorCommands.TryCreateGraph(dialogsFolderPath, out DialogGraph graph))
            {
                currentGraph = graph;
                InvalidateGraphStructure();
            }
        }

        private void LoadGraph()
        {
            if (!GraphEditorAssetService.TryLoadAsset("Load Dialog Graph", out currentGraph))
            {
                return;
            }

            InvalidateGraphStructure();
        }

        private void CreateNewPhrase()
        {
            if (currentGraph == null)
            {
                EditorUtility.DisplayDialog(
                    "No Dialog Selected",
                    "Please create or load a dialog first.",
                    "OK");
                return;
            }

            if (DialogGraphEditorCommands.TryCreatePhrase(
                    currentGraph,
                    phrasesFolderPath,
                    GetCenteredNodePosition(new Vector2(DialogNodeWidth, 220f)),
                    out _))
            {
                MarkDirty(currentGraph);
                InvalidateGraphStructure();
            }
        }

        private void SynchronizeNodeRects()
        {
            if (nodeLayoutSynchronizer.Synchronize(
                    currentGraph.Nodes,
                    nodeRects,
                    node => node.Position,
                    new Vector2(DialogNodeWidth, 220f)))
            {
                InvalidateConnectionRouteCache();
            }
        }

        private void CleanupGraph()
        {
            if (currentGraph == null)
            {
                return;
            }

            bool graphChanged = DialogGraphStructureOperations.EnsureNodes(currentGraph);
            graphChanged |= DialogGraphStructureOperations.RemoveMissingNodes(currentGraph);

            if (graphChanged)
            {
                MarkDirty(currentGraph);
                InvalidateGraphCaches();
            }
        }

        private void RebuildGraphCaches()
        {
            graphIndex.Rebuild(currentGraph);
            InvalidateConnectionRouteCache();
        }

        private void InvalidateGraphCaches()
        {
            graphIndex.Invalidate();
            phraseDisplayNames.Clear();
            exitAbilityCache.Clear();
            phrasesWithDirtyLayout.Clear();
            phrasesAwaitingRepaintAfterLayout.Clear();
            InvalidateConnectionRouteCache();
            RefreshToolkitCanvas();
        }

        private void InvalidateGraphStructure()
        {
            graphStructureDirty = true;
            InvalidateGraphCaches();
            RefreshToolkitCanvas(true);
        }

        private void InvalidateConnectionRouteCache()
        {
            connectionPresenter.InvalidateRoutes();
        }

        private static void InvalidateStaticEditorCaches()
        {
            cachedStringTableCollections = null;
            cachedStringTableOptions = null;
            localizedEntryOptionsCache.Clear();
            cachedQuestGraphs = null;
            cachedQuestSourceNodes = null;
            cachedTerminalQuestNodes = null;
        }

        private void HandleTrackedAssetsChanged(IReadOnlyCollection<string> changedAssetPaths)
        {
            if (!HasRelevantAssetChange(changedAssetPaths))
            {
                return;
            }

            InvalidateStaticEditorCaches();
            bool graphAssetChanged = currentGraph != null &&
                changedAssetPaths.Contains(AssetDatabase.GetAssetPath(currentGraph));
            if (graphAssetChanged || HasMissingPhraseReference())
            {
                InvalidateGraphStructure();
                return;
            }

            InvalidateGraphCaches();
        }

        private bool HasRelevantAssetChange(IReadOnlyCollection<string> changedAssetPaths)
        {
            return GraphEditorAssetChangeFilter.HasRelevantChange(
                changedAssetPaths,
                currentGraph != null ? AssetDatabase.GetAssetPath(currentGraph) : null,
                dialogsFolderPath,
                phrasesFolderPath);
        }

        private bool HasMissingPhraseReference()
        {
            return currentGraph != null && currentGraph.Nodes.Any(node =>
                node == null || node.Phrase == null || !AssetDatabase.Contains(node.Phrase));
        }

        private static IEnumerable<DialogAnswer> GetConnectionAnswers(DialogPhrase phrase)
        {
            if (phrase == null)
            {
                yield break;
            }

            foreach (DialogAnswer answer in phrase.Answers)
            {
                yield return answer;
            }

            if (phrase.IsQuestPhrase)
            {
                yield return phrase.QuestAnswer;
            }

            if (phrase.IsConversationTopic)
            {
                yield return phrase.ConversationAnswer;
            }

            if (phrase.IsConversationReturnAction)
            {
                yield return phrase.ConversationReturnAnswer;
            }

            if (phrase.IsDialogueExitAction)
            {
                yield return phrase.DialogueExitAnswer;
            }
        }

        private IEnumerable<DialogPhrase> GetImplicitConversationReturnActions(DialogPhrase phrase)
        {
            if (currentGraph == null || phrase == null)
            {
                yield break;
            }

            foreach (DialogPhrase returnAction in currentGraph.GetConversationReturnPhrases(phrase))
            {
                yield return returnAction;
            }
        }

        private Color GetConnectionColor(DialogNode sourceNode, DialogNode targetNode)
        {
            return GraphEditorConnectionPresentation.GetColor(
                sourceNode,
                targetNode,
                activeConnectionNode,
                PrimaryConnectionColor,
                SourceHighlightConnectionColor,
                TargetHighlightConnectionColor);
        }

        private void DeleteNode(DialogNode node, bool exitGui = true)
        {
            if (activeConnectionNode == node)
            {
                activeConnectionNode = null;
            }

            bool shouldDeletePhraseAsset = node.Phrase != null &&
                                           EditorUtility.DisplayDialog(
                                               "Delete Phrase?",
                                               $"Do you want to delete the phrase \"{node.Phrase.name}\" from the project?",
                                               "Yes",
                                               "No");

            if (node.Phrase != null)
            {
                RemovePhraseReferences(node.Phrase);

                if (shouldDeletePhraseAsset)
                {
                    GraphEditorAssetService.DeleteAsset(node.Phrase, "Delete dialog phrase");
                }
            }

            GraphEditorAssetService.MarkDirty(currentGraph, "Delete dialog node");
            currentGraph.Nodes.Remove(node);
            MarkDirty(currentGraph);
            InvalidatePhraseDisplayName(node.Phrase);
            InvalidateGraphStructure();
            GraphEditorAssetService.FlushChanges();
            if (exitGui)
            {
                GUIUtility.ExitGUI();
            }
        }

        private void RemovePhraseReferences(DialogPhrase phrase)
        {
            foreach (DialogPhrase changedPhrase in DialogGraphReferenceOperations.RemoveIncomingReferences(currentGraph, phrase))
            {
                MarkDirty(changedPhrase);
                InvalidatePhraseDisplayName(changedPhrase);
            }

            if (targetSelection.SourcePhrase == phrase || targetSelection.PendingAnswer != null && targetSelection.PendingAnswer.NextPhrase == phrase)
            {
                CancelTargetSelection();
            }
        }

        private bool IsTargetPhraseSelectable(DialogPhrase phrase)
        {
            return targetSelection.IsActive &&
                   targetSelection.PendingAnswer != null &&
                   phrase != null &&
                   phrase != targetSelection.SourcePhrase;
        }

        private void ReplacePhraseReferences(DialogPhrase oldPhrase, DialogPhrase newPhrase)
        {
            if (oldPhrase == null || oldPhrase == newPhrase)
            {
                return;
            }

            foreach (DialogPhrase changedPhrase in DialogGraphReferenceOperations.ReplaceIncomingReferences(
                         currentGraph,
                         oldPhrase,
                         newPhrase))
            {
                MarkDirty(changedPhrase);
                InvalidatePhraseDisplayName(changedPhrase);
            }

            InvalidateGraphCaches();
        }

        private bool ContainsPhrase(DialogPhrase phrase)
        {
            return graphIndex.Contains(phrase);
        }

        private bool IsOrphanPhrase(DialogPhrase phrase)
        {
            return graphIndex.IsOrphan(phrase);
        }

        private Color GetNodeTint(DialogNode node)
        {
            if (node.Phrase == null)
            {
                return Color.white;
            }

            if (currentGraph.IsEntryPhrase(node.Phrase))
            {
                return StartNodeTint;
            }

            if (node.Phrase.IsQuestPhrase)
            {
                return QuestNodeTint;
            }

            if (node.Phrase.IsConversationTopic)
            {
                return new Color(0.72f, 0.86f, 0.76f);
            }

            if (node.Phrase.IsConversationReturnAction)
            {
                return new Color(0.76f, 0.82f, 0.94f);
            }

            if (node.Phrase.IsDialogueExitAction)
            {
                return new Color(0.94f, 0.76f, 0.76f);
            }

            if (IsOrphanPhrase(node.Phrase))
            {
                return OrphanNodeTint;
            }

            return Color.white;
        }

        private string GetNodeTitle(DialogNode node)
        {
            if (node.Phrase == null)
            {
                return "Phrase Node";
            }

            string prefix = string.Empty;
            if (currentGraph.IsEntryPhrase(node.Phrase))
            {
                prefix += "[Start] ";
            }

            if (node.Phrase.IsQuestPhrase)
            {
                prefix += "[Quest] ";
            }

            if (node.Phrase.IsConversationTopic)
            {
                prefix += "[Topic] ";
            }

            if (node.Phrase.IsConversationReturnAction)
            {
                prefix += "[Return] ";
            }

            if (node.Phrase.IsDialogueExitAction)
            {
                prefix += "[Exit] ";
            }

            return prefix + GetCachedPhraseDisplayName(node.Phrase);
        }

        private string GetCachedPhraseDisplayName(DialogPhrase phrase)
        {
            return phraseDisplayNames.Get(phrase);
        }

        private void InvalidatePhraseDisplayName(DialogPhrase phrase)
        {
            if (phrase != null)
            {
                phraseDisplayNames.Invalidate(phrase);
            }
        }

        private string GetPhraseDisplayName(DialogPhrase phrase)
        {
            return phraseDisplayNames.Get(phrase);
        }

        private void CancelTargetSelection(bool repaint = true)
        {
            targetSelection.Cancel();
            toolkitCanvas?.RefreshTargetSelection();

            if (repaint)
            {
                Repaint();
            }
        }

        private void MarkDirty(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            EditorUtility.SetDirty(target);
            InvalidateGraphCaches();
        }

        private void MarkNodePositionDirty()
        {
            if (currentGraph != null)
            {
                EditorUtility.SetDirty(currentGraph);
            }
        }

        private string GetThemeToggleLabel() => Theme.GetThemeToggleLabel();

        protected override void ApplyThemedGuiState() => Theme.ApplyGuiState();

        protected override void RestoreThemedGuiState() => Theme.RestoreGuiState();

        private void DrawWindowBackground() =>
            Theme.DrawWindowBackground(new Rect(0f, 0f, position.width, position.height));

        private bool DrawButton(Rect rect, string label) => GUI.Button(rect, label, ButtonStyle);

        private bool DrawMiniButton(Rect rect, string label) => GUI.Button(rect, label, MiniButtonStyle);

        private bool DrawButton(string label, params GUILayoutOption[] options) =>
            GUILayout.Button(label, ButtonStyle, options);

        private bool DrawMiniButton(string label, params GUILayoutOption[] options) =>
            GUILayout.Button(label, MiniButtonStyle, options);

        private GUIStyle NodeWindowStyle => Theme.NodeWindowStyle;
        private GUIStyle HelpBoxStyle => Theme.HelpBoxStyle;
        private GUIStyle ButtonStyle => Theme.ButtonStyle;
        private GUIStyle MiniButtonStyle => Theme.MiniButtonStyle;
        private GUIStyle PopupStyle => Theme.PopupStyle;
        private GUIStyle TextFieldStyle => Theme.TextFieldStyle;
        private GUIStyle LabelStyle => Theme.LabelStyle;
        private GUIStyle FoldoutStyle => Theme.FoldoutStyle;
        private GUIStyle BoldLabelStyle => Theme.BoldLabelStyle;
        private GUIStyle MiniBoldLabelStyle => Theme.MiniBoldLabelStyle;
        private GUIStyle MiniLabelStyle => Theme.MiniLabelStyle;
        private GUIStyle WordWrappedMiniLabelStyle => Theme.WordWrappedMiniLabelStyle;
        private GUIStyle CenteredMiniLabelStyle => Theme.CenteredMiniLabelStyle;
        private GUIStyle PreviewLabelStyle => Theme.PreviewLabelStyle;

        private void DrawEnumPropertyField(SerializedProperty property, string label)
        {
            if (property == null)
            {
                return;
            }

            if (property.propertyType != SerializedPropertyType.Enum)
            {
                DrawPropertyFieldWithCustomLabel(property, label);
                return;
            }

            int selectedIndex = DrawPopupField(label, property.enumValueIndex, property.enumDisplayNames);
            if (selectedIndex != property.enumValueIndex)
            {
                property.enumValueIndex = selectedIndex;
            }
        }

        private void DrawPropertyFieldWithCustomLabel(SerializedProperty property, string label, bool includeChildren = false)
        {
            if (property == null)
            {
                return;
            }

            float height = EditorGUI.GetPropertyHeight(property, includeChildren);
            Rect totalRect = EditorGUILayout.GetControlRect(true, height);
            Rect fieldRect = EditorGUI.PrefixLabel(totalRect, new GUIContent(label), LabelStyle);
            List<EditorStyleTextOverride> temporaryOverrides = null;

            if (useLightTheme &&
                Event.current.type == EventType.Repaint &&
                property.propertyType == SerializedPropertyType.ObjectReference)
            {
                temporaryOverrides = CreateTemporaryObjectFieldTextOverrides(Color.white);
            }

            try
            {
                EditorGUI.PropertyField(fieldRect, property, GUIContent.none, includeChildren);
            }
            finally
            {
                RestoreTemporaryStyleTextOverrides(temporaryOverrides);
            }
        }

        private int DrawPopupField(string label, int selectedIndex, string[] options)
        {
            Rect totalRect = EditorGUILayout.GetControlRect();
            Rect fieldRect = EditorGUI.PrefixLabel(totalRect, new GUIContent(label), LabelStyle);
            return EditorGUI.Popup(fieldRect, selectedIndex, options, PopupStyle);
        }

        private static void SetStyleTextColor(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        private List<EditorStyleTextOverride> CreateTemporaryObjectFieldTextOverrides(Color textColor)
        {
            var overrides = new List<EditorStyleTextOverride>(7);
            AddTemporaryStyleTextOverride(overrides, EditorStyles.objectField, textColor);
            AddTemporaryStyleTextOverride(overrides, EditorStyles.objectFieldThumb, textColor);
            AddTemporarySkinStyleTextOverride(overrides, GUI.skin, "ObjectField", textColor);
            AddTemporarySkinStyleTextOverride(overrides, GUI.skin, "ObjectFieldButton", textColor);
            AddTemporarySkinStyleTextOverride(overrides, GUI.skin, "ObjectFieldThumb", textColor);
            AddTemporarySkinStyleTextOverride(overrides, GUI.skin, "IN ObjectField", textColor);
            AddTemporarySkinStyleTextOverride(overrides, GUI.skin, "IN ObjectFieldText", textColor);
            return overrides;
        }

        private static void AddTemporarySkinStyleTextOverride(
            List<EditorStyleTextOverride> overrides,
            GUISkin skin,
            string styleName,
            Color textColor)
        {
            if (skin == null)
            {
                return;
            }

            AddTemporaryStyleTextOverride(overrides, skin.FindStyle(styleName), textColor);
        }

        private static void AddTemporaryStyleTextOverride(
            List<EditorStyleTextOverride> overrides,
            GUIStyle style,
            Color textColor)
        {
            if (style == null)
            {
                return;
            }

            overrides.Add(new EditorStyleTextOverride(style));
            SetStyleTextColor(style, textColor);
        }

        private static void RestoreTemporaryStyleTextOverrides(List<EditorStyleTextOverride> overrides)
        {
            if (overrides == null)
            {
                return;
            }

            for (int i = overrides.Count - 1; i >= 0; i--)
            {
                overrides[i].Restore();
            }
        }

        private readonly struct EditorStyleTextOverride
        {
            private readonly GUIStyle style;
            private readonly Color normal;
            private readonly Color hover;
            private readonly Color active;
            private readonly Color focused;
            private readonly Color onNormal;
            private readonly Color onHover;
            private readonly Color onActive;
            private readonly Color onFocused;

            public EditorStyleTextOverride(GUIStyle style)
            {
                this.style = style;
                normal = style.normal.textColor;
                hover = style.hover.textColor;
                active = style.active.textColor;
                focused = style.focused.textColor;
                onNormal = style.onNormal.textColor;
                onHover = style.onHover.textColor;
                onActive = style.onActive.textColor;
                onFocused = style.onFocused.textColor;
            }

            public void Restore()
            {
                style.normal.textColor = normal;
                style.hover.textColor = hover;
                style.active.textColor = active;
                style.focused.textColor = focused;
                style.onNormal.textColor = onNormal;
                style.onHover.textColor = onHover;
                style.onActive.textColor = onActive;
                style.onFocused.textColor = onFocused;
            }
        }

        private Color PanelBackgroundColor => Theme.PanelBackgroundColor;
        private Color CanvasBackgroundColor => Theme.CanvasBackgroundColor;
        private Color MinorGridColor => Theme.MinorGridColor;
        private Color MajorGridColor => Theme.MajorGridColor;
        private Color PrimaryConnectionColor => Theme.PrimaryConnectionColor;
        private Color SourceHighlightConnectionColor => Theme.SourceHighlightConnectionColor;
        private Color TargetHighlightConnectionColor => Theme.TargetHighlightConnectionColor;
        private Color WindowBackgroundColor => Theme.WindowBackgroundColor;
        private Color DangerButtonColor => Theme.DangerButtonColor;
        private Color LinkButtonColor => Theme.LinkButtonColor;
        private Color StartBadgeColor => Theme.StartBadgeColor;
        private Color WarningBadgeColor => Theme.WarningBadgeColor;
        private Color StartNodeTint => Theme.StartNodeTint;
        private Color QuestNodeTint => Theme.QuestNodeTint;
        private Color OrphanNodeTint => Theme.OrphanNodeTint;
        private Color SelectionOverlayTextColor => Theme.SelectionOverlayTextColor;
        private Color DangerAccentColor => Theme.DangerAccentColor;
        private Color WarningAccentColor => Theme.WarningAccentColor;
        private Color ConditionAccentColor => Theme.ConditionAccentColor;
        private Color RewardAccentColor => Theme.RewardAccentColor;
        private Color ItemAccentColor => Theme.ItemAccentColor;
        private Color StrongDividerColor => Theme.StrongDividerColor;
        private Color SectionDividerColor => Theme.SectionDividerColor;
        private Color SoftDividerColor => Theme.SoftDividerColor;
        private Color SoftestDividerColor => Theme.SoftestDividerColor;
        private Color GetSelectionOverlayColor(bool isHovered) => Theme.GetSelectionOverlayColor(isHovered);
    }
}
