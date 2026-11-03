using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    public sealed class CloudSkyboxShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            base.OnGUI(materialEditor, properties);
            foreach (Object target in materialEditor.targets)
            {
                var material = (Material)target;
                if (material.GetTexture("_CloudField") == null || material.GetTexture("_UpperCloudField") == null
                    || material.GetVector("_CloudFieldParams").z < 0.5f
                    || material.GetVector("_UpperCloudFieldParams").z < 0.5f)
                {
                    EditorGUILayout.HelpBox("Clouds require a bake. Prepare them in Cloud Field Baker and press Bake and Assign.", MessageType.Warning);
                    break;
                }
            }
            if (GUILayout.Button("Open Cloud Field Baker"))
                CloudFieldBakerWindow.Open();
        }
    }
}
