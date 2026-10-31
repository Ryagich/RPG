using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace World
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class DayNightCycle : MonoBehaviour
    {
        private enum LightPathMode
        {
            FullOrbit,
            ReturnAlongDayArc,
            NightCurve
        }

        [Serializable]
        private struct LightingSettings
        {
            public float sunRotation;
            [Min(0f)] public float lightIntensity;
            [Min(0f)] public float skyExposure;
            [Min(0f)] public float sunDiskIntensity;
            public float postExposure;
            [Range(0f, 1f)] public float fogDensity;
            [Min(0.05f)] public float fogAttenuationDistance;
            public Color sunlight;
            public Color zenith;
            public Color horizon;
            public Color cloudLight;
            public Color cloudShadow;
            public Color ambient;
            public Color ground;
        }

        [Header("References")]
        [SerializeField] private Light sun;
        [Tooltip("Material using RPG/Skybox/Low Poly Clouds. A temporary copy is used.")]
        [SerializeField] private Material skyboxMaterial;
        [Tooltip("Optional scene Volume with Color Adjustments and Volumetric Fog.")]
        [SerializeField] private Volume volume;

        [Header("Clock")]
        [Tooltip("0 = midnight, 0.25 = dawn, 0.5 = day, 0.75 = dusk, 1 = midnight.")]
        [SerializeField, Range(0f, 1f)] private float timeOfDay = 0.35f;
        [SerializeField, Min(1f)] private float cycleDuration = 120f;
        [Tooltip("Advance time in Play Mode. In the editor, use Time Of Day manually.")]
        [SerializeField] private bool animate = true;
#if UNITY_EDITOR
        [SerializeField] private bool previewInEditor = true;
#endif
        [SerializeField, Range(0f, 360f)] private float sunAzimuth = 25f;

        [Header("Light path and visible sun")]
        [Tooltip("Night Curve controls the light's night pitch directly. Night Sun Rotation from the four presets is used only by Full Orbit.")]
        [SerializeField] private LightPathMode lightPath = LightPathMode.NightCurve;
        [Tooltip("X: 0 = dusk, 0.5 = midnight, 1 = dawn. Y: pitch in degrees, without angle wrapping. Match the endpoints to Dusk/Dawn Sun Rotation for continuous transitions.")]
        [SerializeField] private AnimationCurve nightSunRotation = new AnimationCurve(
            new Keyframe(0f, 180f, -180f, -180f),
            new Keyframe(0.5f, 90f, -180f, -180f),
            new Keyframe(1f, 0f, -180f, -180f));
        [SerializeField] private bool hideSunDiskAtNight = true;
        [Tooltip("Sun disk/halo fade duration as a fraction of a full cycle, inside dawn and dusk.")]
        [SerializeField, Range(0f, 0.125f)] private float sunDiskFade = 0.025f;

        [Header("Four lighting values (smoothly interpolated)")]
        [SerializeField] private LightingSettings night = new LightingSettings
        {
            sunRotation = -90f, lightIntensity = 0.2f, skyExposure = 0.95f, sunDiskIntensity = 0f,
            postExposure = 1.1f, fogDensity = 0.02f, fogAttenuationDistance = 220f,
            sunlight = new Color(0.38f, 0.48f, 0.7f), zenith = new Color(0.08f, 0.12f, 0.23f),
            horizon = new Color(0.18f, 0.24f, 0.34f), cloudLight = new Color(0.28f, 0.34f, 0.46f),
            cloudShadow = new Color(0.12f, 0.17f, 0.27f), ambient = new Color(0.2f, 0.25f, 0.34f),
            ground = new Color(0.11f, 0.15f, 0.22f)
        };
        [SerializeField] private LightingSettings dawn = new LightingSettings
        {
            sunRotation = 0f, lightIntensity = 0.08f, skyExposure = 1.05f, sunDiskIntensity = 0.4f,
            postExposure = 0.8f, fogDensity = 0.028f, fogAttenuationDistance = 150f,
            sunlight = new Color(1f, 0.4f, 0.15f), zenith = new Color(0.21f, 0.17f, 0.35f),
            horizon = new Color(0.95f, 0.4f, 0.22f), cloudLight = new Color(0.95f, 0.46f, 0.3f),
            cloudShadow = new Color(0.26f, 0.19f, 0.28f), ambient = new Color(0.3f, 0.23f, 0.24f),
            ground = new Color(0.18f, 0.14f, 0.16f)
        };
        [SerializeField] private LightingSettings day = new LightingSettings
        {
            sunRotation = 90f, lightIntensity = 1.2f, skyExposure = 1f, sunDiskIntensity = 4f,
            postExposure = 0.6f, fogDensity = 0.031f, fogAttenuationDistance = 128f,
            sunlight = new Color(1f, 0.96f, 0.86f), zenith = new Color(0.12f, 0.36f, 0.68f),
            horizon = new Color(0.65f, 0.81f, 0.88f), cloudLight = new Color(1f, 0.97f, 0.88f),
            cloudShadow = new Color(0.42f, 0.54f, 0.66f), ambient = new Color(0.55f, 0.65f, 0.75f),
            ground = new Color(0.275f, 0.325f, 0.375f)
        };
        [SerializeField] private LightingSettings dusk = new LightingSettings
        {
            sunRotation = 180f, lightIntensity = 0.06f, skyExposure = 1.02f, sunDiskIntensity = 0.32f,
            postExposure = 0.8f, fogDensity = 0.029f, fogAttenuationDistance = 155f,
            sunlight = new Color(1f, 0.33f, 0.15f), zenith = new Color(0.26f, 0.2f, 0.38f),
            horizon = new Color(0.88f, 0.33f, 0.23f), cloudLight = new Color(0.9f, 0.4f, 0.32f),
            cloudShadow = new Color(0.29f, 0.18f, 0.27f), ambient = new Color(0.35f, 0.25f, 0.3f),
            ground = new Color(0.19f, 0.14f, 0.18f)
        };

        private Light controlledSun;
        private Material sourceSkybox;
        private Material runtimeSkybox;
        private Material previousSkybox;
        private Light previousSun;
        private Quaternion previousRotation;
        private Color previousLightColor;
        private float previousIntensity;
        private AmbientMode previousAmbientMode;
        private Color previousAmbientColor;
        private Scene settingsScene;
        private bool lightingInitialized;
        private Volume controlledVolume;
        private VolumeProfile sourceSharedProfile;
        private VolumeProfile previousVolumeProfile;
        private VolumeProfile runtimeVolumeProfile;
        private ColorAdjustments colorAdjustments;
        private VolumetricFogVolumeComponent volumetricFog;

        private void OnEnable()
        {
#if UNITY_EDITOR
            EditorApplication.update += EditorPreview;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseLighting;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            Undo.undoRedoPerformed += RequestPreview;
            RequestPreview();
#endif
            if (Application.IsPlaying(gameObject))
                InitializeLighting(true);
        }

        private bool InitializeLighting(bool logErrors)
        {
            if (sun == null || sun.type != LightType.Directional || skyboxMaterial == null
                || skyboxMaterial.shader == null || skyboxMaterial.shader.name != "RPG/Skybox/Low Poly Clouds")
            {
                if (logErrors)
                {
                    Debug.LogError("DayNightCycle needs a Directional Light and a Low Poly Clouds skybox material.", this);
                    enabled = false;
                }
                return false;
            }
            controlledSun = sun;
            sourceSkybox = skyboxMaterial;
            previousRotation = controlledSun.transform.localRotation;
            previousLightColor = controlledSun.color;
            previousIntensity = controlledSun.intensity;
            settingsScene = SceneManager.GetActiveScene();
            previousSkybox = RenderSettings.skybox;
            previousSun = RenderSettings.sun;
            previousAmbientMode = RenderSettings.ambientMode;
            previousAmbientColor = RenderSettings.ambientLight;
            runtimeSkybox = new Material(skyboxMaterial)
            {
                name = skyboxMaterial.name + " (Day Night Preview)", hideFlags = HideFlags.DontSave
            };
            RenderSettings.skybox = runtimeSkybox;
            RenderSettings.sun = controlledSun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            lightingInitialized = true;
            InitializeVolume(logErrors);
            ApplyLighting();
            return true;
        }

        private void Update()
        {
            if (!Application.IsPlaying(gameObject))
                return;
            if (NeedsRebind())
            {
                ReleaseLighting();
                if (!InitializeLighting(true))
                    return;
            }
            if (animate)
                timeOfDay = Mathf.Repeat(timeOfDay + Time.deltaTime / Mathf.Max(1f, cycleDuration), 1f);
            ApplyLighting();
        }

        private bool NeedsRebind()
        {
            return !lightingInitialized || controlledSun == null || controlledSun != sun
                || sourceSkybox != skyboxMaterial || controlledVolume != volume || runtimeSkybox == null
                || (volume != null && sourceSharedProfile != volume.sharedProfile);
        }

        private LightingSettings EvaluateLighting()
        {
            float phase = Mathf.Repeat(timeOfDay, 1f) * 4f;
            int index = Mathf.FloorToInt(phase);
            LightingSettings from = index == 0 ? night : index == 1 ? dawn : index == 2 ? day : dusk;
            LightingSettings to = index == 0 ? dawn : index == 1 ? day : index == 2 ? dusk : night;
            float fraction = phase - index;
            float t = Mathf.SmoothStep(0f, 1f, fraction);
            return new LightingSettings
            {
                sunRotation = EvaluateSunRotation(phase * 0.25f, from.sunRotation, to.sunRotation, fraction),
                lightIntensity = Mathf.Lerp(from.lightIntensity, to.lightIntensity, t),
                skyExposure = Mathf.Lerp(from.skyExposure, to.skyExposure, t),
                sunDiskIntensity = Mathf.Lerp(from.sunDiskIntensity, to.sunDiskIntensity, t),
                postExposure = Mathf.Lerp(from.postExposure, to.postExposure, t),
                fogDensity = Mathf.Lerp(from.fogDensity, to.fogDensity, t),
                fogAttenuationDistance = Mathf.Lerp(from.fogAttenuationDistance, to.fogAttenuationDistance, t),
                sunlight = Color.Lerp(from.sunlight, to.sunlight, t),
                zenith = Color.Lerp(from.zenith, to.zenith, t),
                horizon = Color.Lerp(from.horizon, to.horizon, t),
                cloudLight = Color.Lerp(from.cloudLight, to.cloudLight, t),
                cloudShadow = Color.Lerp(from.cloudShadow, to.cloudShadow, t),
                ambient = Color.Lerp(from.ambient, to.ambient, t),
                ground = Color.Lerp(from.ground, to.ground, t)
            };
        }

        private float EvaluateSunRotation(float time, float from, float to, float fraction)
        {
            if (lightPath == LightPathMode.NightCurve)
            {
                if (time <= 0.25f || time >= 0.75f)
                {
                    return nightSunRotation.Evaluate(GetNightProgress(time));
                }
                return Mathf.LerpAngle(from, to, fraction);
            }
            if (lightPath == LightPathMode.FullOrbit)
                return Mathf.LerpAngle(from, to, fraction);

            // Sample the same daytime trajectory in reverse at night. Do not
            // interpolate Dusk -> Night rotation: angular wrapping could choose
            // the lower half of the orbit, and Night is an appearance preset here.
            float dayTime = time < 0.25f ? 0.5f - time : time > 0.75f ? 1.5f - time : time;
            if (dayTime <= 0.5f)
                return Mathf.LerpAngle(dawn.sunRotation, day.sunRotation, (dayTime - 0.25f) * 4f);
            return Mathf.LerpAngle(day.sunRotation, dusk.sunRotation, (dayTime - 0.5f) * 4f);
        }

        private static float GetNightProgress(float time)
        {
            return time >= 0.75f ? (time - 0.75f) * 2f : (time + 0.25f) * 2f;
        }

        private float EvaluateSunDiskVisibility()
        {
            if (!hideSunDiskAtNight)
                return 1f;
            float time = Mathf.Repeat(timeOfDay, 1f);
            if (time <= 0.25f || time >= 0.75f)
                return 0f;
            if (sunDiskFade <= 0f)
                return 1f;
            return Mathf.SmoothStep(0f, 1f, (time - 0.25f) / sunDiskFade)
                * Mathf.SmoothStep(0f, 1f, (0.75f - time) / sunDiskFade);
        }

        private void ApplyLighting()
        {
            if (controlledSun == null || runtimeSkybox == null)
                return;
            LightingSettings value = EvaluateLighting();
            controlledSun.transform.rotation = Quaternion.Euler(value.sunRotation, sunAzimuth, 0f);
            Vector3 direction = -controlledSun.transform.forward;
            controlledSun.color = value.sunlight;
            controlledSun.intensity = Mathf.Max(0f, value.lightIntensity);
            runtimeSkybox.SetFloat("_UseMainLight", 0f);
            runtimeSkybox.SetFloat("_SunAzimuth", Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg);
            runtimeSkybox.SetFloat("_SunElevation", Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg);
            runtimeSkybox.SetColor("_SunColor", value.sunlight);
            runtimeSkybox.SetFloat("_SunIntensity", Mathf.Max(0f, value.sunDiskIntensity));
            runtimeSkybox.SetFloat("_SunDiskVisibility", EvaluateSunDiskVisibility());
            runtimeSkybox.SetFloat("_Exposure", Mathf.Max(0f, value.skyExposure));
            runtimeSkybox.SetColor("_ZenithColor", value.zenith);
            runtimeSkybox.SetColor("_HorizonColor", value.horizon);
            runtimeSkybox.SetColor("_CloudLightColor", value.cloudLight);
            runtimeSkybox.SetColor("_CloudShadowColor", value.cloudShadow);
            runtimeSkybox.SetColor("_GroundColor", value.ground);
            RenderSettings.ambientLight = value.ambient;
            if (colorAdjustments != null)
                colorAdjustments.postExposure.value = value.postExposure;
            if (volumetricFog != null)
            {
                volumetricFog.density.value = value.fogDensity;
                volumetricFog.attenuationDistance.value = value.fogAttenuationDistance;
            }
        }

        private void InitializeVolume(bool logWarnings)
        {
            controlledVolume = volume;
            sourceSharedProfile = volume != null ? volume.sharedProfile : null;
            if (volume == null)
                return;
            previousVolumeProfile = volume.HasInstantiatedProfile() ? volume.profile : null;
            VolumeProfile source = previousVolumeProfile != null ? previousVolumeProfile : sourceSharedProfile;
            if (source == null)
            {
                if (logWarnings)
                    Debug.LogWarning("DayNightCycle: the Volume has no profile; volume preview is skipped.", this);
                return;
            }
            runtimeVolumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            runtimeVolumeProfile.name = source.name + " (Day Night Preview)";
            runtimeVolumeProfile.hideFlags = HideFlags.DontSave;
            foreach (VolumeComponent component in source.components)
            {
                if (component == null)
                    continue;
                VolumeComponent copy = Instantiate(component);
                copy.hideFlags = HideFlags.DontSave;
                runtimeVolumeProfile.components.Add(copy);
            }
            controlledVolume.profile = runtimeVolumeProfile;
            if (runtimeVolumeProfile.TryGet(out colorAdjustments))
            {
                colorAdjustments.active = true;
                colorAdjustments.postExposure.overrideState = true;
            }
            else if (logWarnings)
                Debug.LogWarning("DayNightCycle: the Volume profile needs Color Adjustments for Post Exposure.", this);
            if (runtimeVolumeProfile.TryGet(out volumetricFog))
            {
                volumetricFog.active = true;
                volumetricFog.enabled.Override(true);
                volumetricFog.density.overrideState = true;
                volumetricFog.attenuationDistance.overrideState = true;
            }
            else if (logWarnings)
                Debug.LogWarning("DayNightCycle: the Volume profile needs Volumetric Fog for fog control.", this);
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorPreview;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= ReleaseLighting;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            Undo.undoRedoPerformed -= RequestPreview;
            EditorApplication.delayCall -= ResumePreviewAfterSave;
#endif
            ReleaseLighting();
        }

        private void ReleaseLighting()
        {
            if (runtimeVolumeProfile != null)
            {
                if (controlledVolume != null && controlledVolume.HasInstantiatedProfile()
                    && controlledVolume.profile == runtimeVolumeProfile)
                    controlledVolume.profile = previousVolumeProfile;
                foreach (VolumeComponent component in runtimeVolumeProfile.components)
                    DisposePreviewObject(component);
                DisposePreviewObject(runtimeVolumeProfile);
            }
            runtimeVolumeProfile = null;
            previousVolumeProfile = null;
            sourceSharedProfile = null;
            controlledVolume = null;
            colorAdjustments = null;
            volumetricFog = null;
            if (!lightingInitialized)
                return;
            lightingInitialized = false;
            if (controlledSun != null)
            {
                controlledSun.transform.localRotation = previousRotation;
                controlledSun.color = previousLightColor;
                controlledSun.intensity = previousIntensity;
            }
            // Restore the scene which owns the preview, even after an editor scene/stage change.
            Scene activeScene = SceneManager.GetActiveScene();
            if (settingsScene.IsValid() && settingsScene.isLoaded)
            {
                try
                {
                    if (activeScene == settingsScene || SceneManager.SetActiveScene(settingsScene))
                    {
                        if (RenderSettings.skybox == runtimeSkybox)
                            RenderSettings.skybox = previousSkybox;
                        if (RenderSettings.sun == controlledSun)
                            RenderSettings.sun = previousSun;
                        RenderSettings.ambientMode = previousAmbientMode;
                        RenderSettings.ambientLight = previousAmbientColor;
                    }
                }
                finally
                {
                    if (activeScene.IsValid() && activeScene.isLoaded && activeScene != settingsScene)
                        SceneManager.SetActiveScene(activeScene);
                }
            }
            DisposePreviewObject(runtimeSkybox);
            runtimeSkybox = null;
            sourceSkybox = null;
            controlledSun = null;
        }

        private void DisposePreviewObject(UnityEngine.Object value)
        {
            if (value == null)
                return;
            if (Application.IsPlaying(gameObject))
                Destroy(value);
            else
                DestroyImmediate(value);
        }

#if UNITY_EDITOR
        private volatile bool previewRefreshRequested = true;
        private bool savingScene;

        // Loading can call OnValidate off the main thread; only set a flag here.
        private void OnValidate() => RequestPreview();
        private void RequestPreview() => previewRefreshRequested = true;

        private void EditorPreview()
        {
            if (this == null || !isActiveAndEnabled || EditorApplication.isPlayingOrWillChangePlaymode || savingScene)
                return;
            bool canPreview = previewInEditor && gameObject.scene.IsValid() && gameObject.scene.isLoaded
                && gameObject.scene == SceneManager.GetActiveScene()
                && StageUtility.GetCurrentStageHandle() == StageUtility.GetMainStageHandle()
                && StageUtility.GetStageHandle(gameObject) == StageUtility.GetMainStageHandle();
            if (!canPreview)
            {
                ReleaseLighting();
                previewRefreshRequested = true;
                return;
            }
            bool rebind = NeedsRebind();
            if (!previewRefreshRequested && !rebind)
                return;
            previewRefreshRequested = false;
            if (rebind)
            {
                ReleaseLighting();
                if (!InitializeLighting(false))
                    return;
            }
            ApplyLighting();
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
                ReleaseLighting();
            RequestPreview();
        }

        private void OnSceneSaving(Scene scene, string path)
        {
            if (Application.IsPlaying(gameObject))
                return;
            // Never serialize temporary preview references into a scene.
            savingScene = true;
            ReleaseLighting();
            EditorApplication.delayCall -= ResumePreviewAfterSave;
            EditorApplication.delayCall += ResumePreviewAfterSave;
        }

        private void OnSceneSaved(Scene scene) => ResumePreviewAfterSave();
        private void ResumePreviewAfterSave()
        {
            savingScene = false;
            RequestPreview();
        }
#endif
    }
}
