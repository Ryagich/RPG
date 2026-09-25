using System.Collections.Generic;
using Dialogs.Graph.Model;

namespace Dialogs.Graph.Editor
{
    internal sealed class DialogAnswerFoldoutState
    {
        private readonly Dictionary<string, bool> values = new();

        public bool Get(DialogPhrase phrase, int answerIndex) =>
            values.TryGetValue(GetKey(phrase, answerIndex), out bool expanded) && expanded;

        public void Set(DialogPhrase phrase, int answerIndex, bool expanded) =>
            values[GetKey(phrase, answerIndex)] = expanded;

        public bool Get(string key) => values.TryGetValue(key, out bool expanded) && expanded;
        public void Set(string key, bool expanded) => values[key] = expanded;

        public void Clear(DialogPhrase phrase)
        {
            if (phrase == null) return;
            string prefix = phrase.GetInstanceID() + ":";
            var keys = new List<string>(values.Keys);
            foreach (string key in keys) if (key.StartsWith(prefix)) values.Remove(key);
        }

        private static string GetKey(DialogPhrase phrase, int index) => phrase.GetInstanceID() + ":" + index;
    }
}
