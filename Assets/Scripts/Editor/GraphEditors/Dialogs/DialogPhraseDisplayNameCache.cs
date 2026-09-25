using System.Collections.Generic;
using Dialogs.Graph.Model;
using UnityEditor;

namespace Dialogs.Graph.Editor
{
    internal sealed class DialogPhraseDisplayNameCache
    {
        private readonly Dictionary<DialogPhrase, string> values = new();

        public string Get(DialogPhrase phrase)
        {
            if (phrase == null) return "Phrase Node";
            if (values.TryGetValue(phrase, out string value)) return value;
            var serialized = new SerializedObject(phrase);
            SerializedProperty entry = serialized.FindProperty("text")?.FindPropertyRelative("m_TableEntryReference");
            SerializedProperty key = entry?.FindPropertyRelative("m_Key");
            SerializedProperty keyId = entry?.FindPropertyRelative("m_KeyId");
            value = key != null && !string.IsNullOrWhiteSpace(key.stringValue)
                ? key.stringValue
                : keyId != null && keyId.longValue != 0 ? $"Key {keyId.longValue}" : "Нет строки: " + phrase.name;
            values[phrase] = value;
            return value;
        }

        public void Invalidate(DialogPhrase phrase) { if (phrase != null) values.Remove(phrase); }
        public void Clear() => values.Clear();
    }
}
