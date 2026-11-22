using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.EditMode
{
    public sealed class GrassDeformationLifecycleEditModeTests
    {
        [Test]
        public void SourceRegistersOnceAndReleasesAnchorWhenItsScopeEnds()
        {
            var playerObject = new GameObject("Grass lifecycle player");
            var npcObject = new GameObject("Grass lifecycle NPC");
            var registry = Activator.CreateInstance(RuntimeType("GrassDeformationRegistry"));
            var player = Activator.CreateInstance(RuntimeType("GrassDeformationSource"),
                playerObject.AddComponent<CharacterController>(), registry, true);
            var npc = Activator.CreateInstance(RuntimeType("GrassDeformationSource"),
                npcObject.AddComponent<CharacterController>(), registry, false);
            try
            {
                Assert.That(SourceCount(registry), Is.Zero);
                Call(player, "Start");
                Call(player, "Start");
                Call(npc, "Start");
                Assert.That(SourceCount(registry), Is.EqualTo(2));
                Assert.That(Property(registry, "Anchor"), Is.SameAs(player));

                playerObject.SetActive(false);
                Assert.That(Property(player, "IsActive"), Is.False);
                playerObject.SetActive(true);
                Assert.That(Property(player, "IsActive"), Is.True);

                ((IDisposable)npc).Dispose();
                Assert.That(SourceCount(registry), Is.EqualTo(1));
                Assert.That(Property(registry, "Anchor"), Is.SameAs(player));
                ((IDisposable)player).Dispose();
                ((IDisposable)player).Dispose();
                Call(player, "Start");
                Assert.That(SourceCount(registry), Is.Zero);
                Assert.That(Property(registry, "Anchor"), Is.Null);
                Assert.That(Property(player, "IsActive"), Is.False);
            }
            finally
            {
                ((IDisposable)npc).Dispose();
                ((IDisposable)player).Dispose();
                Object.DestroyImmediate(npcObject);
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void SourceReadsLiveConfigAndResetsSweepAfterCharacterWasInactive()
        {
            var character = new GameObject("Grass source contact");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var config = ScriptableObject.CreateInstance(RuntimeType("GrassDeformationConfig"));
            var registry = Activator.CreateInstance(RuntimeType("GrassDeformationRegistry"));
            var controller = character.AddComponent<CharacterController>();
            var source = Activator.CreateInstance(RuntimeType("GrassDeformationSource"), controller, registry, true);
            try
            {
                // Keep physics isolated from the currently open scene's authored geometry.
                ground.transform.position = new Vector3(10000, -0.5f, 10000);
                ground.transform.localScale = new Vector3(10, 1, 10);
                character.transform.position = new Vector3(10000, 0.1f, 10000);
                controller.height = 1.8f;
                controller.radius = 0.25f;
                controller.center = new Vector3(0, 0.9f, 0);
                Physics.SyncTransforms();
                controller.Move(Vector3.down * 0.2f);
                Assert.That(controller.isGrounded, Is.True);
                SetConfig(config, "SourceRadiusMultiplier", 2.4f);
                SetConfig(config, "SourceStrength", 0.35f);
                Call(source, "Start");

                var sample = Sample(source, config);
                Assert.That(SampleField<float>(sample, "Radius"), Is.EqualTo(0.6f).Within(0.001f));
                Assert.That(SampleField<float>(sample, "Strength"), Is.EqualTo(0.35f));

                SetConfig(config, "SourceRadiusMultiplier", 3f);
                SetConfig(config, "SourceStrength", 0.6f);
                sample = Sample(source, config);
                Assert.That(SampleField<float>(sample, "Radius"), Is.EqualTo(0.75f).Within(0.001f));
                Assert.That(SampleField<float>(sample, "Strength"), Is.EqualTo(0.6f));

                character.SetActive(false);
                var arguments = new object[] { config, null };
                Assert.That(source.GetType().GetMethod("TrySample").Invoke(source, arguments), Is.False);
                character.transform.position += Vector3.right * 0.1f;
                character.SetActive(true);
                Physics.SyncTransforms();
                controller.Move(Vector3.down * 0.2f);
                sample = Sample(source, config);
                Assert.That(SampleField<Vector3>(sample, "Previous"),
                    Is.EqualTo(SampleField<Vector3>(sample, "Current")));

                SetConfig(config, "SourceStrength", 0f);
                Assert.That(source.GetType().GetMethod("TrySample").Invoke(source, arguments), Is.False);
            }
            finally
            {
                ((IDisposable)source).Dispose();
                Object.DestroyImmediate(character);
                Object.DestroyImmediate(ground);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void DisabledFieldDoesNotInitializeAndCanBeToggledAfterInitializationFailure()
        {
            // Runtime code is in Assembly-CSharp, matching the project's other reflection-based tests.
            var config = ScriptableObject.CreateInstance(RuntimeType("GrassDeformationConfig"));
            var registry = Activator.CreateInstance(RuntimeType("GrassDeformationRegistry"));
            var fieldType = RuntimeType("GrassDeformationField");
            var field = Activator.CreateInstance(fieldType, config, registry);
            var enabled = config.GetType().GetField("<Enabled>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(enabled, Is.Not.Null);
            try
            {
                enabled.SetValue(config, false);
                fieldType.GetMethod("Start").Invoke(field, null);
                fieldType.GetMethod("LateTick").Invoke(field, null);
                Assert.That(fieldType.GetProperty("CurrentField").GetValue(field), Is.Null);
                Assert.That(fieldType.GetProperty("ActiveSourceCount").GetValue(field), Is.EqualTo(0));

                // No compute asset is assigned: enabling must attempt initialization once,
                // then remain quiet. Toggling off/on must permit a new attempt.
                enabled.SetValue(config, true);
                LogAssert.Expect(LogType.Warning, new Regex("^Grass deformation is unavailable:"));
                fieldType.GetMethod("LateTick").Invoke(field, null);
                fieldType.GetMethod("LateTick").Invoke(field, null);
                enabled.SetValue(config, false);
                fieldType.GetMethod("LateTick").Invoke(field, null);
                enabled.SetValue(config, true);
                LogAssert.Expect(LogType.Warning, new Regex("^Grass deformation is unavailable:"));
                fieldType.GetMethod("LateTick").Invoke(field, null);
                Assert.That(fieldType.GetProperty("CurrentField").GetValue(field), Is.Null);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                ((IDisposable)field).Dispose();
                Object.DestroyImmediate(config);
            }
        }

        private static Type RuntimeType(string name)
        {
            var type = Type.GetType($"World.Grass.{name}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null);
            return type;
        }

        private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);

        private static int SourceCount(object registry) => (int)Property(Property(registry, "Sources"), "Count");

        private static void Call(object target, string name) => target.GetType().GetMethod(name).Invoke(target, null);

        private static void SetConfig(ScriptableObject config, string name, object value) => config.GetType()
            .GetField($"<{name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, value);

        private static T SampleField<T>(object sample, string name) => (T)sample.GetType().GetField(name).GetValue(sample);

        private static object Sample(object source, ScriptableObject config)
        {
            var arguments = new object[] { config, null };
            Assert.That(source.GetType().GetMethod("TrySample").Invoke(source, arguments), Is.True);
            return arguments[1];
        }
    }
}
