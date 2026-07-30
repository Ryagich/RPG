using Combat;
using Inventory.Item;
using UnityEngine;

namespace Messages
{
    public readonly struct PlayerDiedMessage
    {
        public readonly GameObject Player;
        public readonly Transform PlayerTransform;

        public PlayerDiedMessage(GameObject player, Transform playerTransform)
        {
            Player = player;
            PlayerTransform = playerTransform;
        }
    }

    public readonly struct CharacterDamagedMessage
    {
        public readonly GameObject Character;
        public readonly Transform CharacterTransform;
        public readonly CharacterDamageReceiver Attacker;
        public readonly CharacterDamageReceiver IntendedTarget;
        public readonly bool IntendedTargetWasHostile;
        public readonly ItemConfig WeaponConfig;
        public readonly DamageBodyPart BodyPart;
        public readonly float BaseDamage;
        public readonly float BodyMultiplier;
        public readonly float PhysicalDefense;
        public readonly float FinalDamage;
        public readonly Vector3 Point;

        public CharacterDamagedMessage(
            GameObject character,
            Transform characterTransform,
            CharacterDamageReceiver attacker,
            CharacterDamageReceiver intendedTarget,
            bool intendedTargetWasHostile,
            ItemConfig weaponConfig,
            DamageBodyPart bodyPart,
            float baseDamage,
            float bodyMultiplier,
            float physicalDefense,
            float finalDamage,
            Vector3 point)
        {
            Character = character;
            CharacterTransform = characterTransform;
            Attacker = attacker;
            IntendedTarget = intendedTarget;
            IntendedTargetWasHostile = intendedTargetWasHostile;
            WeaponConfig = weaponConfig;
            BodyPart = bodyPart;
            BaseDamage = baseDamage;
            BodyMultiplier = bodyMultiplier;
            PhysicalDefense = physicalDefense;
            FinalDamage = finalDamage;
            Point = point;
        }
    }
}
