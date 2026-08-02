using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using DG.Tweening;
using Landings.Plants.PlantConfigs;
using MessagePipe;
using Messages;

namespace Landings.Plants
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class FruitGrower : ITickable
    {
        public FruitPlantConfig FruitPlantConfig;
        private readonly Transform parent;
        private readonly List<Fruit> fruits = new();
        private readonly IObjectResolver resolver;
        private readonly IPublisher<FruitHasGrown> fruitHasGrownPublisher;
        private readonly IPublisher<PlaySoundMessage> globalPlaySoundPublisher;

        public FruitGrower
            (
                Transform parent,
                IObjectResolver resolver,
                IPublisher<FruitHasGrown> fruitHasGrownPublisher,
                IPublisher<PlaySoundMessage> playSoundPublisher
            )
        {
            this.parent = parent;
            this.resolver = resolver;
            this.fruitHasGrownPublisher = fruitHasGrownPublisher;
            globalPlaySoundPublisher = playSoundPublisher;
        }

        public void SetPoints(List<Transform> points)
        {
            fruits.Clear();
            foreach (var point in points)
            {
                fruits.Add(new Fruit(point.transform));
            }
        }
        
        public int StartGrow()
        {
            var count = 0;
            foreach (var fruit in fruits)
            {
                if (FruitPlantConfig.FruitGrowChance >= Random.Range(.0f, 1.0f))
                {
                    SpawnFruit(fruit);
                    fruit.IsPlanted = false;
                    count++;
                }
            }
            return count;
        }
        
        public void Tick()
        {
            foreach (var fruit in fruits)
            {
                if (fruit.IsPlanted)
                {
                    continue;
                }
                if (fruit.Time >= fruit.StageTime)
                {
                    fruit.Time = 0;
                    NextStage(fruit);
                } 
                fruit.Time += Time.deltaTime;
            }
        }
        
        private void NextStage(Fruit fruit)
        {
            SpawnFruit(fruit);
            if (fruit.CurrentStage >= FruitPlantConfig.FruitStages.Count)
            {
                fruit.IsPlanted = true;
                fruit.CurrentStage = 0;    
                fruitHasGrownPublisher.Publish(new FruitHasGrown(fruit));
            }
        }
 
        private void SpawnFruit(Fruit fruit)
        {
            if (fruit.FruitObj)
                Object.Destroy(fruit.FruitObj);
            fruit.StageTime = Random.Range(FruitPlantConfig.FruitGrowTime.x, FruitPlantConfig.FruitGrowTime.y);
            fruit.FruitObj = resolver.Instantiate(FruitPlantConfig.FruitStages[fruit.CurrentStage], fruit.Parent);
            var t = fruit.FruitObj.transform;
            var targetScale = t.localScale;
            t.localScale = targetScale * .5f;
            t.DOScale(targetScale, .5f).SetEase(Ease.OutElastic, .2f);
            fruit.CurrentStage++;
            var newSettings = FruitPlantConfig.FruitSoundConfig.SoundSettings;
            globalPlaySoundPublisher.Publish(new PlaySoundMessage(newSettings, fruit.FruitObj.transform.position, null));
        }
    }
    
    public class Fruit
    {
        public Transform Parent;
        public GameObject FruitObj;
        public float Time;
        public float StageTime;
        public int CurrentStage;
        public bool IsPlanted = true;

        public Fruit(Transform parent)
        {
            Parent = parent;
        }
    }
}
