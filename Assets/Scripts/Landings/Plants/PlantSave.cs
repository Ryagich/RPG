using System;
using UnityEngine;

namespace Landings.Plants
{
    [Serializable]
    public class PlantSave
    {
        public string Id;
        public Vector2Int Cell;

        public PlantSave(string id, Vector2Int cell)
        {
            Id = id;
            Cell = cell;
        }
    }
    
    [Serializable]
    public class PlantInStorageSave
    {
        public string Id;
        public int Count;
        
        public PlantInStorageSave(string id, int count)
        {
            Id = id;
            Count = count;
        }
    }
}