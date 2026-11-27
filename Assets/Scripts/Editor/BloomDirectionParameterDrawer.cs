using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using World.Rendering;

namespace EditorTools
{
    [VolumeParameterDrawer(typeof(BloomDirectionParameter))]
    public sealed class BloomDirectionParameterDrawer : VolumeParameterDrawer
    {
        public override float GetElementHeight(SerializedDataParameter parameter) => EditorGUIUtility.singleLineHeight;

        public override bool OnGUI(SerializedDataParameter parameter, GUIContent title)
        {
            var property = parameter.value;
            Vector2 value = property.vector2Value;
            EditorGUI.BeginChangeCheck();
            var rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(rect, title);
            float gap = 8f;
            float width = (rect.width - gap) * 0.5f;
            var left = new Rect(rect.x, rect.y, width, rect.height);
            var right = new Rect(rect.x + width + gap, rect.y, width, rect.height);
            float previousWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 58f;
            value.x = EditorGUI.FloatField(left, new GUIContent("Intensity"), value.x);
            value.y = EditorGUI.FloatField(right, new GUIContent("Stretch"), value.y);
            EditorGUIUtility.labelWidth = previousWidth;
            if (EditorGUI.EndChangeCheck())
                property.vector2Value = new Vector2(Mathf.Max(0f, value.x), Mathf.Clamp(value.y, 0.01f, 4f));
            return true;
        }
    }
}
