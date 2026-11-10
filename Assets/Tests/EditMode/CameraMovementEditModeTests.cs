using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace Tests.EditMode
{
    /// <summary>
    /// Guards the gameplay camera invariant: its focus point must never lag behind the player.
    /// </summary>
    public sealed class CameraMovementEditModeTests
    {
        private const string CameraConfigPath = "Assets/Configs/Camera/CameraConfig.asset";

        [Test]
        public void GameplayOrbitKeepsCurrentPlayerFocusCenteredAfterRapidTurnAndTargetMove()
        {
            var player = new GameObject("CameraMovementTestPlayer");
            var cameraObject = new GameObject("CameraMovementTestCamera");
            var targetLockConfig = ScriptableObject.CreateInstance(GetRuntimeType("TargetLock.TargetLockConfig"));

            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 60f;
                var cameraConfig = AssetDatabase.LoadAssetAtPath<ScriptableObject>(CameraConfigPath);
                Assert.That(cameraConfig, Is.Not.Null, $"Camera config was not found at '{CameraConfigPath}'.");

                var movement = Create(
                    "CameraScripts.CameraMovement",
                    cameraConfig,
                    targetLockConfig,
                    camera.transform,
                    player.transform,
                    player.transform);

                Invoke(movement, "Tick", 1f / 60f);
                Invoke(movement, "AddLookInput", new Vector2(100000f, 0f));
                Invoke(movement, "Tick", 1f / 60f);
                AssertFocusIsCentered(camera, player.transform, cameraConfig, movement);

                player.transform.position = new Vector3(10f, 0f, -7f);
                Invoke(movement, "Tick", 1f / 60f);
                AssertFocusIsCentered(camera, player.transform, cameraConfig, movement);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(targetLockConfig);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static void AssertFocusIsCentered(
            Camera camera,
            Transform player,
            ScriptableObject cameraConfig,
            object movement)
        {
            var pivotOffset = GetProperty<Vector3>(cameraConfig, "PivotOffset");
            var shoulderOffset = GetProperty<float>(cameraConfig, "ShoulderOffset");
            var planarRotation = (Quaternion)Invoke(movement, "GetPlanarRotation");
            var focusPoint = player.position + pivotOffset + planarRotation * (Vector3.right * shoulderOffset);
            var viewportPoint = camera.WorldToViewportPoint(focusPoint);

            Assert.That(viewportPoint.z, Is.GreaterThan(0f));
            Assert.That(viewportPoint.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(viewportPoint.y, Is.EqualTo(0.5f).Within(0.001f));
        }

        private static object Create(string typeName, params object[] arguments)
        {
            return Activator.CreateInstance(GetRuntimeType(typeName), arguments);
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

        private static T GetProperty<T>(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            return property != null
                ? (T)property.GetValue(target)
                : throw new InvalidOperationException($"Property '{propertyName}' was not found.");
        }
    }
}
