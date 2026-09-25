using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace EditorTools
{
    /// <summary>
    /// Shares immutable localization lookup results between custom editor windows.
    /// The cache is invalidated whenever Unity reports an asset/project change.
    /// </summary>
    internal static class GraphEditorLocalizationCache
    {
        private static ReadOnlyCollection<StringTableCollection> stringTableCollections;
        private static readonly Dictionary<string, StringTableCollection> collectionByReference = new(StringComparer.Ordinal);
        private static readonly Dictionary<LocalizedTableKey, StringTable> tableByCollectionAndLocale = new();
        private static readonly Dictionary<LocalizedValueKey, string> valueByCollectionEntryAndLocale = new();

        static GraphEditorLocalizationCache()
        {
            EditorApplication.projectChanged += Invalidate;
        }

        public static ReadOnlyCollection<StringTableCollection> GetStringTableCollections()
        {
            return stringTableCollections ??= LocalizationEditorSettings.GetStringTableCollections();
        }

        public static StringTableCollection ResolveStringTableCollection(string serializedTableReference)
        {
            if (string.IsNullOrWhiteSpace(serializedTableReference))
            {
                return null;
            }

            if (collectionByReference.TryGetValue(serializedTableReference, out StringTableCollection cachedCollection))
            {
                return cachedCollection;
            }

            foreach (StringTableCollection collection in GetStringTableCollections())
            {
                string guidReference = $"GUID:{collection.SharedData.TableCollectionNameGuid:N}";
                if (string.Equals(serializedTableReference, guidReference, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(serializedTableReference, collection.TableCollectionName, StringComparison.Ordinal))
                {
                    collectionByReference[serializedTableReference] = collection;
                    return collection;
                }
            }

            collectionByReference[serializedTableReference] = null;
            return null;
        }

        public static string GetLocalizedValue(StringTableCollection collection, long entryId, string localeCode)
        {
            if (collection == null || entryId == 0 || string.IsNullOrWhiteSpace(localeCode))
            {
                return string.Empty;
            }

            var valueKey = new LocalizedValueKey(collection, entryId, localeCode);
            if (valueByCollectionEntryAndLocale.TryGetValue(valueKey, out string cachedValue))
            {
                return cachedValue;
            }

            var tableKey = new LocalizedTableKey(collection, localeCode);
            if (!tableByCollectionAndLocale.TryGetValue(tableKey, out StringTable table))
            {
                foreach (StringTable candidate in collection.StringTables)
                {
                    if (candidate != null && candidate.LocaleIdentifier.Code == localeCode)
                    {
                        table = candidate;
                        break;
                    }
                }

                tableByCollectionAndLocale[tableKey] = table;
            }

            StringTableEntry entry = table?.GetEntry(entryId);
            string value = entry != null && !string.IsNullOrWhiteSpace(entry.LocalizedValue)
                ? entry.LocalizedValue
                : string.Empty;
            valueByCollectionEntryAndLocale[valueKey] = value;
            return value;
        }

        public static void Invalidate()
        {
            stringTableCollections = null;
            collectionByReference.Clear();
            tableByCollectionAndLocale.Clear();
            valueByCollectionEntryAndLocale.Clear();
        }

        private readonly struct LocalizedTableKey : IEquatable<LocalizedTableKey>
        {
            private readonly StringTableCollection collection;
            private readonly string localeCode;

            public LocalizedTableKey(StringTableCollection collection, string localeCode)
            {
                this.collection = collection;
                this.localeCode = localeCode;
            }

            public bool Equals(LocalizedTableKey other)
            {
                return collection == other.collection &&
                       string.Equals(localeCode, other.localeCode, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is LocalizedTableKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((collection != null ? collection.GetInstanceID() : 0) * 397) ^
                           (localeCode != null ? StringComparer.Ordinal.GetHashCode(localeCode) : 0);
                }
            }
        }

        private readonly struct LocalizedValueKey : IEquatable<LocalizedValueKey>
        {
            private readonly StringTableCollection collection;
            private readonly long entryId;
            private readonly string localeCode;

            public LocalizedValueKey(StringTableCollection collection, long entryId, string localeCode)
            {
                this.collection = collection;
                this.entryId = entryId;
                this.localeCode = localeCode;
            }

            public bool Equals(LocalizedValueKey other)
            {
                return collection == other.collection &&
                       entryId == other.entryId &&
                       string.Equals(localeCode, other.localeCode, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is LocalizedValueKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = collection != null ? collection.GetInstanceID() : 0;
                    hash = (hash * 397) ^ entryId.GetHashCode();
                    return (hash * 397) ^ (localeCode != null ? StringComparer.Ordinal.GetHashCode(localeCode) : 0);
                }
            }
        }
    }
}
