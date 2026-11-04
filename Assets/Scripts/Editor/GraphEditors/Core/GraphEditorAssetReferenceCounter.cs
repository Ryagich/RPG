using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    /// <summary>
    /// Counts project-wide references before an editor removes a shared subasset.
    /// Domain editors provide their owner asset type and reference projection.
    /// </summary>
    internal static class GraphEditorAssetReferenceCounter
    {
        public static int CountReferencesOutside<TOwner, TReferenced>(
            TOwner excludedOwner,
            TReferenced referencedAsset,
            string ownerSearchFilter,
            Func<TOwner, IEnumerable<TReferenced>> getReferences)
            where TOwner : ScriptableObject
            where TReferenced : UnityEngine.Object
        {
            if (referencedAsset == null || string.IsNullOrWhiteSpace(ownerSearchFilter) || getReferences == null)
            {
                return 0;
            }

            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets(ownerSearchFilter))
            {
                TOwner owner = AssetDatabase.LoadAssetAtPath<TOwner>(AssetDatabase.GUIDToAssetPath(guid));
                if (owner == null || owner == excludedOwner)
                {
                    continue;
                }

                IEnumerable<TReferenced> references = getReferences(owner);
                if (references == null)
                {
                    continue;
                }

                foreach (TReferenced candidate in references)
                {
                    if (candidate == referencedAsset)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
