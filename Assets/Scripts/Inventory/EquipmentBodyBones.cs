using System.Collections.Generic;
using Inventory.Item;
using UnityEngine;

namespace Inventory
{
    public class CharacterVisualRoot : MonoBehaviour
    {
        private readonly Dictionary<BodyPart, Dictionary<string, List<CharacterBodyPartVisual>>> visualsByBodyPart = new();
        private bool isCacheBuilt;

        public bool HasVisual(BodyPart bodyPart, string visualName)
        {
            EnsureCache();
            return !string.IsNullOrWhiteSpace(visualName)
                   && visualsByBodyPart.TryGetValue(bodyPart, out var visualsByName)
                   && visualsByName.ContainsKey(visualName);
        }

        public void ApplyVisuals(IReadOnlyDictionary<BodyPart, string> visualNamesByBodyPart)
        {
            EnsureCache();

            foreach (var bodyPartEntry in visualsByBodyPart)
            {
                visualNamesByBodyPart.TryGetValue(bodyPartEntry.Key, out var targetVisualName);
                foreach (var visualEntry in bodyPartEntry.Value)
                {
                    SetVisualGroupActive(visualEntry.Value, ShouldEnableVisualGroup(visualEntry.Key, targetVisualName));
                }
            }
        }

        private void EnsureCache()
        {
            if (isCacheBuilt)
            {
                return;
            }

            isCacheBuilt = true;
            visualsByBodyPart.Clear();

            foreach (var visual in GetComponentsInChildren<CharacterBodyPartVisual>(true))
            {
                if (visual == null || visual.BodyPart == BodyPart.None || string.IsNullOrWhiteSpace(visual.Name))
                {
                    continue;
                }

                if (!visualsByBodyPart.TryGetValue(visual.BodyPart, out var visualsByName))
                {
                    visualsByName = new Dictionary<string, List<CharacterBodyPartVisual>>();
                    visualsByBodyPart[visual.BodyPart] = visualsByName;
                }

                if (!visualsByName.TryGetValue(visual.Name, out var visuals))
                {
                    visuals = new List<CharacterBodyPartVisual>();
                    visualsByName[visual.Name] = visuals;
                }

                visuals.Add(visual);
            }
        }

        private static void SetVisualGroupActive(IEnumerable<CharacterBodyPartVisual> visuals, bool isActive)
        {
            foreach (var visual in visuals)
            {
                if (visual != null && visual.gameObject.activeSelf != isActive)
                {
                    visual.gameObject.SetActive(isActive);
                }
            }
        }

        private static bool ShouldEnableVisualGroup(string visualName, string targetVisualName)
        {
            // If there is no selected visual name for a body part,
            // this body part should stay empty.
            return !string.IsNullOrWhiteSpace(targetVisualName) && visualName == targetVisualName;
        }

        private void OnValidate()
        {
            isCacheBuilt = false;
        }

#if UNITY_EDITOR
        [ContextMenu("Ensure Visual Components On Child Renderers")]
        private void EnsureVisualComponentsOnChildRenderers()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer || renderer.GetComponent<CharacterBodyPartVisual>() != null)
                {
                    continue;
                }

                UnityEditor.Undo.AddComponent<CharacterBodyPartVisual>(renderer.gameObject);
                UnityEditor.EditorUtility.SetDirty(renderer.gameObject);
            }

            isCacheBuilt = false;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
