using System.Collections.Generic;
using System.IO;
using System.Linq;
using StateMachine.Graph.Model;
using UnityEditor;
using UnityEngine;

namespace StateMachine.Graph.Editor
{
    public class StateMachineEditorWindow : EditorWindow
    {
        private StateMachineGraph currentGraph;
        private Vector2 scrollPos;

        // === Ключи для сохранения путей ===
        private const string STATE_PATH_KEY = "StateMachineEditor_StatesPath";
        private const string TRANSITION_PATH_KEY = "StateMachineEditor_TransitionsPath";

        // === Кэш путей ===
        private string statesFolderPath;
        private string transitionsFolderPath;
        private readonly Dictionary<Transition, Vector2> transitionAnchorPositions = new();

        // === Режим выбора целевого узла ===
        private bool isSelectingTargetNode;
        private Transition pendingTransition;
        private Node sourceNodeForSelection;

        // Для сохранения позиций окон
        private readonly Dictionary<Node, Rect> nodeRects = new();

        // === Навигация по графу ===
        private float zoom = 1f;
        private Vector2 panOffset = Vector2.zero;
        private const float zoomMin = 0.25f;
        private const float zoomMax = 2.0f;

        [MenuItem("Tools/State Machine Editor")]
        public static void Open()
        {
            GetWindow<StateMachineEditorWindow>("State Machine Editor");
        }

        private void OnEnable()
        {
            // Загружаем пути из EditorPrefs при открытии окна
            statesFolderPath = EditorPrefs.GetString(STATE_PATH_KEY, "Assets/States");
            transitionsFolderPath = EditorPrefs.GetString(TRANSITION_PATH_KEY, "Assets/Transitions");
        }

        private void OnGUI()
        {
            DrawMenuButtons();

            // Если граф не выбран, показываем сообщение
            if (currentGraph == null)
            {
                EditorGUILayout.HelpBox("Выбери или создай граф состояния", MessageType.Info);
                return;
            }

            DrawGraphArea();
        }

        private void DrawMenuButtons()
        {
            float panelWidth = 320f;
            float buttonHeight = 28f;
            float spacing = 6f;
            float x = 10f;
            float y = 10f;

            // === STATES PATH ===
            EditorGUI.LabelField(new Rect(x, y, panelWidth, 18f), "📁 States Folder Path:");
            y += 18f;

            statesFolderPath = EditorGUI.TextField(new Rect(x, y, panelWidth - 170, 20f), statesFolderPath);

            // Pick Folder
            if (GUI.Button(new Rect(x + panelWidth - 165, y, 75, 20f), "📂 Pick"))
            {
                string selected = EditorUtility.OpenFolderPanel("Select folder for States", "Assets", "");
                if (!string.IsNullOrEmpty(selected))
                {
                    if (selected.StartsWith(Application.dataPath))
                    {
                        statesFolderPath = "Assets" + selected.Substring(Application.dataPath.Length);
                        EditorPrefs.SetString(STATE_PATH_KEY, statesFolderPath);
                        Debug.Log($"✅ Saved States folder: {statesFolderPath}");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Invalid Folder",
                                                    "Please select a folder inside your Assets directory.", "OK");
                    }
                }
            }

            // Save manually
            if (GUI.Button(new Rect(x + panelWidth - 85, y, 75, 20f), "💾 Save"))
            {
                EditorPrefs.SetString(STATE_PATH_KEY, statesFolderPath);
                Debug.Log($"✅ Saved States path: {statesFolderPath}");
            }

            y += 28f;

            // === TRANSITIONS PATH ===
            EditorGUI.LabelField(new Rect(x, y, panelWidth, 18f), "📁 Transitions Folder Path:");
            y += 18f;

            transitionsFolderPath = EditorGUI.TextField(new Rect(x, y, panelWidth - 170, 20f), transitionsFolderPath);

            if (GUI.Button(new Rect(x + panelWidth - 165, y, 75, 20f), "📂 Pick"))
            {
                string selected = EditorUtility.OpenFolderPanel("Select folder for Transitions", "Assets", "");
                if (!string.IsNullOrEmpty(selected))
                {
                    if (selected.StartsWith(Application.dataPath))
                    {
                        transitionsFolderPath = "Assets" + selected.Substring(Application.dataPath.Length);
                        EditorPrefs.SetString(TRANSITION_PATH_KEY, transitionsFolderPath);
                        Debug.Log($"✅ Saved Transitions folder: {transitionsFolderPath}");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Invalid Folder",
                                                    "Please select a folder inside your Assets directory.", "OK");
                    }
                }
            }

            if (GUI.Button(new Rect(x + panelWidth - 85, y, 75, 20f), "💾 Save"))
            {
                EditorPrefs.SetString(TRANSITION_PATH_KEY, transitionsFolderPath);
                Debug.Log($"✅ Saved Transitions path: {transitionsFolderPath}");
            }

            y += 36f;

            // === Основные кнопки ===
            if (GUI.Button(new Rect(x, y, panelWidth, buttonHeight), "🧩 New Graph"))
                CreateNewGraph();
            y += buttonHeight + spacing;

            if (GUI.Button(new Rect(x, y, panelWidth, buttonHeight), "📥 Load Graph"))
                LoadGraph();
            y += buttonHeight + spacing;

            if (GUI.Button(new Rect(x, y, panelWidth, buttonHeight), "🟢 New State"))
                CreateNewState();
            y += buttonHeight + spacing;

            // === Cancel selection ===
            if (isSelectingTargetNode)
            {
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUI.Button(new Rect(x, y, panelWidth, buttonHeight), "❌ Cancel Selection"))
                {
                    isSelectingTargetNode = false;
                    pendingTransition = null;
                    sourceNodeForSelection = null;
                    Debug.Log("Node selection canceled.");
                }
                GUI.backgroundColor = Color.white;
            }
        }

        private void CreateNewState()
        {
            if (currentGraph == null)
            {
                EditorUtility.DisplayDialog("No Graph Selected",
                                            "Please create or load a State Machine Graph first.", "OK");
                return;
            }

            // 🔹 Проверяем дубликаты State
            var existingStates = new HashSet<State>();
            foreach (var node in currentGraph.Nodes)
            {
                if (node.State != null)
                    existingStates.Add(node.State);
            }

            // 🔹 Проверяем и создаём папку
            if (string.IsNullOrEmpty(statesFolderPath))
            {
                EditorUtility.DisplayDialog("Path not set", "Please specify the folder for saving States.", "OK");
                return;
            }

            if (!Directory.Exists(statesFolderPath))
            {
                Directory.CreateDirectory(statesFolderPath);
                Debug.Log($"📁 Created missing folder: {statesFolderPath}");
            }

            // 🔹 Генерируем путь
            string fileName = $"NewState_{currentGraph.Nodes.Count}.asset";
            string targetPath = Path.Combine(statesFolderPath, fileName);
            targetPath = AssetDatabase.GenerateUniqueAssetPath(targetPath);

            // 🔹 Создаём State
            var state = CreateInstance<State>();
            state.name = Path.GetFileNameWithoutExtension(targetPath);

            AssetDatabase.CreateAsset(state, targetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 🔹 Проверяем дубликаты
            if (existingStates.Contains(state))
            {
                EditorUtility.DisplayDialog("Duplicate State Detected",
                                            $"State \"{state.name}\" is already used by another Node.", "OK");
                AssetDatabase.DeleteAsset(targetPath);
                return;
            }

            // 🔹 Добавляем Node
            // 🔹 Центр экрана в мировых координатах графа
            Vector2 screenCenter = new Vector2(position.width / 2f, position.height / 2f);
            Vector2 graphCenter = (screenCenter - panOffset) / zoom;

            // 🔹 Смещаем немного вверх, чтобы не перекрывалось меню
            graphCenter.y += 100f;

            var newNode = new Node(state)
                          {
                              Position = graphCenter - new Vector2(90, 30) // половина размера окна узла (180x60)
                          };

            currentGraph.Nodes.Add(newNode);
            EditorUtility.SetDirty(currentGraph);

            Debug.Log($"✅ Created new State asset at: {targetPath}");
            EditorGUIUtility.PingObject(state);
            Selection.activeObject = state;
        }

        private void CreateNewGraph()
        {
            currentGraph = CreateInstance<StateMachineGraph>();
            ProjectWindowUtil.CreateAsset(currentGraph, "NewStateMachineGraph.asset");
        }

        private void LoadGraph()
        {
            var path = EditorUtility.OpenFilePanel("Load State Machine Graph", "Assets", "asset");
            if (string.IsNullOrEmpty(path)) return;

            path = "Assets" + path.Replace(Application.dataPath, "");
            currentGraph = AssetDatabase.LoadAssetAtPath<StateMachineGraph>(path);
        }
        
        // Размер твоего холста должен совпадать с BeginClip
        private const float WORKSPACE_W = 10000f;
        private const float WORKSPACE_H = 10000f;
        private void DrawGraphArea()
        {
            transitionAnchorPositions.Clear();
            nodeRects.Clear();

            Event e = Event.current;

            // === Обработка масштабирования ===
            if (e.type == EventType.ScrollWheel)
            {
                float zoomDelta = -e.delta.y * 0.05f;
                float oldZoom = zoom;
                zoom = Mathf.Clamp(zoom + zoomDelta, zoomMin, zoomMax);

                // Зум вокруг центра окна
                Vector2 windowCenter = new Vector2(position.width / 2f, position.height / 2f);
                panOffset = (panOffset - windowCenter) * (zoom / oldZoom) + windowCenter;

                // ❗ Не даём «выйти» за рабочую область
                ClampPanToWorkspace(WORKSPACE_W, WORKSPACE_H);

                e.Use();
            }

            // === Панорамирование при зажатой ПКМ ===
            if (e.type == EventType.MouseDrag && e.button == 1)
            {
                panOffset += e.delta;

                // ❗ Держим внутри рабочей области
                ClampPanToWorkspace(WORKSPACE_W, WORKSPACE_H);

                e.Use();
                Repaint();
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
            GUI.EndClip();
            GUI.EndClip();
            GUI.BeginClip(new Rect(Vector2.zero, new Vector2(WORKSPACE_W,WORKSPACE_H)));
            GUI.BeginClip(new Rect(Vector2.zero, new Vector2(WORKSPACE_W,WORKSPACE_H)));
            
            // === Применяем трансформацию (панорамирование + масштабирование от центра) ===
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(panOffset, Quaternion.identity, Vector3.one * zoom);

            Rect backgroundRect = new Rect(0, 0, 10000, 10000);
            DrawBackgroundGrid(backgroundRect);
            
            EditorGUI.BeginDisabledGroup(isSelectingTargetNode);
            BeginWindows();
            nodeRects.Clear();

            // === 1. Отрисовка окон узлов ===
            for (var i = 0; i < currentGraph.Nodes.Count; i++)
            {
                var node = currentGraph.Nodes[i];
                var rect = new Rect(node.Position, new Vector2(180, 60));
                rect = GUILayout.Window(i, rect, _ => DrawNodeWindow(node),
                                        node.State != null ? $"Node" : "Empty Node");
                nodeRects[node] = rect;
                node.Position = rect.position;
            }

            EndWindows();

            // === 2. Точки входа ===
            foreach (var node in currentGraph.Nodes)
            {
                if (node.State == null) continue;
                var pointRect = new Rect(node.Position.x + 5, node.Position.y + 5, 15, 15);

                var prevColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.7f, 0.2f);

                GUIStyle pointStyle = new GUIStyle(GUI.skin.button)
                                      {
                                          alignment = TextAnchor.MiddleCenter,
                                          fontSize = 10,
                                          fontStyle = FontStyle.Bold,
                                          normal = { textColor = Color.white }
                                      };

                GUI.Box(pointRect, "●", pointStyle);
                GUI.backgroundColor = prevColor;
            }

            EditorGUI.EndDisabledGroup();

            // === 3. Связи между узлами ===
            Handles.BeginGUI();

            foreach (var node in currentGraph.Nodes)
            {
                if (node.State == null || node.State.Transitions == null)
                    continue;

                foreach (var transition in node.State.Transitions)
                {
                    if (transition == null || transition.TargetState == null)
                        continue;

                    var targetNode = currentGraph.Nodes.FirstOrDefault(n => n.State == transition.TargetState);
                    if (targetNode == null)
                        continue;

                    if (!nodeRects.TryGetValue(node, out var sourceRect) ||
                        !nodeRects.TryGetValue(targetNode, out var targetRect))
                        continue;

                    Vector2 startPos;
                    if (!transitionAnchorPositions.TryGetValue(transition, out startPos))
                        startPos = new Vector2(sourceRect.xMax - 10, sourceRect.center.y);
                    Vector2 endPos = new Vector2(targetRect.x + 10, targetRect.y + 12);

                    Vector2 startTangent = startPos + Vector2.right * 50f;
                    Vector2 endTangent = endPos + Vector2.left * 50f;

                    bool isSelected = Selection.activeObject == transition;
                    Handles.color = isSelected
                                        ? new Color(0.3f, 0.9f, 1f, 1f)
                                        : new Color(1f, 0.85f, 0.2f, 0.9f);

                    Handles.DrawBezier(startPos, endPos, startTangent, endTangent, Handles.color, null, 2f);

                    // Стрелка
                    Vector2 dir = (endPos - startPos).normalized;
                    Vector2 perp = new Vector2(-dir.y, dir.x);
                    Vector2 arrowTip = endPos;
                    Vector2 arrowBase1 = endPos - dir * 10f + perp * 4f;
                    Vector2 arrowBase2 = endPos - dir * 10f - perp * 4f;
                    Handles.DrawAAConvexPolygon(arrowTip, arrowBase1, arrowBase2);
                }
            }

            Handles.EndGUI();

            // === 4. Выбор целевого узла ===
            if (isSelectingTargetNode && pendingTransition != null)
            {
                Handles.BeginGUI();
                Vector2 mousePos = Event.current.mousePosition;

                foreach (var node in currentGraph.Nodes)
                {
                    if (node == sourceNodeForSelection) continue;
                    if (node.State == null) continue;
                    if (!nodeRects.TryGetValue(node, out var rect)) continue;

                    bool isHovered = rect.Contains(mousePos);
                    EditorGUI.DrawRect(rect, new Color(0f, 0.8f, 0.2f, isHovered ? 0.45f : 0.25f));

                    var bigStyle = new GUIStyle(GUI.skin.label)
                                   {
                                       alignment = TextAnchor.MiddleCenter,
                                       fontSize = 52,
                                       fontStyle = FontStyle.Bold,
                                       normal = { textColor = Color.white }
                                   };
                    GUI.Label(rect, "+", bigStyle);

                    if ((Event.current.rawType == EventType.MouseDown || Event.current.rawType == EventType.MouseUp)
                     && rect.Contains(Event.current.mousePosition))
                    {
                        pendingTransition.TargetState = node.State;
                        EditorUtility.SetDirty(pendingTransition);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();

                        Debug.Log($"✅ Transition now leads to State: {node.State.name}");

                        isSelectingTargetNode = false;
                        pendingTransition = null;
                        sourceNodeForSelection = null;

                        Event.current.Use();
                        GUIUtility.ExitGUI();
                        return;
                    }

                    if (isHovered)
                    {
                        Handles.color = new Color(1f, 1f, 1f, 0.85f);
                        Handles.DrawSolidRectangleWithOutline(rect, Color.clear, Handles.color);
                    }
                }

                Handles.EndGUI();
            }

            // === Восстановление матрицы ===
            GUI.matrix = oldMatrix;
            EditorGUILayout.EndScrollView();
        }
        
        /// <summary>
        /// Держит панорамирование в пределах рабочей области с учётом зума.
        /// Если вьюпорт больше контента — центрируем и блокируем ось.
        /// </summary>
        private void ClampPanToWorkspace(float workspaceW, float workspaceH)
        {
            float viewW = position.width;
            float viewH = position.height;

            // Пределы таковы, чтобы весь вьюпорт оставался внутри контента в мировых координатах
            float minX = viewW - workspaceW * zoom; // максимально влево (контент упирается правым краем)
            float maxX = 0f;                        // максимально вправо (контент упирается левым краем)
            float minY = viewH - workspaceH * zoom; // максимально вверх
            float maxY = 0f;                        // максимально вниз

            // Если вьюпорт больше контента по оси — центрируем и блокируем ось
            if (workspaceW * zoom <= viewW)
                panOffset.x = Mathf.Round((viewW - workspaceW * zoom) * 0.5f);
            else
                panOffset.x = Mathf.Clamp(panOffset.x, minX, maxX);

            if (workspaceH * zoom <= viewH)
                panOffset.y = Mathf.Round((viewH - workspaceH * zoom) * 0.5f);
            else
                panOffset.y = Mathf.Clamp(panOffset.y, minY, maxY);
        }
        
        private void DrawNodeWindow(Node node)
        {
            EditorGUI.BeginDisabledGroup(isSelectingTargetNode); // 🔸 Блокируем весь GUI внутри окна, если выбираем узел

            // === Кнопка удаления Node ===
            var removeButtonRect = new Rect(200, 5, 15, 15);
            if (GUI.Button(removeButtonRect, "x"))
            {
                bool shouldDeleteNode = true;

                if (node.State != null)
                {
                    string statePath = AssetDatabase.GetAssetPath(node.State);

                    bool confirm = EditorUtility.DisplayDialog(
                                                               "Delete State?",
                                                               $"Do you want to delete the State \"{node.State.name}\" and all its Transitions from the project?",
                                                               "Yes", "No");

                    if (confirm)
                    {
                        // --- 🗑 Удаляем все Transition из этого State ---
                        if (node.State.Transitions != null && node.State.Transitions.Count > 0)
                        {
                            foreach (var transition in node.State.Transitions.ToList())
                            {
                                if (transition == null) continue;

                                string transitionPath = AssetDatabase.GetAssetPath(transition);
                                if (!string.IsNullOrEmpty(transitionPath))
                                {
                                    Debug.Log($"🗑 Deleted Transition asset: {transition.name}");
                                    AssetDatabase.DeleteAsset(transitionPath);
                                }
                            }

                            node.State.Transitions.Clear();
                            EditorUtility.SetDirty(node.State);
                        }

                        // --- 🗑 Удаляем сам State ---
                        if (!string.IsNullOrEmpty(statePath))
                        {
                            Debug.Log($"🗑 Deleted State asset: {node.State.name}");
                            AssetDatabase.DeleteAsset(statePath);
                        }
                    }
                    else
                    {
                        shouldDeleteNode = false;
                    }
                }

                // --- Удаляем Node из графа ---
                if (shouldDeleteNode)
                {
                    currentGraph.Nodes.Remove(node);
                    EditorUtility.SetDirty(currentGraph);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    GUIUtility.ExitGUI();
                    return;
                }
            }

            // === Поле выбора State ===
            EditorGUI.BeginChangeCheck();
            var newState =
                (State)EditorGUILayout.ObjectField(node.State, typeof(State), false, GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck())
            {
                // Проверка на дубликаты State
                if (currentGraph.Nodes.Exists(n => n != node && n.State == newState))
                {
                    EditorUtility.DisplayDialog("Duplicate State Detected",
                                                $"State \"{newState.name}\" is already assigned to another Node.",
                                                "OK");
                }
                else
                {
                    node.State = newState;
                    EditorUtility.SetDirty(currentGraph);
                }
            }

            if (node.State == null)
            {
                EditorGUILayout.HelpBox("No state assigned", MessageType.Warning);
                GUI.DragWindow();
                return;
            }

            var state = node.State;

            // === BEHAVIOURS ===
            EditorGUILayout.LabelField("Behaviours:", EditorStyles.miniBoldLabel);
            int removeBehaviourIndex = -1;
            if (state.Behaviours != null && state.Behaviours.Count > 0)
            {
                for (int i = 0; i < state.Behaviours.Count; i++)
                {
                    var behaviour = state.Behaviours[i];
                    EditorGUILayout.BeginHorizontal();

                    EditorGUI.BeginChangeCheck();
                    var newBehaviour = (BaseBehaviour)EditorGUILayout.ObjectField(
                         behaviour, typeof(BaseBehaviour), false, GUILayout.ExpandWidth(true));
                    if (EditorGUI.EndChangeCheck())
                    {
                        state.Behaviours[i] = newBehaviour;
                        EditorUtility.SetDirty(state);
                    }

                    if (GUILayout.Button("X", GUILayout.Width(20)))
                        removeBehaviourIndex = i;

                    EditorGUILayout.EndHorizontal();
                }
            }
            else
                EditorGUILayout.LabelField("— none —", EditorStyles.miniLabel);

            if (GUILayout.Button("+ Add new Behaviour"))
            {
                // ReSharper disable once PossibleNullReferenceException
                state.Behaviours.Add(null);
                EditorUtility.SetDirty(state);
            }

            if (removeBehaviourIndex >= 0)
            {
                // ReSharper disable once PossibleNullReferenceException
                state.Behaviours.RemoveAt(removeBehaviourIndex);
                EditorUtility.SetDirty(state);
            }

            EditorGUILayout.Space(5);

            // === TRANSITIONS ===
            DrawTransitionsSection(state, node);

            EditorGUI.EndDisabledGroup(); // 🔸 Возвращаем нормальное состояние GUI
            GUI.DragWindow();
        }

        private void DrawTransitionsSection(State state, Node ownerNode)
        {
            EditorGUILayout.LabelField("Transitions:", EditorStyles.miniBoldLabel);
            int removeTransitionIndex = -1;

            // === 1. Очистка списка от уничтоженных ссылок ===
            for (int i = state.Transitions.Count - 1; i >= 0; i--)
            {
                var tr = state.Transitions[i];
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (tr == null || ReferenceEquals(tr, null) || !AssetDatabase.Contains(tr))
                {
                    state.Transitions.RemoveAt(i);
                    EditorUtility.SetDirty(state);
                }
            }

            // === 2. Отрисовка переходов ===
            for (int i = 0; i < state.Transitions.Count; i++)
            {
                var transition = state.Transitions[i];
                if (transition is null || !AssetDatabase.Contains(transition))
                    continue;

                EditorGUILayout.BeginHorizontal();

                // === Поле выбора Transition с проверками ===
                EditorGUI.BeginChangeCheck();
                var newTransition = (Transition)EditorGUILayout.ObjectField(
                                                                            transition, typeof(Transition), false,
                                                                            GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck())
                {
                    // Проверка: нельзя выбрать Transition, который уже используется в другом State
                    bool duplicateInOtherState = currentGraph.Nodes.Any(n =>
                                                                            n.State != null &&
                                                                            n.State.Transitions
                                                                             .Contains(newTransition) &&
                                                                            n.State != state);

                    // Проверка: нельзя выбрать Transition, который уже есть в этом State
                    bool duplicateInSameState = state.Transitions.Contains(newTransition) &&
                                                state.Transitions[i] != newTransition;

                    if (duplicateInOtherState)
                    {
                        EditorUtility.DisplayDialog("Duplicate Transition Detected",
                                                    $"Transition \"{newTransition.name}\" is already used in another State.",
                                                    "OK");
                    }
                    else if (duplicateInSameState)
                    {
                        EditorUtility.DisplayDialog("Duplicate Transition Detected",
                                                    $"Transition \"{newTransition.name}\" already exists in this State.",
                                                    "OK");
                    }
                    else
                    {
                        state.Transitions[i] = newTransition;
                        EditorUtility.SetDirty(state);
                    }
                }

                // === Кнопка удаления Transition ===
                if (GUILayout.Button("X", GUILayout.Width(20)))
                    removeTransitionIndex = i;

                // === Кнопка выбора целевого узла (●) ===
                GUI.backgroundColor = new Color(1f, 0.7f, 0.2f);
                bool pickPressed = GUILayout.Button("●", GUILayout.Width(20));
                GUI.backgroundColor = Color.white;

                // ✅ Запоминаем экранную позицию кнопки "●"
                Rect localBtnRect = GUILayoutUtility.GetLastRect();
                Vector2 localCenter = new Vector2(
                                                  localBtnRect.x + localBtnRect.width * 0.5f,
                                                  localBtnRect.y + localBtnRect.height * 0.5f);

                if (nodeRects.TryGetValue(ownerNode, out var nodeRect))
                {
                    Vector2 globalCenter = nodeRect.position + localCenter;
                    transitionAnchorPositions[transition] = globalCenter;
                }

                // ✅ При нажатии — активируем режим выбора целевого узла
                if (pickPressed)
                {
                    isSelectingTargetNode = true;
                    pendingTransition = transition;
                    sourceNodeForSelection = currentGraph.Nodes.Find(n => n.State == state);
                    Debug.Log($".Select target node for transition from state: {state.name}");
                }

                EditorGUILayout.EndHorizontal();
            }

            if (state.Transitions.Count == 0)
                EditorGUILayout.LabelField("— none —", EditorStyles.miniLabel);

            EditorGUILayout.Space(2);

            // === 3. Добавление нового Transition ===
            if (GUILayout.Button("+ Add new Transition"))
            {
                if (string.IsNullOrEmpty(transitionsFolderPath))
                {
                    EditorUtility.DisplayDialog("Path not set",
                                                "Please specify the folder for saving Transitions.", "OK");
                    return;
                }

                if (!Directory.Exists(transitionsFolderPath))
                {
                    Directory.CreateDirectory(transitionsFolderPath);
                    Debug.Log($"📁 Created missing folder: {transitionsFolderPath}");
                }

                string fileName = $"{state.name}_Transition_{state.Transitions.Count}.asset";
                string targetPath = Path.Combine(transitionsFolderPath, fileName);
                targetPath = AssetDatabase.GenerateUniqueAssetPath(targetPath);

                // Проверяем, не существует ли Transition с тем же путём
                var existingTransition = currentGraph.Nodes
                                                     .SelectMany(n => n.State.Transitions ?? new List<Transition>())
                                                     .FirstOrDefault(t => t != null &&
                                                                          AssetDatabase.GetAssetPath(t) == targetPath);

                if (existingTransition != null)
                {
                    EditorUtility.DisplayDialog("Duplicate Transition Detected",
                                                $"Transition with same file path already exists: {existingTransition.name}",
                                                "OK");
                    return;
                }

                // Создаём Transition
                var newTransition = CreateInstance<Transition>();
                newTransition.name = Path.GetFileNameWithoutExtension(targetPath);

                AssetDatabase.CreateAsset(newTransition, targetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                state.Transitions.Add(newTransition);
                EditorUtility.SetDirty(state);

                Debug.Log($"✅ Created new Transition asset at: {targetPath}");
            }

            // === 4. Удаление Transition ===
            if (removeTransitionIndex >= 0 && removeTransitionIndex < state.Transitions.Count)
            {
                var removedTransition = state.Transitions[removeTransitionIndex];
                state.Transitions.RemoveAt(removeTransitionIndex);
                EditorUtility.SetDirty(state);

                if (removedTransition is not null)
                {
                    string path = AssetDatabase.GetAssetPath(removedTransition);
                    if (!string.IsNullOrEmpty(path))
                    {
                        bool confirm = EditorUtility.DisplayDialog(
                                                                   "Delete Transition?",
                                                                   $".Delete Transition \"{removedTransition.name}\" from project?",
                                                                   "Yes", "No");

                        if (confirm)
                        {
                            Debug.Log($"🗑 Deleted Transition asset: {removedTransition.name}");
                            AssetDatabase.DeleteAsset(path);
                        }
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = null;
                GUIUtility.ExitGUI();
            }
        }
        
        private void DrawBackgroundGrid(Rect rect)
        {
            // Цвета и параметры сетки
            Color minorColor = new Color(0.25f, 0.25f, 0.25f, 0.35f);
            Color majorColor = new Color(0.25f, 0.25f, 0.25f, 0.6f);

            // Размеры сетки (меняются при увеличении)
            float gridSpacing = 20f * zoom;     // шаг мелких линий
            float majorStep = gridSpacing * 5f; // каждая 5-я линия толще

            // Смещение сетки при панорамировании
            Vector2 offset = new Vector2(panOffset.x % gridSpacing, panOffset.y % gridSpacing);

            Handles.BeginGUI();

            // Мелкая сетка
            Handles.color = minorColor;
            for (float x = rect.xMin + offset.x; x < rect.xMax; x += gridSpacing)
                Handles.DrawLine(new Vector3(x, rect.yMin, 0), new Vector3(x, rect.yMax, 0));
            for (float y = rect.yMin + offset.y; y < rect.yMax; y += gridSpacing)
                Handles.DrawLine(new Vector3(rect.xMin, y, 0), new Vector3(rect.xMax, y, 0));

            // Крупная сетка
            Handles.color = majorColor;
            for (float x = rect.xMin + offset.x; x < rect.xMax; x += majorStep)
                Handles.DrawLine(new Vector3(x, rect.yMin, 0), new Vector3(x, rect.yMax, 0));
            for (float y = rect.yMin + offset.y; y < rect.yMax; y += majorStep)
                Handles.DrawLine(new Vector3(rect.xMin, y, 0), new Vector3(rect.xMax, y, 0));

            Handles.EndGUI();
        }
    }
}