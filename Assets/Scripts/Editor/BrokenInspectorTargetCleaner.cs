using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    [InitializeOnLoad]
    public static class BrokenInspectorTargetCleaner
    {
        static BrokenInspectorTargetCleaner()
        {
            EditorApplication.delayCall += Clean;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/RPG/Editor/Clean Broken Inspector Targets")]
        public static void Clean()
        {
            Selection.objects = Selection.objects.Where(target => target != null).ToArray();

            foreach (var editor in Resources.FindObjectsOfTypeAll<Editor>())
            {
                if (editor == null || !HasBrokenTarget(editor))
                {
                    continue;
                }

                Object.DestroyImmediate(editor);
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                Clean();
            }
        }

        private static bool HasBrokenTarget(Editor editor)
        {
            try
            {
                var targets = editor.targets;
                return targets == null || targets.Any(target => target == null);
            }
            catch
            {
                return true;
            }
        }
    }
}
