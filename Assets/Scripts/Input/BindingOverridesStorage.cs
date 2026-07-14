using UnityEngine;
using UnityEngine.InputSystem;

namespace Input
{
    public static class BindingOverridesStorage
    {
        private const string PlayerPrefsKey = "InputBindingOverrides.v1";

        public static void Load(InputActionAsset inputActions)
        {
            if (inputActions == null || !PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                return;
            }

            var overridesJson = PlayerPrefs.GetString(PlayerPrefsKey);
            if (!string.IsNullOrWhiteSpace(overridesJson))
            {
                inputActions.LoadBindingOverridesFromJson(overridesJson);
            }
        }

        public static void Save(InputActionAsset inputActions)
        {
            if (inputActions == null)
            {
                return;
            }

            PlayerPrefs.SetString(PlayerPrefsKey, inputActions.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public static void Reset(InputActionAsset inputActions)
        {
            if (inputActions == null)
            {
                return;
            }

            inputActions.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            PlayerPrefs.Save();
        }
    }
}
