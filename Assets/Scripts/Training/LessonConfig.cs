using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace Training
{
    public enum LessonId
    {
        CombatIntroduction,
        EnemyVision,
        Dodge,
        WeaponAttacks,
        Roll,
        Stamina,
        StaminaLoad,
        FinalSparring
    }

    [CreateAssetMenu(fileName = "Lesson Config", menuName = "configs/Training/Lesson Config")]
    public sealed class LessonConfig : ScriptableObject
    {
        [field: SerializeField, Min(0f)] public float SkipTextShowDelay { get; private set; } = 1f;
        [field: SerializeField] public LocalizedString EvasionPracticeObjective { get; private set; } = new();
        [field: SerializeField] public List<LessonDefinition> Lessons { get; private set; } = new();

        public bool TryGetLesson(LessonId lessonId, out LessonDefinition lesson)
        {
            lesson = Lessons?.Find(candidate => candidate != null && candidate.Id == lessonId);
            return lesson != null;
        }
    }

    [Serializable]
    public sealed class LessonDefinition
    {
        [field: SerializeField] public LessonId Id { get; private set; }
        [field: SerializeField] public LocalizedString Description { get; private set; } = new();
        [field: SerializeField] public LocalizedString QuestDescription { get; private set; } = new();
        [field: SerializeField] public bool CanSkipWithLessonInput { get; private set; } = true;
    }
}
