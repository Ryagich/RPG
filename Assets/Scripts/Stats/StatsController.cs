using UI;
using UnityEngine;

namespace Stats
{
    public class StatsController
    {
        public Stat Hp { get; }

        public StatsController(StatsConfig statsConfig)
        {
            Debug.Log($"StatsController");
            Hp = new Stat(statsConfig.HpStat);
        }
    }
}
