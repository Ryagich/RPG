using Input;
using UnityEngine.InputSystem;

namespace Training
{
    public static class LessonTextFormatter
    {
        public static string Format(string source, InputConfig inputConfig)
        {
            if (string.IsNullOrEmpty(source))
            {
                return string.Empty;
            }

            return source
                .Replace("{Dodge}", GetBinding(inputConfig?.Dodge))
                .Replace("{Roll}", GetBinding(inputConfig?.Roll))
                .Replace("{LightAttack}", GetBinding(inputConfig?.LeftClick))
                .Replace("{HeavyAttack}", GetBinding(inputConfig?.RightClick));
        }

        private static string GetBinding(InputActionReference actionReference)
        {
            var action = actionReference?.action;
            if (action == null || action.bindings.Count == 0)
            {
                return string.Empty;
            }

            return InputRebinder.GetLocalizedBindingDisplayName(action.bindings[0]);
        }
    }
}
