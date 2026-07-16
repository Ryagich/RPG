using System;
using System.Collections.Generic;
using Interactable;
using Inventory.Inventories;
using Inventory.Item;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Landings.Plants
{
    [RequireComponent(typeof(Interactable.Interactable))]
    public sealed class FruitPlantHarvestInteractable : MonoBehaviour, IInteractableAvailability
    {
        public event Action<FruitPlantHarvestInteractable> Emptied;
        public event Action<ItemHolder> FruitCollected;

        [SerializeField] private GameObject plantRoot;
        [SerializeField] private bool destroyPlantWhenEmpty = true;

        private readonly List<ItemHolder> fruits = new();
        private Interactable.Interactable interactable;

        public bool HasRipeFruits
        {
            get
            {
                RegisterChildFruits();
                RemoveMissingFruits();
                return fruits.Count > 0;
            }
        }

        private void Awake()
        {
            if (plantRoot == null)
            {
                plantRoot = gameObject;
            }

            EnsureInteractable();
            RegisterChildFruits();
        }

        private void OnEnable()
        {
            EnsureInteractable();
            if (interactable != null)
            {
                interactable.Interacted += OnInteracted;
            }

        }

        private void OnDisable()
        {
            if (interactable != null)
            {
                interactable.Interacted -= OnInteracted;
            }
        }

        public bool IsInteractableAvailable(LifetimeScope interactorScope)
        {
            return HasRipeFruits;
        }

        public void RegisterFruit(ItemHolder fruit)
        {
            if (fruit == null || fruits.Contains(fruit))
            {
                return;
            }

            fruit.CanInteractable = false;
            fruit.Destroyed += OnFruitDestroyed;
            fruits.Add(fruit);
        }

        private void OnInteracted(LifetimeScope playerScope)
        {
            RemoveMissingFruits();
            RegisterChildFruits();
            if (fruits.Count == 0)
            {
                return;
            }

            if (playerScope == null || !playerScope.Container.TryResolve<IInventory>(out var inventory))
            {
                return;
            }

            foreach (var fruit in fruits.ToArray())
            {
                if (fruit == null)
                {
                    fruits.Remove(fruit);
                    continue;
                }

                var stack = fruit.GetItemStack();
                if (stack == null)
                {
                    continue;
                }

                var originalCount = stack.Count;
                var remainder = inventory.TryAdd(stack);
                if (remainder != null && remainder.Count == originalCount && stack.CanRotate())
                {
                    remainder.Rotate90();
                    remainder = inventory.TryAdd(remainder);
                }

                if (remainder == null)
                {
                    fruits.Remove(fruit);
                    FruitCollected?.Invoke(fruit);
                    fruit.Destroyed -= OnFruitDestroyed;
                    Destroy(fruit.gameObject);
                    continue;
                }

                if (remainder.Count != originalCount)
                {
                    fruit.SetCount(remainder.Count);
                }

                break;
            }

            if (fruits.Count == 0)
            {
                Emptied?.Invoke(this);
                if (destroyPlantWhenEmpty && plantRoot != null)
                {
                    Destroy(plantRoot);
                }
            }
        }

        private void OnFruitDestroyed(ItemHolder fruit)
        {
            if (fruit != null)
            {
                fruit.Destroyed -= OnFruitDestroyed;
            }

            fruits.Remove(fruit);
        }

        private void EnsureInteractable()
        {
            interactable = GetComponent<Interactable.Interactable>();
            if (interactable == null)
            {
                interactable = gameObject.AddComponent<Interactable.Interactable>();
            }

            interactable.InteractionMode = InteractionMode.Manual;
        }

        private void RegisterChildFruits()
        {
            var root = plantRoot != null ? plantRoot : gameObject;
            var childFruits = root.GetComponentsInChildren<ItemHolder>(true);
            foreach (var fruit in childFruits)
            {
                RegisterFruit(fruit);
            }
        }

        private void RemoveMissingFruits()
        {
            for (var i = fruits.Count - 1; i >= 0; i--)
            {
                if (fruits[i] == null)
                {
                    fruits.RemoveAt(i);
                }
            }
        }
    }
}
