namespace Monsters
{
    /// <summary>Immutable attack balance supplied by PlantMonsterLifetimeScope.</summary>
    public sealed class PlantMonsterAttackSettings
    {
        public PlantMonsterAttackSettings(float contactDamage)
        {
            ContactDamage = contactDamage;
        }

        public float ContactDamage { get; }
    }
}
