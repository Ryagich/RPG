using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    /// <summary>Shared compact UI for non-mutating graph validation results.</summary>
    internal static class GraphEditorValidationGui
    {
        public static float DrawSummary(float x, float y, float width, IReadOnlyList<GraphEditorValidationIssue> issues)
        {
            if (issues == null || issues.Count == 0) return 0f;
            int errors = issues.Count(issue => issue.Severity == GraphEditorValidationSeverity.Error);
            string message = errors > 0
                ? $"Graph validation: {errors} error(s), {issues.Count - errors} warning(s)."
                : $"Graph validation: {issues.Count} warning(s).";
            EditorGUI.HelpBox(new Rect(x, y, width, 40f), message, errors > 0 ? MessageType.Error : MessageType.Warning);
            return 46f;
        }
    }
}
