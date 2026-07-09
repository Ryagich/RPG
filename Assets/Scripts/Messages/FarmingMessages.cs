using Landings.Plants;
using Sounds;
using UnityEngine;

namespace Messages
{
    public readonly struct PlantHasGrownMessage
    {
        public readonly IGrower Grower;

        public PlantHasGrownMessage(IGrower grower)
        {
            Grower = grower;
        }
    }

    public readonly struct PlantHasFinishedGrownMessage
    {
        public readonly IGrower Grower;

        public PlantHasFinishedGrownMessage(IGrower grower)
        {
            Grower = grower;
        }
    }

    public readonly struct ItemGivenFromInventory { }

    public readonly struct FruitHasGrown
    {
        public readonly Fruit Fruit;

        public FruitHasGrown(Fruit fruit)
        {
            Fruit = fruit;
        }
    }

    public readonly struct PlaySoundMessage
    {
        public readonly SoundSettings SoundSettings;
        public readonly Vector3 Position;
        public readonly Transform Parent;

        public PlaySoundMessage(SoundSettings soundSettings, Vector3 position, Transform parent)
        {
            SoundSettings = soundSettings;
            Position = position;
            Parent = parent;
        }
    }
}
