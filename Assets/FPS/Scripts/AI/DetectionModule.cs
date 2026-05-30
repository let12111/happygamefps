using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.AI
{
    // ============================================================================
    // DetectionModule — «органы чувств» врага. Отвечает на вопросы:
    //  - вижу ли я сейчас цель;
    //  - есть ли «известная цель» (я её недавно видел);
    //  - в радиусе атаки ли она.
    //
    // Алгоритм для каждого тика (вызывается EnemyController с дросселированием 10 Гц):
    //  1) Если у нас была «известная цель», но мы её не видим больше KnownTargetTimeout
    //     — забываем.
    //  2) Перебираем всех Actor'ов из ActorsManager, отфильтровываем по фракции.
    //  3) Для самого близкого пускаем RaycastNonAlloc — есть ли line-of-sight.
    //  4) Если попали именно в этого Actor — он наша цель.
    //  5) Сравниваем с «прошлым» состоянием — генерим события onDetect/onLost.
    // ============================================================================
    public class DetectionModule : MonoBehaviour
    {
        // Откуда стартует луч проверки видимости — обычно «глаза» бота.
        [Tooltip("The point representing the source of target-detection raycasts for the enemy AI")]
        public Transform DetectionSourcePoint;

        [Tooltip("The max distance at which the enemy can see targets")]
        public float DetectionRange = 20f;

        [Tooltip("The max distance at which the enemy can attack its target")]
        public float AttackRange = 10f;

        // Время, на которое враг «помнит» цель, скрывшуюся с глаз.
        // Без этого враг моментально переключался бы в Patrol, когда игрок зашёл за столб.
        [Tooltip("Time before an enemy abandons a known target that it can't see anymore")]
        public float KnownTargetTimeout = 4f;

        [Tooltip("Optional animator for OnShoot animations")]
        public Animator Animator;

        public UnityAction onDetectedTarget;
        public UnityAction onLostTarget;

        // Цель, о которой мы «помним» (может уже не видеть прямо сейчас).
        public GameObject KnownDetectedTarget { get; private set; }
        public bool IsTargetInAttackRange { get; private set; }
        public bool IsSeeingTarget { get; private set; }
        // Знали ли цель в прошлом кадре — для генерации событий «появилась/исчезла».
        public bool HadKnownTarget { get; private set; }

        protected float TimeLastSeenTarget = Mathf.NegativeInfinity;

        IActorsManager m_ActorsManager;

        // Буфер для NonAlloc-рейкаста. Статический — общий на все DetectionModule
        // (детекция последовательна на main thread). См. CLAUDE.md.
        static readonly RaycastHit[] s_RaycastBuffer = new RaycastHit[16];

        const string k_AnimAttackParameter = "Attack";
        const string k_AnimOnDamagedParameter = "OnDamaged";

        protected virtual void Start()
        {
            var actorsManager = FindAnyObjectByType<ActorsManager>();
            DebugUtility.HandleErrorIfNullFindObject<ActorsManager, DetectionModule>(actorsManager, this);
            m_ActorsManager = actorsManager;
        }

        public virtual void HandleTargetDetection(Actor actor, Collider[] selfColliders)
        {
            // Handle known target detection timeout
            // 1) Забываем цель, если давно не видим.
            if (KnownDetectedTarget && !IsSeeingTarget && (Time.time - TimeLastSeenTarget) > KnownTargetTimeout)
            {
                KnownDetectedTarget = null;
            }

            // Find the closest visible hostile actor
            // sqrMagnitude быстрее magnitude (нет sqrt), поэтому сравниваем квадраты.
            float sqrDetectionRange = DetectionRange * DetectionRange;
            IsSeeingTarget = false;
            float closestSqrDistance = Mathf.Infinity;
            foreach (Actor otherActor in m_ActorsManager.Actors)
            {
                // Чужая фракция (Affiliation) = потенциальная цель.
                if (otherActor.Affiliation != actor.Affiliation)
                {
                    float sqrDistance = (otherActor.transform.position - DetectionSourcePoint.position).sqrMagnitude;
                    if (sqrDistance < sqrDetectionRange && sqrDistance < closestSqrDistance)
                    {
                        // Check for obstructions
                        // Проверяем line-of-sight: пускаем луч к AimPoint цели.
                        int hitCount = Physics.RaycastNonAlloc(DetectionSourcePoint.position,
                            (otherActor.AimPoint.position - DetectionSourcePoint.position).normalized,
                            s_RaycastBuffer, DetectionRange, -1, QueryTriggerInteraction.Ignore);
                        // Ищем ближайшее попадание, ИГНОРИРУЯ свои коллайдеры.
                        RaycastHit closestValidHit = new RaycastHit();
                        closestValidHit.distance = Mathf.Infinity;
                        bool foundValidHit = false;
                        for (int i = 0; i < hitCount; i++)
                        {
                            var hit = s_RaycastBuffer[i];
                            bool isSelf = false;
                            for (int j = 0; j < selfColliders.Length; j++)
                            {
                                if (selfColliders[j] == hit.collider) { isSelf = true; break; }
                            }
                            if (!isSelf && hit.distance < closestValidHit.distance)
                            {
                                closestValidHit = hit;
                                foundValidHit = true;
                            }
                        }

                        if (foundValidHit)
                        {
                            // Действительно ли мы попали в нужного актора?
                            // (Может попасть в стену между нами и им.)
                            Actor hitActor = closestValidHit.collider.GetComponentInParent<Actor>();
                            if (hitActor == otherActor)
                            {
                                IsSeeingTarget = true;
                                closestSqrDistance = sqrDistance;

                                TimeLastSeenTarget = Time.time;
                                KnownDetectedTarget = otherActor.AimPoint.gameObject;
                            }
                        }
                    }
                }
            }

            // Точное расстояние для проверки радиуса атаки.
            IsTargetInAttackRange = KnownDetectedTarget != null &&
                                    Vector3.Distance(transform.position, KnownDetectedTarget.transform.position) <=
                                    AttackRange;

            // Detection events
            // Границы: было null → стало не-null → onDetect.
            if (!HadKnownTarget &&
                KnownDetectedTarget != null)
            {
                OnDetect();
            }

            // Было не-null → стало null → onLost.
            if (HadKnownTarget &&
                KnownDetectedTarget == null)
            {
                OnLostTarget();
            }

            // Remember if we already knew a target (for next frame)
            HadKnownTarget = KnownDetectedTarget != null;
        }

        public virtual void OnLostTarget() => onLostTarget?.Invoke();

        public virtual void OnDetect() => onDetectedTarget?.Invoke();

        // Получили урон — даже если источник за углом, теперь он наша «известная цель».
        // Это даёт игроку шанс выманить врага: выстрелил из укрытия, он попытался зайти.
        public virtual void OnDamaged(GameObject damageSource)
        {
            TimeLastSeenTarget = Time.time;
            KnownDetectedTarget = damageSource;

            if (Animator)
            {
                Animator.SetTrigger(k_AnimOnDamagedParameter);
            }
        }

        public virtual void OnAttack()
        {
            if (Animator)
            {
                Animator.SetTrigger(k_AnimAttackParameter);
            }
        }
    }
}
