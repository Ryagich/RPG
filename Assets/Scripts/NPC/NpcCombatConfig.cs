using UnityEngine;

namespace NPC
{
    [CreateAssetMenu(fileName = "NpcCombatConfig", menuName = "configs/NPC/NpcCombatConfig")]
    public sealed class NpcCombatConfig : ScriptableObject
    {
        [field: Tooltip("Интервал поиска врага. Как часто NPC заново проверяет видимые цели; меньшее значение быстрее реагирует, но требует больше вычислений.")]
        [field: SerializeField, Min(0.05f)] public float EnemyScanInterval { get; private set; } = 0.25f;
        [field: Tooltip("Считать ли враждебными цели без фракции. Включайте, если нейтральный или не настроенный объект должен быть целью для NPC.")]
        [field: SerializeField] public bool TreatFactionlessTargetsAsHostile { get; private set; }
        [field: Tooltip("Дистанция остановки при обычном сближении. NPC не пытается буквально войти в центр противника и сохраняет место для атаки.")]
        [field: SerializeField, Min(0f)] public float ApproachStoppingDistance { get; private set; } = 1.6f;
        [field: Tooltip("Насколько близко NPC должен подойти к последней известной позиции цели, прежде чем начать осматриваться.")]
        [field: SerializeField, Min(0f)] public float LastKnownReachedDistance { get; private set; } = 1.2f;
        [field: Tooltip("Длительность осмотра последней известной позиции. После истечения времени и без новой цели NPC возвращается к прежнему занятию.")]
        [field: SerializeField, Min(0f)] public float LookAtLastKnownDuration { get; private set; } = 2f;
        [field: Tooltip("Минимальная пауза между запросами атаки. Защищает аниматор от спама команд и задаёт темп серии ударов.")]
        [field: SerializeField, Min(0f)] public float AttackRequestInterval { get; private set; } = 0.8f;
        [field: Tooltip("Радиус поиска кандидатов на цель. За его пределами NPC не начинает новое обнаружение противника.")]
        [field: SerializeField, Min(0f)] public float TargetSearchRadius { get; private set; } = 18f;
        [field: Tooltip("Аварийный лимит ожидания состояния атаки. Если аниматор не подтвердил или не завершил удар, NPC выходит из зависшего состояния.")]
        [field: SerializeField, Min(0.1f)] public float AttackStateTimeout { get; private set; } = 3f;
        [field: Tooltip("Допуск к стартовой дистанции атаки. Позволяет начать удар у края боевой дистанции и не заставляет NPC бесконечно подбегать на сантиметры.")]
        [field: SerializeField, Min(0f)] public float AttackStartDistanceTolerance { get; private set; } = 0.25f;
        [field: Tooltip("Шанс продолжить комбинацию, когда аниматор открыл окно комбо. 0 — не продолжать, 1 — всегда пытаться продолжить при подходящих условиях.")]
        [field: SerializeField, Range(0f, 1f)] public float ComboAttackChance { get; private set; } = 0.4f;
        [field: Tooltip("Максимум дополнительных запросов атаки в одной комбинации. Ограничение не даёт NPC бесконечно буферизовать удары.")]
        [field: SerializeField, Min(0)] public int MaxComboAttackRequests { get; private set; } = 2;
        [field: Tooltip("Пауза после начала атаки перед первым запросом продолжения комбо. Нужна, чтобы запрос попал в окно анимации, а не в её начало.")]
        [field: SerializeField, Min(0f)] public float ComboAttackInputDelay { get; private set; } = 0.18f;
        [field: Tooltip("Минимальный интервал между дополнительными запросами в комбо. Предотвращает несколько команд за один кадр.")]
        [field: SerializeField, Min(0.01f)] public float ComboAttackInputInterval { get; private set; } = 0.22f;

        [field: Header("Тактическая память")]
        [field: Tooltip("Как долго недавний нанесённый и полученный урон влияет на настроение боя. Большее значение делает реакцию на удачную или неудачную размену более инерционной.")]
        [field: SerializeField, Min(0.1f)] public float DamageMemoryDuration { get; private set; } = 4f;
        [field: Tooltip("Личная дистанция. Если противник подходит ближе, NPC получает сильный повод отступить, сделать уклонение или кувырок.")]
        [field: SerializeField, Min(0.1f)] public float PersonalSpaceDistance { get; private set; } = 1.05f;
        [field: Tooltip("Дистанция сброса личного пространства. Когда противник отходит дальше неё, счётчик защитных отступлений снова разрешается.")]
        [field: SerializeField, Min(0.1f)] public float PersonalSpaceReleaseDistance { get; private set; } = 1.45f;
        [field: Tooltip("Пауза между защитными отступлениями из личного пространства. Не позволяет игроку заставить NPC бесконечно пятиться.")]
        [field: SerializeField, Min(0f)] public float SpacingRetreatCooldown { get; private set; } = 0.55f;
        [field: Tooltip("Сколько раз подряд NPC может отступить из-за слишком близкой цели до её выхода из личного пространства. Это защита от абуза тараном.")]
        [field: SerializeField, Min(1)] public int MaxConsecutiveSpacingRetreats { get; private set; } = 2;

        [field: Header("Уклонения")]
        [field: Tooltip("Аварийный лимит ожидания уклонения или кувырка. Если аниматор не подтвердил действие, NPC возвращается к обычному боевому решению.")]
        [field: SerializeField, Min(0.1f)] public float EvasionStateTimeout { get; private set; } = 1.6f;

        [field: Header("Резкое сближение")]
        [field: Tooltip("Базовый шанс резко сократить дистанцию уклонением вперёд или кувырком. Сильнее срабатывает, если цель действительно отступает; стиль NPC также влияет на итоговый шанс.")]
        [field: SerializeField, Range(0f, 1f)] public float ApproachBurstBaseChance { get; private set; } = 0.15f;
        [field: Tooltip("Пауза между резкими сближениями. Нужна, чтобы погоня не превращалась в постоянный спам кувырков.")]
        [field: SerializeField, Min(0f)] public float ApproachBurstCooldown { get; private set; } = 2.2f;
        [field: Tooltip("Минимальная пауза перед следующей проверкой шанса резкого сближения, если NPC решил не делать его. Не даёт случайности проверяться каждый кадр.")]
        [field: SerializeField, Min(0.05f)] public float ApproachBurstDecisionInterval { get; private set; } = 0.8f;
        [field: Tooltip("Минимальная дистанция для резкого сближения. Вблизи NPC оставляет место обычным атакам и защитным манёврам.")]
        [field: SerializeField, Min(0.1f)] public float ApproachBurstMinDistance { get; private set; } = 2.5f;
        [field: Tooltip("Максимальная дистанция для резкого сближения. Слишком далёкую цель NPC преследует навигацией, а не тратит уклонение впустую.")]
        [field: SerializeField, Min(0.1f)] public float ApproachBurstMaxDistance { get; private set; } = 6.5f;
        [field: Tooltip("Скорость удаления цели от NPC, начиная с которой погоня считает её отступающей. При отступающей цели резкое сближение заметно вероятнее.")]
        [field: SerializeField, Min(0f)] public float TargetRetreatSpeedThreshold { get; private set; } = 0.5f;

        [field: Header("Решения после атаки")]
        [field: Tooltip("Шанс начать бой с обхода цели по дуге. 0 — никогда не обходить в начале, 1 — всегда пытаться при наличии места на NavMesh.")]
        [field: SerializeField, Range(0f, 1f)] public float InitialCircleChance { get; private set; } = 0.55f;
        [field: Tooltip("Базовый вес немедленной следующей атаки. 0 — действие выключено, 1 — максимальный вес на своей шкале; итоговый шанс нормализуется вместе с остальными действиями и меняется от ситуации.")]
        [field: SerializeField, Range(0f, 1f)] public float PostAttackImmediateAttackWeight { get; private set; } = 0.22f;
        [field: Tooltip("Базовый вес стрейфа после атаки. 0 — действие выключено, 1 — максимальный вес на своей шкале; итоговый шанс нормализуется вместе с остальными действиями и меняется от ситуации.")]
        [field: SerializeField, Range(0f, 1f)] public float PostAttackStrafeWeight { get; private set; } = 0.58f;
        [field: Tooltip("Базовый вес отступления назад после атаки. 0 — действие выключено, 1 — максимальный вес на своей шкале; итоговый шанс нормализуется вместе с остальными действиями и меняется от ситуации.")]
        [field: SerializeField, Range(0f, 1f)] public float PostAttackBackstepWeight { get; private set; } = 0.42f;
        [field: Tooltip("Базовый вес обхода цели по дуге после атаки. 0 — действие выключено, 1 — максимальный вес на своей шкале; итоговый шанс нормализуется вместе с остальными действиями и меняется от ситуации.")]
        [field: SerializeField, Range(0f, 1f)] public float PostAttackCircleWeight { get; private set; } = 0.58f;
        [field: Tooltip("Базовый вес короткой паузы после атаки. 0 — действие выключено, 1 — максимальный вес на своей шкале; итоговый шанс нормализуется вместе с остальными действиями и меняется от ситуации.")]
        [field: SerializeField, Range(0f, 1f)] public float PostAttackWaitWeight { get; private set; } = 0.14f;
        [field: Tooltip("Базовый вес удержания дистанции после атаки. 0 — действие выключено, 1 — максимальный вес на своей шкале; итоговый шанс нормализуется вместе с остальными действиями и меняется от ситуации.")]
        [field: SerializeField, Range(0f, 1f)] public float PostAttackKeepDistanceWeight { get; private set; } = 0.68f;
        [field: Tooltip("Насколько сильно повторные немедленные атаки теряют вес. 0 оставляет серии без изменений, 1 максимально подталкивает к другому действию после серии атак.")]
        [field: SerializeField, Range(0f, 1f)] public float AttackRepetitionPenalty { get; private set; } = 0.85f;
        [field: Tooltip("Количество последовательных решений «атаковать», после которого штраф за повтор достигает максимума. Не ограничивает атаки жёстко, а делает другие решения заметно вероятнее.")]
        [field: SerializeField, Min(1)] public int MaxConsecutiveAttackDecisions { get; private set; } = 1;

        [field: Header("Пауза в бою")]
        [field: Tooltip("Минимальная длительность короткой паузы после атаки. Даёт бою читаемый ритм и возможность оценить противника.")]
        [field: SerializeField, Min(0f)] public float WaitMinDuration { get; private set; } = 0.35f;
        [field: Tooltip("Максимальная длительность короткой паузы после атаки. Должна быть не меньше минимальной; фактическое значение выбирается случайно между ними.")]
        [field: SerializeField, Min(0f)] public float WaitMaxDuration { get; private set; } = 0.85f;

        [field: Header("Удержание дистанции")]
        [field: Tooltip("Минимальная длительность выжидательного поведения. В это время NPC держит удобную дистанцию вместо немедленной атаки.")]
        [field: SerializeField, Min(0f)] public float KeepDistanceMinDuration { get; private set; } = 1.8f;
        [field: Tooltip("Максимальная длительность выжидательного поведения. Должна быть не меньше минимальной; фактическое значение выбирается случайно между ними.")]
        [field: SerializeField, Min(0f)] public float KeepDistanceMaxDuration { get; private set; } = 3.4f;
        [field: Tooltip("Нижняя граница желаемой дистанции при выжидании. Если цель ближе, NPC будет искать позицию дальше или сбоку.")]
        [field: SerializeField, Min(0.1f)] public float KeepDistanceMinRange { get; private set; } = 2.7f;
        [field: Tooltip("Верхняя граница желаемой дистанции при выжидании. Если цель дальше, NPC не будет без необходимости продолжать отходить.")]
        [field: SerializeField, Min(0.1f)] public float KeepDistanceMaxRange { get; private set; } = 4.3f;
        [field: Tooltip("Как часто NPC может выбрать новую позицию во время удержания дистанции. Меньше — живее перестроения, больше — спокойнее траектория.")]
        [field: SerializeField, Min(0.05f)] public float KeepDistanceRepositionInterval { get; private set; } = 0.25f;
        [field: Tooltip("Базовая вероятность атаковать, когда противник вошёл в радиус удара во время удержания дистанции. В остальных случаях NPC пытается немедленно разорвать дистанцию уклонением или кувырком.")]
        [field: SerializeField, Range(0f, 1f)] public float KeepDistanceAttackChance { get; private set; } = 0.48f;
        [field: Tooltip("Вероятность атаковать противника, который повернулся к NPC спиной, во время удержания дистанции.")]
        [field: SerializeField, Range(0f, 1f)] public float KeepDistanceBackstabAttackChance { get; private set; } = 0.86f;
        [field: Tooltip("Угол от направления взгляда цели, начиная с которого она считается повернувшейся спиной к NPC.")]
        [field: SerializeField, Range(90f, 180f)] public float KeepDistanceBackstabMinAngle { get; private set; } = 115f;
        [field: Tooltip("Вероятность выбрать боковой стрейф, а не прямой отход при поиске дистанции. 0 — всегда уходить назад, 1 — всегда предпочитать боковое смещение.")]
        [field: SerializeField, Range(0f, 1f)] public float KeepDistanceStrafeChance { get; private set; } = 0.92f;
        [field: Tooltip("Максимальное отклонение направления отхода от прямой линии назад. Позволяет сохранить визуальный контакт и не двигаться по одной прямой.")]
        [field: SerializeField, Range(0f, 75f)] public float KeepDistanceRetreatAngle { get; private set; } = 35f;

        [field: Header("Боевые перемещения")]
        [field: Tooltip("На каком расстоянии точка боевого манёвра считается достигнутой. Больший допуск уменьшает дёрганье возле точки назначения.")]
        [field: SerializeField, Min(0f)] public float CombatMoveReachedDistance { get; private set; } = 0.45f;
        [field: Tooltip("Радиус поиска ближайшей точки NavMesh для боевого манёвра. Помогает выбирать достижимую позицию возле желаемой точки.")]
        [field: SerializeField, Min(0.1f)] public float CombatMoveNavMeshSampleRadius { get; private set; } = 2f;
        [field: Tooltip("Сколько времени NPC может не продвигаться к боевой точке, прежде чем признать путь застрявшим и сменить решение.")]
        [field: SerializeField, Min(0.1f)] public float CombatMoveStuckTimeout { get; private set; } = 1.3f;
        [field: Tooltip("Минимальное изменение позиции, считающееся продвижением. Нужен для корректного определения застревания без реакции на микродрожание.")]
        [field: SerializeField, Min(0.001f)] public float CombatMoveProgressDistance { get; private set; } = 0.08f;
        [field: Tooltip("Минимальная длина бокового стрейфа. Используется для разнообразия позиции после атаки.")]
        [field: SerializeField, Min(0.1f)] public float StrafeMinDistance { get; private set; } = 1.8f;
        [field: Tooltip("Максимальная длина бокового стрейфа. Должна быть не меньше минимальной; конкретная длина выбирается случайно.")]
        [field: SerializeField, Min(0.1f)] public float StrafeMaxDistance { get; private set; } = 3.3f;
        [field: Tooltip("Минимальная длина обычного отступления назад. Отступление применяет NavMesh и сохраняет возможность обойти препятствия.")]
        [field: SerializeField, Min(0.1f)] public float BackstepMinDistance { get; private set; } = 1.6f;
        [field: Tooltip("Максимальная длина обычного отступления назад. Должна быть не меньше минимальной; конкретная длина выбирается случайно.")]
        [field: SerializeField, Min(0.1f)] public float BackstepMaxDistance { get; private set; } = 3.2f;
        [field: Tooltip("Минимальный радиус обхода цели по дуге. Малый радиус делает манёвр более агрессивным, большой — безопаснее.")]
        [field: SerializeField, Min(0.1f)] public float CircleMinRadius { get; private set; } = 2.8f;
        [field: Tooltip("Максимальный радиус обхода цели по дуге. Должен быть не меньше минимального; конкретный радиус выбирается случайно.")]
        [field: SerializeField, Min(0.1f)] public float CircleMaxRadius { get; private set; } = 4.6f;
        [field: Tooltip("Минимальный угол обхода цели по дуге. Определяет, насколько заметно NPC сменит сторону.")]
        [field: SerializeField, Range(5f, 180f)] public float CircleMinAngle { get; private set; } = 50f;
        [field: Tooltip("Максимальный угол обхода цели по дуге. Должен быть не меньше минимального; конкретный угол выбирается случайно.")]
        [field: SerializeField, Range(5f, 180f)] public float CircleMaxAngle { get; private set; } = 125f;

        [field: Header("Групповой бой")]
        [field: Tooltip("Максимум NPC, которым одновременно разрешено занимать прямой атакующий слот у одной цели. Остальные обходят или ждут снаружи.")]
        [field: SerializeField, Min(1)] public int MaxDirectAttackersPerTarget { get; private set; } = 4;
        [field: Tooltip("Радиус прямого атакующего слота вокруг цели. На нём распределяются NPC, которым разрешено сближаться для атаки.")]
        [field: SerializeField, Min(0.1f)] public float DirectAttackSlotRadius { get; private set; } = 1.65f;
        [field: Tooltip("Допуск к закреплённой атакующей позиции. NPC не начнёт удар, пока не займёт свой сектор вокруг цели; это предотвращает атаки на пути к формации.")]
        [field: SerializeField, Min(0.05f)] public float DirectAttackSlotReachedDistance { get; private set; } = 0.75f;
        [field: Tooltip("Минимальный радиус очереди вокруг цели для NPC без прямого атакующего слота.")]
        [field: SerializeField, Min(0.1f)] public float QueueCircleMinRadius { get; private set; } = 3.8f;
        [field: Tooltip("Максимальный радиус очереди вокруг цели для NPC без прямого атакующего слота. Должен быть не меньше минимального.")]
        [field: SerializeField, Min(0.1f)] public float QueueCircleMaxRadius { get; private set; } = 5.4f;
        [field: Tooltip("Минимальный угол перемещения по кругу очереди. Помогает NPC без слота не стоять в одной точке.")]
        [field: SerializeField, Range(5f, 180f)] public float QueueCircleMinAngle { get; private set; } = 25f;
        [field: Tooltip("Максимальный угол перемещения по кругу очереди. Должен быть не меньше минимального; конкретный угол выбирается случайно.")]
        [field: SerializeField, Range(5f, 180f)] public float QueueCircleMaxAngle { get; private set; } = 65f;
        [field: Tooltip("Не позволять ли атаковать, если союзник находится на линии удара. Защищает от дружественного огня и визуальных ударов сквозь союзников.")]
        [field: SerializeField] public bool PreventFriendlyFire { get; private set; } = true;
        [field: Tooltip("Ширина проверяемого коридора линии удара. Больший радиус безопаснее для союзников, но чаще отменяет атаку в тесной группе.")]
        [field: SerializeField, Min(0.05f)] public float FriendlyFireLaneRadius { get; private set; } = 0.65f;
        [field: Tooltip("Как долго после принятой атаки NPC помнит, что замах был направлен во врага. Получивший случайный удар союзник использует это как запасное объяснение, если исходная цель уже умерла, сменилась или исчезла.")]
        [field: SerializeField, Min(0.1f)] public float FriendlyFireIntentGraceDuration { get; private set; } = 3.5f;
        [field: Tooltip("Пауза после падения текущей цели. Даёт анимации смерти и смене цели завершиться без мгновенного рывка NPC.")]
        [field: SerializeField, Min(0f)] public float TargetDownWaitDuration { get; private set; } = 2f;

        [field: Header("Уведомление об агрессии")]
        [field: Tooltip("Задержка перед тем, как NPC сообщит ближайшим союзникам о замеченной агрессии. Убирает мгновенную неестественную реакцию всей группы.")]
        [field: SerializeField, Min(0f)] public float AggressionNotificationDelay { get; private set; } = 1.2f;
        [field: Tooltip("Радиус, в котором союзники получают уведомление об агрессии и могут выбрать того же противника.")]
        [field: SerializeField, Min(0f)] public float AggressionNotificationRadius { get; private set; } = 12f;

        [field: Header("Бегство")]
        [field: Tooltip("Множитель скорости NPC в состоянии бегства. Применяется поверх его обычной скорости навигации.")]
        [field: SerializeField, Min(0.1f)] public float FleeSpeedMultiplier { get; private set; } = 1.65f;
        [field: Tooltip("Минимальная дистанция выбираемой точки бегства от угрозы. Не позволяет NPC остановиться слишком близко к опасности.")]
        [field: SerializeField, Min(0.5f)] public float FleeMinDistance { get; private set; } = 6f;
        [field: Tooltip("Максимальная дистанция выбираемой точки бегства от угрозы. Должна быть не меньше минимальной; конкретная точка выбирается в этом диапазоне.")]
        [field: SerializeField, Min(0.5f)] public float FleeMaxDistance { get; private set; } = 10f;
        [field: Tooltip("Случайное отклонение направления бегства от прямой линии от угрозы. Делает маршруты менее одинаковыми и помогает обходить препятствия.")]
        [field: SerializeField, Range(0f, 90f)] public float FleeAngleJitter { get; private set; } = 25f;
        [field: Tooltip("Сколько кандидатов точки бегства проверить на NavMesh. Большее число повышает шанс хорошего пути, но требует больше расчётов.")]
        [field: SerializeField, Min(1)] public int FleeSampleAttempts { get; private set; } = 8;
        [field: Tooltip("Радиус поиска точки NavMesh возле кандидата бегства. Позволяет подобрать достижимую позицию вместо точки вне навигационной сетки.")]
        [field: SerializeField, Min(0.1f)] public float FleeNavMeshSampleRadius { get; private set; } = 3f;
        [field: Tooltip("Длина лучей, которыми оценивается открытость точки бегства. Открытая точка снижает риск упереться в стену или тупик.")]
        [field: SerializeField, Min(0.1f)] public float FleeOpennessProbeDistance { get; private set; } = 3f;
        [field: Tooltip("Вес открытости при выборе точки бегства. 0 игнорирует открытость; большее значение сильнее предпочитает свободные направления.")]
        [field: SerializeField, Min(0f)] public float FleeOpennessWeight { get; private set; } = 20f;
        [field: Tooltip("Дистанция, на которой выбранная точка бегства считается достигнутой. Больший допуск уменьшает дёрганье у финальной точки.")]
        [field: SerializeField, Min(0f)] public float FleeReachedDistance { get; private set; } = 0.75f;
        [field: Tooltip("Как долго NPC оглядывается после достижения точки бегства. Даёт ему шанс снова увидеть угрозу перед следующим решением.")]
        [field: SerializeField, Min(0f)] public float FleeLookBackDuration { get; private set; } = 1.5f;
        [field: Tooltip("Минимальная длительность решения о бегстве. Не даёт состоянию мгновенно переключаться туда-сюда при кратком изменении угрозы.")]
        [field: SerializeField, Min(0f)] public float FleeMinDecisionDuration { get; private set; } = 0.15f;
        [field: Tooltip("Дистанция, на которой угроза считается слишком близкой во время бегства. Используется для выбора более срочной реакции.")]
        [field: SerializeField, Min(0f)] public float FleeCloseThreatDistance { get; private set; } = 2.25f;
        [field: Tooltip("Дистанция, после которой угроза считается далёкой во время бегства. Помогает определить, можно ли закончить срочное бегство.")]
        [field: SerializeField, Min(0f)] public float FleeFarThreatDistance { get; private set; } = 8f;
    }
}
