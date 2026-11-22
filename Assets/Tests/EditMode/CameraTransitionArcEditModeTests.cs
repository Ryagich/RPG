using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Both directions of the dialogue transition must stay outside the CharacterController by
    /// the configured avoidance radius.
    /// </summary>
    public sealed class CameraTransitionArcEditModeTests
    {
        [Test]
        public void RoutesAroundPlayerWhenReturnStartsInsideAvoidanceRadius()
        {
            AssertRoutesAroundPlayer(new Vector3(0f, 1.6f, 0.5f), new Vector3(0f, 1.6f, -2f));
        }

        [Test]
        public void RoutesAroundPlayerWhenDialogueViewEndsInsideAvoidanceRadius()
        {
            AssertRoutesAroundPlayer(new Vector3(0f, 1.6f, -2f), new Vector3(0f, 1.6f, 0.5f));
        }

        private static void AssertRoutesAroundPlayer(Vector3 startPosition, Vector3 destination)
        {
            var player = new GameObject("CameraTransitionArcTestPlayer");

            try
            {
                var controller = player.AddComponent<CharacterController>();
                controller.radius = 0.3f;
                controller.height = 1.8f;
                controller.center = new Vector3(0f, 0.9f, 0f);

                var constructor = GetRuntimeType("CameraScripts.CameraTransitionArc").GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(CharacterController) },
                    null);
                Assert.That(constructor, Is.Not.Null);
                var arc = constructor.Invoke(new object[] { controller });
                var arguments = new object[]
                {
                    startPosition,
                    destination,
                    0.6f,
                    1f / 60f,
                    0.35f,
                    0.999f,
                    Vector3.zero
                };

                var usesArc = (bool)arc.GetType().InvokeMember(
                    "TryGetNextPosition",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod,
                    null,
                    arc,
                    arguments);

                Assert.That(usesArc, Is.True);
                Assert.That((Vector3)arguments[6], Is.Not.EqualTo(destination));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static Type GetRuntimeType(string typeName)
        {
            return Type.GetType($"{typeName}, Assembly-CSharp")
                   ?? throw new InvalidOperationException($"Runtime type '{typeName}' was not found.");
        }
    }
}
