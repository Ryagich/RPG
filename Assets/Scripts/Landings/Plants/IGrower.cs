using Landings.Plants.PlantConfigs;
using UnityEngine;

namespace Landings.Plants
{
    public interface IGrower
    {
        public void StartGrow(PlantConfig config);
        public GameObject GivePlant();
        public void DeletePlant();
    }
}