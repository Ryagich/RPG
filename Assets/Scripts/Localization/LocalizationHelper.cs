using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Localization
{
    public static class LocalizationHelper
    {
        private static readonly Dictionary<long, string> Cache = new();

        public static async Task InvalidateAsync(string language)
        {
            await LocalizationSettings.InitializationOperation;
            var locales = LocalizationSettings.AvailableLocales.Locales;

            // Ищем локаль по коду, например "en", "ru", "de" и т.д.
            var locale = locales.Find(l => l.Identifier.Code == language);

            if (locale != null)
            {
                LocalizationSettings.SelectedLocale = locale;
            }

            var tablesTask = LocalizationSettings.StringDatabase.GetAllTables();
            await tablesTask;
            var tables = tablesTask.Result;

            foreach (var table in tables)
            {
                foreach (var entry in table)
                {
                    Cache.TryAdd(entry.Key, entry.Value.LocalizedValue);
                }
            }
        }

        public static string GetLocalizedStringCached(this LocalizedString localizedString)
        {
            if (localizedString == null)
            {
                return string.Empty;
            }

            long keyId = localizedString.TableEntryReference.KeyId;
            if (keyId == 0)
            {
                return string.Empty;
            }

            return Cache.TryGetValue(keyId, out string localizedValue)
                ? localizedValue
                : string.Empty;
        }
    }
    
    public static class LocalizationAwaiter
    {
        private static TaskCompletionSource<bool> _tcs;

        public static Task WaitUntilReadyAsync()
        {
            _tcs ??= new TaskCompletionSource<bool>();
            return _tcs.Task;
        }

        internal static void SignalReady()
        {
            _tcs?.TrySetResult(true);
        }
    }
}
