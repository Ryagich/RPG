using System.Linq;
using Landings.Plants.PlantConfigs;
using MessagePipe;
using Messages;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Landings.Plants
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class PlantGrowerByUpper : ITickable, IGrower
    {
        private readonly Transform parent;
        private readonly IObjectResolver resolver;
        private readonly IPublisher<PlantHasGrownMessage> plantHasGrownPublisher;

        private readonly IPublisher<PlaySoundMessage> globalPlaySoundPublisher;

        private GameObject plant;
        public bool IsPlanting { get; private set; }
        public float GrowTime { get; private set; }
        public float Distance { get; private set; }
        public ReactiveProperty<float> LostDistance { get; private set; } = new();

        private PlantConfig plantConfig;
        private float speed;
        
        public PlantGrowerByUpper
            (
                Transform parent,
                IObjectResolver resolver,
                IPublisher<PlantHasGrownMessage> plantHasGrownPublisher
            )
        {
            this.parent = parent;
            this.resolver = resolver;
            this.plantHasGrownPublisher = plantHasGrownPublisher;
            
            globalPlaySoundPublisher = GlobalMessagePipe.GetPublisher<PlaySoundMessage>();
        }

        public void StartGrow(PlantConfig config)
        {
            plantConfig = config;
            UpdateLocalValues();
            plant = resolver.Instantiate(plantConfig.Stages.First());
            var t = plant.transform;
            t.SetParent(parent);
            t.localPosition = plantConfig.StartPosition;
            IsPlanting = true;
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
            if (!plant || !IsPlanting)
                return;
            if (plant.transform.localPosition.Equals(plantConfig.TargetPosition))
            {
                IsPlanting = false;
                var newSettings = plantConfig.PlantSoundsSettings.GrownStageSoundSettings;
                globalPlaySoundPublisher.Publish(new PlaySoundMessage(newSettings, plant.transform.position, null));
                plantHasGrownPublisher.Publish(new PlantHasGrownMessage(this));
                return;
            }
            var localPos = plant.transform.localPosition;
            plant.transform.localPosition = Vector3.MoveTowards(localPos, plantConfig.TargetPosition,
                                                                speed * Time.deltaTime);
            LostDistance.Value = Vector3.Distance(localPos, plantConfig.StartPosition);
        }

        private void UpdateLocalValues()
        {
            Distance = Vector3.Distance(plantConfig.StartPosition, plantConfig.TargetPosition);
            GrowTime = Random.Range(plantConfig.GrowTime.x, plantConfig.GrowTime.y);
            speed = Distance / GrowTime;
        }
    }
}