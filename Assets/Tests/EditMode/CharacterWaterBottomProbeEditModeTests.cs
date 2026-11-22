using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Guards the boundary where a shallow-water floor could otherwise be mistaken for deep water.
    /// </summary>
    public sealed class CharacterWaterBottomProbeEditModeTests
    {
        private const string WaterConfigPath = "Assets/Configs/Water/CharacterWaterConfig.asset";

        [Test]
        public void RecognizesBottomWithinConfiguredWadingDepth()
        {
            var character = new GameObject("WaterBottomProbeTestCharacter");
            var bottom = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bottom.name = "WaterBottomProbeTestBottom";

            try
            {
                // The configured wading range ends at SwimEnter (1.3 m), not at
                // WadingEnter (1.1 m). A 1.2 m floor is still valid wading ground.
                bottom.transform.position = new Vector3(0f, -1.25f, 0f);
                bottom.transform.localScale = new Vector3(10f, 0.1f, 10f);
                Physics.SyncTransforms();

                var tracker = (Component)character.AddComponent(GetRuntimeType("Movement.CharacterWaterContactTracker"));
                var waterConfig = AssetDatabase.LoadAssetAtPath<ScriptableObject>(WaterConfigPath);
                Assert.That(waterConfig, Is.Not.Null, $"Water config was not found at '{WaterConfigPath}'.");

                var probe = Activator.CreateInstance(
                    GetRuntimeType("Movement.CharacterWaterBottomProbe"),
                    tracker,
                    waterConfig);
                var hasStandableBottom = (bool)Invoke(probe, "HasStandableBottom", 0f);

                Assert.That(hasStandableBottom, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bottom);
                UnityEngine.Object.DestroyImmediate(character);
            }
        }

        private static Type GetRuntimeType(string typeName)
        {
            return Type.GetType($"{typeName}, Assembly-CSharp")
                   ?? throw new InvalidOperationException($"Runtime type '{typeName}' was not found.");
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            return target.GetType().InvokeMember(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod,
                null,
                target,
                arguments);
        }
    }
}
