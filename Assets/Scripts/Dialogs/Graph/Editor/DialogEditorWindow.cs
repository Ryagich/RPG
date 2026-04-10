using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dialogs.Graph.Model;
using UnityEditor;
using UnityEngine;

namespace Dialogs.Graph.Editor
{
    public class DialogEditorWindow : EditorWindow
    {
        private const string DialogsPathKey = "DialogEditor_DialogsPath";
        private const string PhrasesPathKey = "DialogEditor_PhrasesPath";
        private const float WorkspaceWidth = 10000f;
        private const float WorkspaceHeight = 10000f;
        private const float ZoomMin = 0.25f;
        private const float ZoomMax = 2f;

        private DialogGraph currentGraph;
        private Vector2 scrollPos;
        private string dialogsFolderPath;
        private string phrasesFolderPath;
        private readonly Dictionary<DialogAnswer, Vector2> answerAnchorPositions = new();
        private readonly Dictionary<DialogNode, Rect> nodeRects = new();

        private bool isSelectingTargetPhrase;
        private DialogAnswer pendingAnswer;
        private DialogPhrase sourcePhraseForSelection;

        private float zoom = 1f;
        private Vector2 panOffset = Vector2.zero;

        [MenuItem("Tools/Dialog Editor")]
        public static void Open()
        {
            GetWindow<DialogEditorWindow>("Dialog Editor");
        }

        private void OnEnable()
        {
            dialogsFolderPath = EditorPrefs.GetString(DialogsPathKey, "Assets/Dialogs");
            phrasesFolderPath = EditorPrefs.GetString(PhrasesPathKey, "Assets/DialogPhrases");
        }

        private void OnGUI()
        {
            DrawMenuButtons();

            if (currentGraph == null)
            {
                EditorGUILayout.HelpBox("Create or load a dialog graph.", MessageType.Info);
                return;
            }

            if (currentGraph.EntryPhrase == null)
            {
                EditorGUILayout.HelpBox(
                    "Entry phrase is not selected. The dialog will not work without it.",
                    MessageType.Warning);
            }

            DrawGraphArea();
        }

        private void DrawMenuButtons()
        {
            const float panelWidth = 320f;
            const float buttonHeight = 28f;
            const float spacing = 6f;
            const float x = 10f;
            float y = 10f;

            EditorGUI.LabelField(new Rect(x, y, panelWidth, 18f), "Dialogs Folder Path:");
            y += 18f;

            dialogsFolderPath = EditorGUI.TextField(new Rect(x, y, panelWidth - 170f, 20f), dialogsFolderPath);
            if (GUI.Button(new Rect(x + panelWidth - 165f, y, 75f, 20f), "Pick"))
            {
                PickFolder("Select folder for Dialogs", ref dialogsFolderPath, DialogsPathKey);
            }

            if (GUI.Button(new Rect(x + panelWidth - 85f, y, 75f, 20f), "Save"))
            {
                EditorPrefs.SetString(DialogsPathKey, dialogsFolderPath);
            }

            y += 28f;

            EditorGUI.LabelField(new Rect(x, y, panelWidth, 18f), "Phrases Folder Path:");
            y += 18f;

            phrasesFolderPath = EditorGUI.TextField(new Rect(x, y, panelWidth - 170f, 20f), phrasesFolderPath);
            if (GUI.Button(new Rect(x + panelWidth - 165f, y, 75f, 20f), "Pick"))
            {
                PickFolder("Select folder for Dialog Phrases", ref phrasesFolderPath, PhrasesPathKey);
            }

            if (GUI.Button(new Rect(x + panelWidth - 85f, y, 75f, 20f), "Save"))
            {
                EditorPrefs.SetString(PhrasesPathKey, phrasesFolderPath);
            }

            y += 36f;

            if (GUI.Button(new Rect(x, y, panelWidth, buttonHeight), "New Dialog"))
            {
                CreateNewGraph();
            }

            y += buttonHeight + spacing;

            if (GUI.Button(new Rect(x, y, panelWidth, buttonHeight), "Load Dialog"))
            {
                LoadGraph();
            }

            y += buttonHeight + spacing;

            if (GUI.Button(new Rect(x, y, panelWidth, buttonHeight), "New Phrase"))
            {
                CreateNewPhrase();
            }

            y += buttonHeight + spacing;

            if (currentGraph != null)
            {
                EditorGUI.BeginDisabledGroup(currentGraph.EntryPhrase == null);
                if (GUI.Button(new Rect(x, y, panelWidth, buttonHeight), "Ping Entry Phrase"))
                {
                    EditorGUIUtility.PingObject(currentGraph.EntryPhrase);
                    Selection.activeObject = currentGraph.EntryPhrase;
                }

                EditorGUI.EndDisabledGroup();
                y += buttonHeight + spacing;
            }

            if (isSelectingTargetPhrase)
            {
                Color previousColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUI.Button(new Rect(x, y, panelWidth, buttonHeight), "Cancel Selection"))
                {
                    CancelTargetSelection();
                }

                GUI.backgroundColor = previousColor;
            }
        }

        private void PickFolder(string title, ref string folderPath, string prefsKey)
        {
            string selected = EditorUtility.OpenFolderPanel(title, "Assets", "");
            if (string.IsNullOrEmpty(selected))
            {
                return;
            }

            if (!selected.StartsWith(Application.dataPath))
            {
                EditorUtility.DisplayDialog(
                    "Invalid Folder",
                    "Please select a folder inside your Assets directory.",
                    "OK");
                return;
            }

            folderPath = "Assets" + selected.Substring(Application.dataPath.Length);
            EditorPrefs.SetString(prefsKey, folderPath);
        }

        private void CreateNewGraph()
        {
            if (!EnsureFolderExists(dialogsFolderPath, "Please specify the folder for saving dialogs."))
            {
                return;
            }

            string targetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(dialogsFolderPath, "DialogGraph.asset"));
            currentGraph = CreateInstance<DialogGraph>();
            currentGraph.name = Path.GetFileNameWithoutExtension(targetPath);

            AssetDatabase.CreateAsset(currentGraph, targetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(currentGraph);
            Selection.activeObject = currentGraph;
        }

        private void LoadGraph()
        {
            string path = EditorUtility.OpenFilePanel("Load Dialog Graph", "Assets", "asset");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            path = "Assets" + path.Replace(Application.dataPath, "");
            currentGraph = AssetDatabase.LoadAssetAtPath<DialogGraph>(path);
            if (currentGraph == null)
            {
                EditorUtility.DisplayDialog("Invalid Asset", "Selected asset is not a DialogGraph.", "OK");
            }
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

            if (!EnsureFolderExists(phrasesFolderPath, "Please specify the folder for saving phrases."))
            {
                return;
            }

            string fileName = $"DialogPhrase_{currentGraph.Nodes.Count}.asset";
            string targetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(phrasesFolderPath, fileName));

            var phrase = CreateInstance<DialogPhrase>();
            phrase.name = Path.GetFileNameWithoutExtension(targetPath);

            AssetDatabase.CreateAsset(phrase, targetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var newNode = new DialogNode(phrase)
            {
                Position = GetCenteredNodePosition(new Vector2(320f, 220f))
            };

            currentGraph.Nodes.Add(newNode);
            MarkDirty(currentGraph);

            EditorGUIUtility.PingObject(phrase);
            Selection.activeObject = phrase;
        }

        private bool EnsureFolderExists(string folderPath, string emptyPathMessage)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                EditorUtility.DisplayDialog("Path not set", emptyPathMessage, "OK");
                return false;
            }

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            return true;
        }

        private Vector2 GetCenteredNodePosition(Vector2 nodeSize)
        {
            Vector2 screenCenter = new Vector2(position.width / 2f, position.height / 2f);
            Vector2 graphCenter = (screenCenter - panOffset) / zoom;
            graphCenter.y += 120f;
            return graphCenter - nodeSize * 0.5f;
        }

        private void DrawGraphArea()
        {
            CleanupGraph();
            answerAnchorPositions.Clear();
            nodeRects.Clear();

            Event currentEvent = Event.current;
            HandleZoom(currentEvent);
            HandlePan(currentEvent);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
            GUI.EndClip();
            GUI.EndClip();
            GUI.BeginClip(new Rect(Vector2.zero, new Vector2(WorkspaceWidth, WorkspaceHeight)));
            GUI.BeginClip(new Rect(Vector2.zero, new Vector2(WorkspaceWidth, WorkspaceHeight)));

            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(panOffset, Quaternion.identity, Vector3.one * zoom);

            DrawBackgroundGrid(new Rect(0f, 0f, WorkspaceWidth, WorkspaceHeight));

            EditorGUI.BeginDisabledGroup(isSelectingTargetPhrase);
            BeginWindows();

            for (int i = 0; i < currentGraph.Nodes.Count; i++)
            {
                DialogNode node = currentGraph.Nodes[i];
                Rect rect = new Rect(node.Position, new Vector2(320f, 220f));

                Color previousColor = GUI.color;
                GUI.color = GetNodeTint(node);
                rect = GUILayout.Window(i, rect, _ => DrawNodeWindow(node), GetNodeTitle(node));
                GUI.color = previousColor;

                nodeRects[node] = rect;
                node.Position = rect.position;
            }

            EndWindows();
            EditorGUI.EndDisabledGroup();

            DrawNodeMarkers();
            DrawConnections();
            DrawTargetSelectionOverlay();

            GUI.matrix = oldMatrix;
            EditorGUILayout.EndScrollView();
        }

        private void CleanupGraph()
        {
            if (currentGraph == null)
            {
                return;
            }

            bool graphChanged = false;

            for (int i = currentGraph.Nodes.Count - 1; i >= 0; i--)
            {
                DialogNode node = currentGraph.Nodes[i];
                if (node == null)
                {
                    currentGraph.Nodes.RemoveAt(i);
                    graphChanged = true;
                    continue;
                }

                if (node.Phrase == null || !AssetDatabase.Contains(node.Phrase))
                {
                    if (currentGraph.IsEntryPhrase(node.Phrase))
                    {
                        currentGraph.SetEntryPhrase(null);
                    }

                    currentGraph.Nodes.RemoveAt(i);
                    graphChanged = true;
                }
            }

            if (graphChanged)
            {
                MarkDirty(currentGraph);
            }
        }

        private void HandleZoom(Event currentEvent)
        {
            if (currentEvent.type != EventType.ScrollWheel)
            {
                return;
            }

            float zoomDelta = -currentEvent.delta.y * 0.05f;
            float oldZoom = zoom;
            zoom = Mathf.Clamp(zoom + zoomDelta, ZoomMin, ZoomMax);

            Vector2 windowCenter = new Vector2(position.width / 2f, position.height / 2f);
            panOffset = (panOffset - windowCenter) * (zoom / oldZoom) + windowCenter;

            ClampPanToWorkspace(WorkspaceWidth, WorkspaceHeight);
            currentEvent.Use();
        }

        private void HandlePan(Event currentEvent)
        {
            if (currentEvent.type != EventType.MouseDrag || currentEvent.button != 1)
            {
                return;
            }

            panOffset += currentEvent.delta;
            ClampPanToWorkspace(WorkspaceWidth, WorkspaceHeight);
            currentEvent.Use();
            Repaint();
        }

        private void DrawNodeMarkers()
        {
            foreach (DialogNode node in currentGraph.Nodes)
            {
                if (node.Phrase == null)
                {
                    continue;
                }

                Rect badgeRect = new Rect(node.Position.x + 6f, node.Position.y + 6f, 18f, 18f);
                Color previous = GUI.backgroundColor;

                if (currentGraph.IsEntryPhrase(node.Phrase))
                {
                    GUI.backgroundColor = new Color(0.2f, 0.7f, 0.25f);
                    GUI.Box(badgeRect, "S");
                }
                else if (IsOrphanPhrase(node.Phrase))
                {
                    GUI.backgroundColor = new Color(1f, 0.6f, 0.15f);
                    GUI.Box(badgeRect, "!");
                }

                GUI.backgroundColor = previous;
            }
        }

        private void DrawConnections()
        {
            Handles.BeginGUI();

            foreach (DialogNode node in currentGraph.Nodes)
            {
                DialogPhrase phrase = node.Phrase;
                if (phrase == null)
                {
                    continue;
                }

                foreach (DialogAnswer answer in phrase.Answers)
                {
                    if (answer == null || answer.NextPhrase == null)
                    {
                        continue;
                    }

                    DialogNode targetNode = currentGraph.Nodes.FirstOrDefault(n => n.Phrase == answer.NextPhrase);
                    if (targetNode == null)
                    {
                        continue;
                    }

                    if (!nodeRects.TryGetValue(node, out Rect sourceRect) ||
                        !nodeRects.TryGetValue(targetNode, out Rect targetRect))
                    {
                        continue;
                    }

                    Vector2 startPos;
                    if (!answerAnchorPositions.TryGetValue(answer, out startPos))
                    {
                        startPos = new Vector2(sourceRect.xMax - 12f, sourceRect.center.y);
                    }

                    Vector2 endPos = new Vector2(targetRect.x + 10f, targetRect.y + 14f);
                    Vector2 startTangent = startPos + Vector2.right * 50f;
                    Vector2 endTangent = endPos + Vector2.left * 50f;

                    Handles.color = new Color(0.9f, 0.8f, 0.2f, 0.95f);
                    Handles.DrawBezier(startPos, endPos, startTangent, endTangent, Handles.color, null, 2f);

                    Vector2 direction = (endPos - startPos).normalized;
                    Vector2 perpendicular = new Vector2(-direction.y, direction.x);
                    Vector2 arrowTip = endPos;
                    Vector2 arrowBase1 = endPos - direction * 10f + perpendicular * 4f;
                    Vector2 arrowBase2 = endPos - direction * 10f - perpendicular * 4f;
                    Handles.DrawAAConvexPolygon(arrowTip, arrowBase1, arrowBase2);
                }
            }

            Handles.EndGUI();
        }

        private void DrawTargetSelectionOverlay()
        {
            if (!isSelectingTargetPhrase || pendingAnswer == null)
            {
                return;
            }

            Handles.BeginGUI();

            foreach (DialogNode node in currentGraph.Nodes)
            {
                if (node.Phrase == null || node.Phrase == sourcePhraseForSelection)
                {
                    continue;
                }

                if (!nodeRects.TryGetValue(node, out Rect rect))
                {
                    continue;
                }

                bool isHovered = rect.Contains(Event.current.mousePosition);
                EditorGUI.DrawRect(rect, new Color(0f, 0.75f, 0.2f, isHovered ? 0.45f : 0.25f));

                GUIStyle style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 52,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };

                GUI.Label(rect, "+", style);

                if ((Event.current.rawType == EventType.MouseDown || Event.current.rawType == EventType.MouseUp) &&
                    rect.Contains(Event.current.mousePosition))
                {
                    pendingAnswer.SetNextPhrase(node.Phrase);
                    MarkDirty(sourcePhraseForSelection);
                    CancelTargetSelection(false);
                    Event.current.Use();
                    GUIUtility.ExitGUI();
                    return;
                }
            }

            Handles.EndGUI();
        }

        private void ClampPanToWorkspace(float workspaceWidth, float workspaceHeight)
        {
            float viewWidth = position.width;
            float viewHeight = position.height;

            float minX = viewWidth - workspaceWidth * zoom;
            float maxX = 0f;
            float minY = viewHeight - workspaceHeight * zoom;
            float maxY = 0f;

            if (workspaceWidth * zoom <= viewWidth)
            {
                panOffset.x = Mathf.Round((viewWidth - workspaceWidth * zoom) * 0.5f);
            }
            else
            {
                panOffset.x = Mathf.Clamp(panOffset.x, minX, maxX);
            }

            if (workspaceHeight * zoom <= viewHeight)
            {
                panOffset.y = Mathf.Round((viewHeight - workspaceHeight * zoom) * 0.5f);
            }
            else
            {
                panOffset.y = Mathf.Clamp(panOffset.y, minY, maxY);
            }
        }

        private void DrawNodeWindow(DialogNode node)
        {
            EditorGUI.BeginDisabledGroup(isSelectingTargetPhrase);

            Rect removeButtonRect = new Rect(298f, 5f, 16f, 16f);
            if (GUI.Button(removeButtonRect, "x"))
            {
                DeleteNode(node);
                EditorGUI.EndDisabledGroup();
                return;
            }

            EditorGUI.BeginChangeCheck();
            var newPhrase = (DialogPhrase)EditorGUILayout.ObjectField(node.Phrase, typeof(DialogPhrase), false);
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

                    ReplacePhraseReferences(node.Phrase, newPhrase);
                    node.Phrase = newPhrase;
                    MarkDirty(currentGraph);
                }
            }

            if (node.Phrase == null)
            {
                EditorGUILayout.HelpBox("No phrase assigned.", MessageType.Warning);
                GUI.DragWindow();
                EditorGUI.EndDisabledGroup();
                return;
            }

            DrawPhraseEditor(node.Phrase);

            if (GUILayout.Button(currentGraph.IsEntryPhrase(node.Phrase) ? "Start Phrase" : "Set As Start"))
            {
                currentGraph.SetEntryPhrase(node.Phrase);
                MarkDirty(currentGraph);
            }

            if (IsOrphanPhrase(node.Phrase))
            {
                EditorGUILayout.HelpBox(
                    "This phrase has no incoming answers and is not the entry phrase.",
                    MessageType.Warning);
            }

            EditorGUI.EndDisabledGroup();
            GUI.DragWindow();
        }

        private void DrawPhraseEditor(DialogPhrase phrase)
        {
            SerializedObject phraseObject = new SerializedObject(phrase);
            phraseObject.Update();

            SerializedProperty textProperty = FindAutoProperty(phraseObject, "Text");
            SerializedProperty answersProperty = FindAutoProperty(phraseObject, "Answers");

            if (answersProperty == null)
            {
                EditorGUILayout.HelpBox("Answers property was not found.", MessageType.Error);
                return;
            }

            EditorGUI.BeginChangeCheck();

            if (textProperty != null)
            {
                EditorGUILayout.PropertyField(textProperty, new GUIContent("Phrase"));
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Answers", EditorStyles.miniBoldLabel);

            int removeAnswerIndex = -1;
            DialogNode ownerNode = currentGraph.Nodes.FirstOrDefault(n => n.Phrase == phrase);

            for (int i = 0; i < answersProperty.arraySize; i++)
            {
                SerializedProperty answerProperty = answersProperty.GetArrayElementAtIndex(i);
                SerializedProperty answerTextProperty = FindAutoProperty(answerProperty, "Text");
                SerializedProperty nextPhraseProperty = FindAutoProperty(answerProperty, "NextPhrase");

                bool missingLink = nextPhraseProperty.objectReferenceValue == null;
                bool targetOutsideGraph = nextPhraseProperty.objectReferenceValue != null &&
                                         !ContainsPhrase((DialogPhrase)nextPhraseProperty.objectReferenceValue);

                Color previousColor = GUI.color;
                if (missingLink)
                {
                    GUI.color = new Color(1f, 0.75f, 0.75f);
                }
                else if (targetOutsideGraph)
                {
                    GUI.color = new Color(1f, 0.9f, 0.65f);
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.color = previousColor;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Answer {i + 1}", EditorStyles.miniBoldLabel);

                if (GUILayout.Button("X", GUILayout.Width(22f)))
                {
                    removeAnswerIndex = i;
                }

                Color previousBackground = GUI.backgroundColor;
                GUI.backgroundColor = missingLink ? new Color(1f, 0.4f, 0.4f) : new Color(1f, 0.7f, 0.2f);
                bool pickPressed = GUILayout.Button("O", GUILayout.Width(22f));
                GUI.backgroundColor = previousBackground;

                Rect localButtonRect = GUILayoutUtility.GetLastRect();
                if (ownerNode != null && nodeRects.TryGetValue(ownerNode, out Rect nodeRect) && i < phrase.Answers.Count)
                {
                    Vector2 localCenter = new Vector2(
                        localButtonRect.x + localButtonRect.width * 0.5f,
                        localButtonRect.y + localButtonRect.height * 0.5f);
                    answerAnchorPositions[phrase.Answers[i]] = nodeRect.position + localCenter;
                }

                if (pickPressed && i < phrase.Answers.Count)
                {
                    isSelectingTargetPhrase = true;
                    pendingAnswer = phrase.Answers[i];
                    sourcePhraseForSelection = phrase;
                }

                EditorGUILayout.EndHorizontal();

                if (answerTextProperty != null)
                {
                    EditorGUILayout.PropertyField(answerTextProperty, new GUIContent("Text"));
                }

                if (nextPhraseProperty != null)
                {
                    EditorGUILayout.PropertyField(nextPhraseProperty, new GUIContent("Next Phrase"));
                }

                if (missingLink)
                {
                    EditorGUILayout.HelpBox("Next phrase is not assigned for this answer.", MessageType.Error);
                }
                else if (targetOutsideGraph)
                {
                    EditorGUILayout.HelpBox("Target phrase is not added to the current dialog graph.", MessageType.Warning);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2f);
            }

            if (GUILayout.Button("+ Add Answer"))
            {
                answersProperty.arraySize++;
            }

            if (removeAnswerIndex >= 0)
            {
                answersProperty.DeleteArrayElementAtIndex(removeAnswerIndex);
            }

            if (EditorGUI.EndChangeCheck())
            {
                phraseObject.ApplyModifiedProperties();
                MarkDirty(phrase);
            }
            else
            {
                phraseObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private void DeleteNode(DialogNode node)
        {
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
                    string phrasePath = AssetDatabase.GetAssetPath(node.Phrase);
                    if (!string.IsNullOrEmpty(phrasePath))
                    {
                        AssetDatabase.DeleteAsset(phrasePath);
                    }
                }
            }

            currentGraph.Nodes.Remove(node);
            MarkDirty(currentGraph);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GUIUtility.ExitGUI();
        }

        private void RemovePhraseReferences(DialogPhrase phrase)
        {
            if (currentGraph.IsEntryPhrase(phrase))
            {
                currentGraph.SetEntryPhrase(null);
            }

            foreach (DialogNode otherNode in currentGraph.Nodes)
            {
                if (otherNode.Phrase == null)
                {
                    continue;
                }

                bool phraseChanged = false;
                foreach (DialogAnswer answer in otherNode.Phrase.Answers)
                {
                    if (answer != null && answer.NextPhrase == phrase)
                    {
                        answer.SetNextPhrase(null);
                        phraseChanged = true;
                    }
                }

                if (phraseChanged)
                {
                    MarkDirty(otherNode.Phrase);
                }
            }

            if (sourcePhraseForSelection == phrase || pendingAnswer != null && pendingAnswer.NextPhrase == phrase)
            {
                CancelTargetSelection();
            }
        }

        private void ReplacePhraseReferences(DialogPhrase oldPhrase, DialogPhrase newPhrase)
        {
            if (oldPhrase == null || oldPhrase == newPhrase)
            {
                return;
            }

            foreach (DialogNode node in currentGraph.Nodes)
            {
                if (node.Phrase == null)
                {
                    continue;
                }

                bool phraseChanged = false;
                foreach (DialogAnswer answer in node.Phrase.Answers)
                {
                    if (answer != null && answer.NextPhrase == oldPhrase)
                    {
                        answer.SetNextPhrase(newPhrase);
                        phraseChanged = true;
                    }
                }

                if (phraseChanged)
                {
                    MarkDirty(node.Phrase);
                }
            }
        }

        private bool ContainsPhrase(DialogPhrase phrase)
        {
            return currentGraph.Nodes.Any(node => node.Phrase == phrase);
        }

        private bool IsOrphanPhrase(DialogPhrase phrase)
        {
            if (phrase == null || currentGraph.IsEntryPhrase(phrase))
            {
                return false;
            }

            return !currentGraph.Nodes
                .Where(node => node.Phrase != null)
                .SelectMany(node => node.Phrase.Answers)
                .Any(answer => answer != null && answer.NextPhrase == phrase);
        }

        private Color GetNodeTint(DialogNode node)
        {
            if (node.Phrase == null)
            {
                return Color.white;
            }

            if (currentGraph.IsEntryPhrase(node.Phrase))
            {
                return new Color(0.82f, 1f, 0.82f);
            }

            if (IsOrphanPhrase(node.Phrase))
            {
                return new Color(1f, 0.92f, 0.72f);
            }

            return Color.white;
        }

        private string GetNodeTitle(DialogNode node)
        {
            if (node.Phrase == null)
            {
                return "Phrase Node";
            }

            string prefix = currentGraph.IsEntryPhrase(node.Phrase) ? "[Start] " : string.Empty;
            return prefix + node.Phrase.name;
        }

        private void CancelTargetSelection(bool repaint = true)
        {
            isSelectingTargetPhrase = false;
            pendingAnswer = null;
            sourcePhraseForSelection = null;

            if (repaint)
            {
                Repaint();
            }
        }

        private static SerializedProperty FindAutoProperty(SerializedObject serializedObject, string propertyName)
        {
            return serializedObject.FindProperty($"<{propertyName}>k__BackingField");
        }

        private static SerializedProperty FindAutoProperty(SerializedProperty parentProperty, string propertyName)
        {
            return parentProperty.FindPropertyRelative($"<{propertyName}>k__BackingField");
        }

        private static void MarkDirty(Object target)
        {
            if (target == null)
            {
                return;
            }

            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
        }

        private void DrawBackgroundGrid(Rect rect)
        {
            Color minorColor = new Color(0.25f, 0.25f, 0.25f, 0.35f);
            Color majorColor = new Color(0.25f, 0.25f, 0.25f, 0.6f);

            float gridSpacing = 20f * zoom;
            float majorStep = gridSpacing * 5f;
            Vector2 offset = new Vector2(panOffset.x % gridSpacing, panOffset.y % gridSpacing);

            Handles.BeginGUI();

            Handles.color = minorColor;
            for (float x = rect.xMin + offset.x; x < rect.xMax; x += gridSpacing)
            {
                Handles.DrawLine(new Vector3(x, rect.yMin, 0f), new Vector3(x, rect.yMax, 0f));
            }

            for (float y = rect.yMin + offset.y; y < rect.yMax; y += gridSpacing)
            {
                Handles.DrawLine(new Vector3(rect.xMin, y, 0f), new Vector3(rect.xMax, y, 0f));
            }

            Handles.color = majorColor;
            for (float x = rect.xMin + offset.x; x < rect.xMax; x += majorStep)
            {
                Handles.DrawLine(new Vector3(x, rect.yMin, 0f), new Vector3(x, rect.yMax, 0f));
            }

            for (float y = rect.yMin + offset.y; y < rect.yMax; y += majorStep)
            {
                Handles.DrawLine(new Vector3(rect.xMin, y, 0f), new Vector3(rect.xMax, y, 0f));
            }

            Handles.EndGUI();
        }
    }
}
