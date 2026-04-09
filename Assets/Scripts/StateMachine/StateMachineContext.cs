using System.Collections.Generic;
using Inventory.Inventories;
using UnityEngine;
using UnityEngine.AI;

namespace StateMachine
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class StateMachineContext
    {
        public NavMeshAgent NavMeshAgent;
        public IInventory Inventory;
        public Animator Animator;

        public Vector3 TP;

        public float DistanceToTarget;
        public float DeltaTime;
        public float TimeBetweenIterations;
        public float T;
        public List<int> Costs = new();
        
        public int QueueIndex;
    }
}