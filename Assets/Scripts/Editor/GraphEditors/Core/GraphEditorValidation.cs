namespace EditorTools
{
    /// <summary>Domain-neutral result of a read-only graph validation pass.</summary>
    internal enum GraphEditorValidationSeverity { Warning, Error }

    internal readonly struct GraphEditorValidationIssue
    {
        public GraphEditorValidationIssue(GraphEditorValidationSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }

        public GraphEditorValidationSeverity Severity { get; }
        public string Message { get; }
    }
}
