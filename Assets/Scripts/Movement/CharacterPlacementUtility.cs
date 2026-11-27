using UnityEngine;

namespace Movement
{
    /// <summary>
    /// Applies a world pose without letting CharacterController collision correction alter it
    /// during the placement frame.
    /// </summary>
    public static class CharacterPlacementUtility
    {
        public static void SetPose(Transform characterTransform, CharacterController characterController, Pose pose)
        {
            if (characterTransform == null)
            {
                return;
            }

            var wasControllerEnabled = characterController != null && characterController.enabled;
            if (wasControllerEnabled)
            {
                characterController.enabled = false;
            }

            characterTransform.SetPositionAndRotation(pose.position, pose.rotation);
            Physics.SyncTransforms();

            if (wasControllerEnabled)
            {
                characterController.enabled = true;
            }
        }
    }
}
