using System;
using System.Collections.Generic;
using System.Linq;
using Container;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EditorTools
{
    /// <summary>
    /// Gives every NPC placement in an authored scene a serialised identity. Runtime hierarchy
    /// positions must never take part in save identities: normal gameplay may add, disable or
    /// destroy objects and therefore changes sibling indexes.
    /// </summary>
    public static class NpcPersistentIdMigration
    {
        [MenuItem("Tools/RPG/Persistence/Assign NPC Persistent IDs In Build Scenes")]
        public static void AssignIdsInBuildScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("NPC persistent IDs can only be assigned outside Play Mode.");
            }

            var scenePaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (scenePaths.Length == 0)
            {
                throw new InvalidOperationException("No enabled build scenes are available for NPC persistent-ID migration.");
            }

            var identities = new Dictionary<string, string>(StringComparer.Ordinal);
            var updatedSceneCount = 0;
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var scenesToCheck = new List<Scene>(scenePaths.Length);
                for (var index = 0; index < scenePaths.Length; index++)
                {
                    string scenePath = scenePaths[index];
                    EditorUtility.DisplayProgressBar(
                        "NPC persistent IDs",
                        $"Checking {scenePath}",
                        (float)index / scenePaths.Length);

                    Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    scenesToCheck.Add(scene);
                }

                // All scenes stay loaded until every identity has been checked, so a duplicate
                // aborts before any generated IDs are written to disk.
                foreach (Scene scene in scenesToCheck)
                {
                    bool changed = AssignMissingIds(scene, identities);
                    // Opening an old scene can invoke OnValidate, which assigns
                    // the missing ID before AssignMissingIds reaches the scope.
                    // The dirty flag covers that valid editor-side assignment.
                    if (changed || scene.isDirty)
                    {
                        if (!EditorSceneManager.SaveScene(scene))
                        {
                            throw new InvalidOperationException($"Could not save NPC persistent IDs in scene '{scene.path}'.");
                        }

                        updatedSceneCount++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"NPC persistent-ID migration completed. Updated {updatedSceneCount} scene(s).");
        }

        private static bool AssignMissingIds(Scene scene, IDictionary<string, string> identities)
        {
            bool changed = false;
            foreach (NpcLifetimeScope scope in scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<NpcLifetimeScope>(true)))
            {
                changed |= scope.EnsurePersistentCharacterIdInEditor();

                string id = scope.PersistentCharacterId;
                if (string.IsNullOrWhiteSpace(id))
                {
                    throw new InvalidOperationException(
                        $"NPC '{GetHierarchyPath(scope.transform)}' in scene '{scene.path}' has no persistent ID.");
                }

                if (!IsUniqueCharacter(scope) && !Guid.TryParse(id, out _))
                {
                    throw new InvalidOperationException(
                        $"NPC '{GetHierarchyPath(scope.transform)}' in scene '{scene.path}' has a non-GUID persistent ID.");
                }

                string owner = $"{scene.path}:{GetHierarchyPath(scope.transform)}";
                if (identities.TryGetValue(id, out string previousOwner))
                {
                    throw new InvalidOperationException(
                        $"Persistent NPC ID '{id}' is used by both '{previousOwner}' and '{owner}'.");
                }

                identities.Add(id, owner);
            }

            return changed;
        }

        private static bool IsUniqueCharacter(NpcLifetimeScope scope)
        {
            // A unique character is permitted to use CharacterInfo.characterId. Normal NPCs
            // must always use the per-instance GUID assigned by the scope itself.
            var serializedScope = new SerializedObject(scope);
            var characterInfo = serializedScope.FindProperty("characterInfo")?.objectReferenceValue as Character.CharacterInfo;
            return characterInfo != null && characterInfo.IsUniqueCharacter;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var names = new Stack<string>();
            for (Transform current = transform; current != null; current = current.parent)
            {
                names.Push(current.name);
            }

            return string.Join("/", names);
        }
    }
}
