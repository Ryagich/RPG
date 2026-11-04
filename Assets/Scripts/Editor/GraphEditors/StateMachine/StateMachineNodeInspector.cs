using System.Collections.Generic;
using System.Linq;
using EditorTools;
using StateMachine.Graph.Model;
using UnityEditor;
using UnityEngine;

namespace StateMachine.Graph.Editor
{
    /// <summary>Small window-facing contract needed to render one State node inspector.</summary>
    internal interface IStateMachineNodeInspectorHost
    {
        StateMachineGraph CurrentGraph { get; }
        GraphEditorTheme Theme { get; }
        bool ContainsState(State state);
        bool TryAssignState(Node node, State state);
        void MarkInspectorAssetDirty(Object asset);
        void NotifyTransitionTargetChanged(Transition transition);
        void BeginTransitionTargetSelection(Transition transition, Node sourceNode);
        void CreateTransition(State state);
        void RemoveTransition(State state, int index);
        void RecordTransitionPicker(Transition transition, Rect pickerRect);
    }

    /// <summary>
    /// Renders and edits the serialized contents of a State. Graph navigation, canvas state,
    /// confirmation dialogs and asset lifecycle remain owned by the editor window.
    /// </summary>
    internal sealed class StateMachineNodeInspector
    {
        private readonly IStateMachineNodeInspectorHost host;

        public StateMachineNodeInspector(IStateMachineNodeInspectorHost host)
        {
            this.host = host;
        }

        public void Draw(Node node)
        {
            EditorGUI.BeginChangeCheck();
            var newState = (State)EditorGUILayout.ObjectField(node.State, typeof(State), false);
            if (EditorGUI.EndChangeCheck())
            {
                host.TryAssignState(node, newState);
            }

            if (node.State == null)
            {
                EditorGUILayout.HelpBox("No state assigned.", MessageType.Warning);
                return;
            }

            EnsureCollections(node.State);
            DrawStateEditor(node.State, node);
        }

        /// <summary>Renders the inexpensive, at-a-glance fields kept visible on collapsed nodes.</summary>
        public void DrawCollapsed(Node node)
        {
            if (node.State == null)
            {
                EditorGUILayout.HelpBox("No state assigned.", MessageType.Warning);
                return;
            }

            EnsureCollections(node.State);
            EditorGUILayout.LabelField("Behaviours", host.Theme.MiniBoldLabelStyle);
            if (node.State.Behaviours.Count == 0)
            {
                EditorGUILayout.LabelField("- none -", host.Theme.MiniLabelStyle);
            }

            for (int index = 0; index < node.State.Behaviours.Count; index++)
            {
                EditorGUI.BeginChangeCheck();
                BaseBehaviour behaviour = (BaseBehaviour)EditorGUILayout.ObjectField(
                    node.State.Behaviours[index], typeof(BaseBehaviour), false);
                if (EditorGUI.EndChangeCheck())
                {
                    node.State.Behaviours[index] = behaviour;
                    host.MarkInspectorAssetDirty(node.State);
                }
            }

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Transitions", host.Theme.MiniBoldLabelStyle);
            if (node.State.Transitions.Count == 0)
            {
                EditorGUILayout.LabelField("- none -", host.Theme.MiniLabelStyle);
            }

            for (int index = 0; index < node.State.Transitions.Count; index++)
            {
                Transition transition = node.State.Transitions[index];
                if (transition == null || !AssetDatabase.Contains(transition))
                {
                    continue;
                }

                EditorGUI.BeginChangeCheck();
                State target = (State)EditorGUILayout.ObjectField(transition.TargetState, typeof(State), false);
                if (EditorGUI.EndChangeCheck())
                {
                    transition.TargetState = target;
                    host.MarkInspectorAssetDirty(transition);
                    host.NotifyTransitionTargetChanged(transition);
                }

                if (index < node.State.Transitions.Count - 1)
                {
                    DrawCompactTransitionDivider();
                }
            }
        }

        private void DrawCompactTransitionDivider()
        {
            EditorGUILayout.Space(2f);
            Rect dividerRect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(dividerRect, host.Theme.SubtleDividerColor);
            EditorGUILayout.Space(2f);
        }

        private void DrawStateEditor(State state, Node ownerNode)
        {
            SerializedObject stateObject = new(state);
            stateObject.Update();
            SerializedProperty nameProperty = stateObject.FindProperty("<Name>k__BackingField");
            if (nameProperty != null)
            {
                EditorGUILayout.PropertyField(nameProperty, new GUIContent("Name"), true);
            }

            if (stateObject.hasModifiedProperties)
            {
                stateObject.ApplyModifiedProperties();
                host.MarkInspectorAssetDirty(state);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Behaviours", host.Theme.MiniBoldLabelStyle);
            DrawBehavioursSection(state);
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Transitions", host.Theme.MiniBoldLabelStyle);
            DrawTransitionsSection(state, ownerNode);
        }

        private void DrawBehavioursSection(State state)
        {
            int removeIndex = -1;
            for (int index = 0; index < state.Behaviours.Count; index++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                BaseBehaviour value = (BaseBehaviour)EditorGUILayout.ObjectField(
                    state.Behaviours[index], typeof(BaseBehaviour), false, GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck())
                {
                    state.Behaviours[index] = value;
                    host.MarkInspectorAssetDirty(state);
                }

                if (DrawMiniButton("X", GUILayout.Width(22f))) removeIndex = index;
                EditorGUILayout.EndHorizontal();
            }

            if (state.Behaviours.Count == 0) EditorGUILayout.LabelField("- none -", host.Theme.MiniLabelStyle);
            if (DrawButton("+ Add Behaviour"))
            {
                state.Behaviours.Add(null);
                host.MarkInspectorAssetDirty(state);
            }

            if (removeIndex >= 0)
            {
                state.Behaviours.RemoveAt(removeIndex);
                host.MarkInspectorAssetDirty(state);
            }
        }

        private void DrawTransitionsSection(State state, Node ownerNode)
        {
            CleanupMissingTransitions(state);
            int removeIndex = -1;
            for (int index = 0; index < state.Transitions.Count; index++)
            {
                Transition transition = state.Transitions[index];
                if (transition == null || !AssetDatabase.Contains(transition)) continue;

                bool missingLink = transition.TargetState == null;
                bool outsideGraph = !missingLink && !host.ContainsState(transition.TargetState);
                Color previousColor = GUI.color;
                GUI.color = missingLink ? host.Theme.MissingLinkPanelTint : outsideGraph ? host.Theme.OutsideGraphPanelTint : previousColor;
                EditorGUILayout.BeginVertical(host.Theme.HelpBoxStyle);
                GUI.color = previousColor;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Transition {index + 1}", host.Theme.MiniBoldLabelStyle);
                if (DrawMiniButton("X", GUILayout.Width(22f))) removeIndex = index;
                Color previousBackground = GUI.backgroundColor;
                GUI.backgroundColor = missingLink ? host.Theme.DangerButtonColor : host.Theme.LinkButtonColor;
                bool pickPressed = DrawMiniButton("O", GUILayout.Width(22f));
                GUI.backgroundColor = previousBackground;
                host.RecordTransitionPicker(transition, GUILayoutUtility.GetLastRect());
                if (pickPressed) host.BeginTransitionTargetSelection(transition, ownerNode);
                EditorGUILayout.EndHorizontal();

                DrawTransitionAssetField(state, index);
                transition = state.Transitions[index];
                if (transition != null)
                {
                    DrawTransitionInspector(transition);
                    if (transition.TargetState == null)
                        EditorGUILayout.HelpBox("Target state is not assigned for this transition.", MessageType.Error);
                    else if (!host.ContainsState(transition.TargetState))
                        EditorGUILayout.HelpBox("Target state is not added to the current graph.", MessageType.Warning);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2f);
            }

            if (DrawButton("+ Add Transition")) host.CreateTransition(state);
            if (removeIndex >= 0 && removeIndex < state.Transitions.Count) host.RemoveTransition(state, removeIndex);
        }

        private void DrawTransitionAssetField(State state, int index)
        {
            Transition transition = state.Transitions[index];
            EditorGUI.BeginChangeCheck();
            Transition replacement = (Transition)EditorGUILayout.ObjectField("Asset", transition, typeof(Transition), false);
            if (!EditorGUI.EndChangeCheck()) return;
            if (replacement == null)
            {
                state.Transitions[index] = null;
                host.MarkInspectorAssetDirty(state);
                return;
            }

            bool duplicate = state.Transitions.Where((candidate, candidateIndex) => candidateIndex != index)
                .Any(candidate => candidate == replacement);
            if (duplicate)
            {
                EditorUtility.DisplayDialog("Duplicate Transition Detected", $"Transition \"{replacement.name}\" already exists in this state.", "OK");
                return;
            }

            state.Transitions[index] = replacement;
            host.MarkInspectorAssetDirty(state);
        }

        private void DrawTransitionInspector(Transition transition)
        {
            SerializedObject serializedTransition = new(transition);
            serializedTransition.Update();
            DrawEnumPropertyField(serializedTransition.FindProperty("<Type>k__BackingField"));
            SerializedProperty target = serializedTransition.FindProperty("TargetState");
            if (target != null) EditorGUILayout.PropertyField(target);
            SerializedProperty conditions = serializedTransition.FindProperty("<Conditions>k__BackingField");
            if (conditions != null) EditorGUILayout.PropertyField(conditions, true);
            SerializedProperty actions = serializedTransition.FindProperty("<ActionOnTransitions>k__BackingField");
            if (actions != null) EditorGUILayout.PropertyField(actions, true);
            if (serializedTransition.hasModifiedProperties)
            {
                State previousTarget = transition.TargetState;
                serializedTransition.ApplyModifiedProperties();
                host.MarkInspectorAssetDirty(transition);
                if (transition.TargetState != previousTarget)
                {
                    host.NotifyTransitionTargetChanged(transition);
                }
            }
        }

        private void CleanupMissingTransitions(State state)
        {
            for (int index = state.Transitions.Count - 1; index >= 0; index--)
            {
                if (state.Transitions[index] == null || !AssetDatabase.Contains(state.Transitions[index]))
                {
                    state.Transitions.RemoveAt(index);
                    host.MarkInspectorAssetDirty(state);
                }
            }
        }

        private void DrawEnumPropertyField(SerializedProperty property)
        {
            if (property == null) return;
            if (property.propertyType != SerializedPropertyType.Enum)
            {
                EditorGUILayout.PropertyField(property);
                return;
            }

            int selected = EditorGUILayout.Popup(property.displayName, property.enumValueIndex, property.enumDisplayNames, host.Theme.PopupStyle);
            if (selected != property.enumValueIndex) property.enumValueIndex = selected;
        }

        private bool DrawButton(string label, params GUILayoutOption[] options) => GUILayout.Button(label, host.Theme.ButtonStyle, options);
        private bool DrawMiniButton(string label, params GUILayoutOption[] options) => GUILayout.Button(label, host.Theme.MiniButtonStyle, options);

        private static void EnsureCollections(State state)
        {
            state.Behaviours ??= new List<BaseBehaviour>();
            state.Transitions ??= new List<Transition>();
        }
    }
}
