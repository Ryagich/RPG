using Combat;
using Player;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    public static class BodyHitboxSetupMenu
    {
        private const string MenuPath = "Tools/RPG/Combat/Setup Body Hitboxes On Selection";

        [MenuItem(MenuPath, true)]
        private static bool ValidateSetupBodyHitboxes()
        {
            return Selection.gameObjects.Length > 0;
        }

        [MenuItem(MenuPath)]
        private static void SetupBodyHitboxes()
        {
            var configuredHitboxes = 0;

            foreach (var selectedObject in Selection.gameObjects)
            {
                if (selectedObject == null)
                {
                    continue;
                }

                var controller = selectedObject.GetComponent<PlayerRagdollController>();
                if (controller == null)
                {
                    controller = Undo.AddComponent<PlayerRagdollController>(selectedObject);
                    EditorUtility.SetDirty(selectedObject);
                }

                configuredHitboxes += SetupBodyHitboxes(selectedObject);
                EditorUtility.SetDirty(controller);
            }

            Debug.Log($"Setup Body Hitboxes configured {configuredHitboxes} hitboxes.");
        }

        private static int SetupBodyHitboxes(GameObject root)
        {
            var configuredHitboxes = 0;
            var colliders = root.GetComponentsInChildren<Collider>(true);

            foreach (var collider in colliders)
            {
                if (collider == null
                    || collider.transform == root.transform
                    || collider.GetComponent<CharacterController>() != null)
                {
                    continue;
                }

                Undo.RecordObject(collider, "Setup Body Hitboxes");
                collider.isTrigger = true;
                EditorUtility.SetDirty(collider);

                var hitbox = collider.GetComponent<BodyHitbox>();
                var wasCreated = hitbox == null;
                if (wasCreated)
                {
                    hitbox = Undo.AddComponent<BodyHitbox>(collider.gameObject);
                }
                else
                {
                    Undo.RecordObject(hitbox, "Setup Body Hitboxes");
                }

                hitbox.ConfigureDefaults(PlayerRagdollController.InferBodyPart(collider.transform.name), wasCreated);
                EditorUtility.SetDirty(hitbox);
                configuredHitboxes++;
            }

            return configuredHitboxes;
        }
    }
}
