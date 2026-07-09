using DG.Tweening;
using Landings.Plants.PlantConfigs;
using MessagePipe;
using Messages;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Landings.Plants
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PlantGrowerByStages : ITickable, IGrower
    {
        private readonly IObjectResolver resolver;
        private readonly Transform parent;
        private readonly IPublisher<PlantHasFinishedGrownMessage> plantHasFinishedGrowPublisher;
        private readonly IPublisher<PlaySoundMessage> globalPlaySoundPublisher;

        private GameObject plant;
        public ReactiveProperty<bool> IsPlanted { get; private set; } = new(true);
        public int currentStage { get; private set; }
        public ReactiveProperty<float> timer { get; private set; } = new();
        public float stageTime { get; private set; }
        private PlantConfig plantConfig;
        
        public PlantGrowerByStages
            (
                Transform parent,
                IObjectResolver resolver,
                IPublisher<PlantHasFinishedGrownMessage> plantHasFinishedGrowPublisher
            )
        {
            this.resolver = resolver;
            this.parent = parent;
            this.resolver = resolver;
            this.plantHasFinishedGrowPublisher = plantHasFinishedGrowPublisher;

            globalPlaySoundPublisher = GlobalMessagePipe.GetPublisher<PlaySoundMessage>();
        }

        public void StartGrow(PlantConfig config)
        {
            plantConfig = config;
            NextStage(false);
            IsPlanted.Value = false;
        }

        public GameObject GivePlant()
        {
            var toGive = plant;
            plant = null;
            return toGive;
        }
        
        public void DeletePlant()
        {
            if (plant)
                Object.Destroy(plant);
            plant = null;
        }

        public void Tick()
        {
            if (IsPlanted.Value)
                return;

            timer.Value += Time.deltaTime;
            if (timer.Value >= stageTime)
            {
                timer.Value = 0f;
                NextStage();
            }
        }

        private void NextStage(bool doAnimation = true)
        {
            SpawnPlant(doAnimation);
            currentStage++;
            if (currentStage >= plantConfig.Stages.Count)
            {
                IsPlanted.Value = true;
                currentStage = 0;
                plantHasFinishedGrowPublisher.Publish(new PlantHasFinishedGrownMessage(this));
            }
        }

        private void SpawnPlant(bool doAnimation = true)
        {
            if (plant)
                Object.Destroy(plant);
            stageTime = Random.Range(plantConfig.TimeBetweenStages.x, plantConfig.TimeBetweenStages.y);
            plant = resolver.Instantiate(plantConfig.Stages[currentStage]);
            var t = plant.transform;
            t.SetParent(parent);
            t.localPosition = plantConfig.TargetPosition;
            if (doAnimation)
            {
                var targetScale = t.localScale;
                t.localScale = targetScale * .5f;
                t.DOScale(targetScale, .5f).SetEase(Ease.OutElastic, .2f);
            }
            var newSettings = plantConfig.PlantSoundsSettings.GrownStageSoundSettings;
            globalPlaySoundPublisher.Publish(new PlaySoundMessage(newSettings, plant.transform.position, null));
        }
    }
}