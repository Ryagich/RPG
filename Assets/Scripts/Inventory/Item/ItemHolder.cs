using System;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Serialization;

namespace Inventory.Item
{
    public class ItemHolder : MonoBehaviour
    {
        public event Action<ItemHolder> Destroyed;
        public event Action<ItemHolder> StateChanged;

        [field: SerializeField] public ItemConfig Config { get; private set; } = null!;
        [field: SerializeField, Min(1)] public int Count { get; private set; } = 1;
        [SerializeField, FormerlySerializedAs("usesPersistence"),
         Tooltip("When enabled, this individual world item is included in the game save.")]
        private bool usesSave;
        [SerializeField, ShowIf(nameof(usesSave)), ReadOnlyInInspector,
         Tooltip("Stable identity of this individual scene item. Generated automatically.")]
        private string persistentId;
        [SerializeField, ShowIf(nameof(usesSave)),
         Tooltip("When enabled, this item expires after ten minutes of unpaused gameplay.")]
        private bool usesLifetime;
        public string RuntimeTag { get; private set; }

        public bool CanInteractable = true;
        public bool UsesSave => usesSave;
        public string PersistentId => persistentId;
        public bool UsesLifetime => usesLifetime;

        public ItemStack GetItemStack()
        {
            return Config == null ? null : new ItemStack(Config, Count, runtimeTag: RuntimeTag);
        }

        public void Initialize(ItemConfig config, int count = 1, string runtimeTag = null)
        {
            Config = config;
            RuntimeTag = runtimeTag;
            SetCount(count);
        }

        public void SetCount(int count)
        {
            Count = Mathf.Max(1, count);
            StateChanged?.Invoke(this);
        }

        /// <summary>
        /// The prefab asset remains identity-free. An authored scene instance or a runtime drop
        /// receives an individual immutable identifier when it enters persistent world state.
        /// </summary>
        public void ConfigurePersistence(bool usesSave, bool usesLifetime, string assignedPersistentId = null)
        {
            this.usesSave = usesSave;
            this.usesLifetime = usesLifetime;
            if (!usesSave)
            {
                return;
            }

            persistentId = !string.IsNullOrWhiteSpace(assignedPersistentId)
                ? assignedPersistentId
                : string.IsNullOrWhiteSpace(persistentId) ? CreatePersistentId() : persistentId;
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this))
            {
                persistentId = string.Empty;
                return;
            }
#endif
            if (usesSave && Config != null &&
                (string.IsNullOrWhiteSpace(persistentId) || HasDuplicatePersistentIdInScene()))
            {
                persistentId = CreatePersistentId();
            }
        }

        private bool HasDuplicatePersistentIdInScene()
        {
            if (string.IsNullOrWhiteSpace(persistentId))
            {
                return false;
            }

            foreach (ItemHolder candidate in FindObjectsByType<ItemHolder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate != this && candidate.gameObject.scene == gameObject.scene &&
                    candidate.usesSave && candidate.persistentId == persistentId)
                {
                    return true;
                }
            }

            return false;
        }

        private string CreatePersistentId()
        {
            var itemId = Config != null && !string.IsNullOrWhiteSpace(Config.Id)
                ? Config.Id
                : "item";
            return $"{itemId}_{Guid.NewGuid():N}";
        }
        
        private void OnDestroy()
        {
            Destroyed?.Invoke(this);
        }
    }
}
