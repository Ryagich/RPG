using System.Collections.Generic;
using GameAudio;
using UnityEngine;

namespace UI.UIElements
{
    public sealed class SoundSettingsPageUI : MonoBehaviour
    {
        [SerializeField] private RectTransform rowsParent;
        [SerializeField] private SoundSettingsRow rowPrefab;

        private readonly List<SoundSettingsRow> rows = new();

        public void Initialize(IAudioService audioService)
        {
            if (rowsParent == null || rowPrefab == null || audioService == null)
            {
                Debug.LogError("Sound settings prefab is not configured.", this);
                return;
            }

            Dispose();
            foreach (var category in AudioConfig.SettingsCategories)
            {
                var row = Instantiate(rowPrefab, rowsParent);
                row.name = $"Sound_{category}";
                row.Initialize(category, audioService);
                rows.Add(row);
            }
        }

        public void Dispose()
        {
            foreach (var row in rows)
            {
                if (row != null)
                {
                    row.Dispose();
                    Destroy(row.gameObject);
                }
            }

            rows.Clear();
        }

        private void OnDestroy() => Dispose();
    }
}
