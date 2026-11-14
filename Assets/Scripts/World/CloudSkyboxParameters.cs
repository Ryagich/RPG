using UnityEngine;

namespace World
{
    internal static class CloudSkyboxParameters
    {
        private static readonly string[] FloatProperties =
        {
            "_SkyGradient", "_CloudProjectionBlend", "_CloudDomeCurvature", "_CloudVolume",
            "_CloudLighting", "_CloudRimStrength", "_FacetContrast", "_CloudHorizonFade",
            "_CloudBlur", "_CloudBlurRadius", "_CloudScale",
            "_CloudCoverageMin", "_CloudCoverageMax", "_UpperCloudCoverageMin", "_UpperCloudCoverageMax",
            "_CloudOpacity", "_WindDirection", "_WindSpeed", "_CloudWindStretch",
            "_UpperCloudScale",
            "_UpperCloudOpacity", "_UpperCloudWindDirection", "_UpperCloudWindSpeed",
            "_UpperCloudWindStretch", "_UpperCloudHaze", "_SunRadius", "_SunHaloSize", "_SunHaloIntensity"
        };

        public static bool HaveChanges(Material source, Material target)
        {
            if (source == null || target == null)
                return false;
            foreach (string property in FloatProperties)
                if (source.GetFloat(property) != target.GetFloat(property))
                    return true;
            return source.GetVector("_CloudOffset") != target.GetVector("_CloudOffset")
                || source.GetVector("_UpperCloudOffset") != target.GetVector("_UpperCloudOffset")
                || source.GetTexture("_CloudField") != target.GetTexture("_CloudField")
                || source.GetTexture("_UpperCloudField") != target.GetTexture("_UpperCloudField")
                || source.GetVector("_CloudFieldParams") != target.GetVector("_CloudFieldParams")
                || source.GetVector("_UpperCloudFieldParams") != target.GetVector("_UpperCloudFieldParams");
        }

        public static void Apply(Material source, Material target)
        {
            foreach (string property in FloatProperties)
            {
                float value = source.GetFloat(property);
                if (value != target.GetFloat(property))
                    target.SetFloat(property, value);
            }
            target.SetVector("_CloudOffset", source.GetVector("_CloudOffset"));
            target.SetVector("_UpperCloudOffset", source.GetVector("_UpperCloudOffset"));
            target.SetTexture("_CloudField", source.GetTexture("_CloudField"));
            target.SetTexture("_UpperCloudField", source.GetTexture("_UpperCloudField"));
            target.SetVector("_CloudFieldParams", source.GetVector("_CloudFieldParams"));
            target.SetVector("_UpperCloudFieldParams", source.GetVector("_UpperCloudFieldParams"));
            SetKeyword(target, "_CLOUD_BLUR", source.GetFloat("_CloudBlur") > 0.5f);
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (material.IsKeywordEnabled(keyword) == enabled)
                return;
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }
    }
}
