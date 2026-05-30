using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Unity.FPS.AI
{
    // ============================================================================
    // EnemyController — БАЗОВЫЙ класс всех врагов. Унифицирует общие вещи:
    //  - здоровье + смерть + дроп лута;
    //  - визуальные реакции на урон (вспышка тела, цвет глаза);
    //  - управление NavMeshAgent (движение по NavMesh);
    //  - привязка модулей DetectionModule и NavigationModule;
    //  - управление оружием (одно или несколько с автосменой);
    //  - регистрация в EnemyManager (для глобального счётчика).
    //
    // Конкретные подклассы (EnemyMobile, EnemyTurret) реализуют конечный
    // автомат поведения (патруль/атака/...) поверх этой базы.
    //
    // [RequireComponent] жёстко требует Health (бьём по нему), Actor (фракция,
    // AimPoint) и NavMeshAgent (даже у турелей — он используется для подсветки положения).
    // ============================================================================
    [RequireComponent(typeof(Health), typeof(Actor), typeof(NavMeshAgent))]
    public class EnemyController : MonoBehaviour
    {
        // Пара «рендерер + индекс материала» — для точечного override через
        // PropertyBlock (см. OverheatBehavior, тот же приём).
        [System.Serializable]
        public struct RendererIndexData
        {
            public Renderer Renderer;
            public int MaterialIndex;

            public RendererIndexData(Renderer renderer, int index)
            {
                Renderer = renderer;
                MaterialIndex = index;
            }
        }

        [Header("Parameters")]
        // Самоудаление при падении за карту (как у игрока KillHeight).
        [Tooltip("The Y height at which the enemy will be automatically killed (if it falls off of the level)")]
        public float SelfDestructYHeight = -20f;

        // Расстояние до следующей точки маршрута, при котором она считается достигнутой.
        [Tooltip("The distance at which the enemy considers that it has reached its current path destination point")]
        public float PathReachingRadius = 2f;

        [Tooltip("The speed at which the enemy rotates")]
        public float OrientationSpeed = 10f;

        // Задержка перед удалением — для анимации смерти (падение, разваливание).
        [Tooltip("Delay after death where the GameObject is destroyed (to allow for animation)")]
        public float DeathDuration = 0f;


        [Header("Weapons Parameters")] [Tooltip("Allow weapon swapping for this enemy")]
        public bool SwapToNextWeapon = false;

        [Tooltip("Time delay between a weapon swap and the next attack")]
        public float DelayAfterWeaponSwap = 0f;

        [Header("Eye color")] [Tooltip("Material for the eye color")]
        public Material EyeColorMaterial;

        // ColorUsageAttribute(true, true) = с альфой и HDR (для свечения глаза).
        [Tooltip("The default color of the bot's eye")] [ColorUsageAttribute(true, true)]
        public Color DefaultEyeColor;

        [Tooltip("The attack color of the bot's eye")] [ColorUsageAttribute(true, true)]
        public Color AttackEyeColor;

        [Header("Flash on hit")] [Tooltip("The material used for the body of the hoverbot")]
        public Material BodyMaterial;

        // Градиент эмиссии: вспышка попадания → возврат к нейтральному.
        [Tooltip("The gradient representing the color of the flash on hit")] [GradientUsageAttribute(true)]
        public Gradient OnHitBodyGradient;

        [Tooltip("The duration of the flash on hit")]
        public float FlashOnHitDuration = 0.5f;

        [Header("Sounds")] [Tooltip("Sound played when recieving damages")]
        public AudioClip DamageTick;

        [Header("VFX")] [Tooltip("The VFX prefab spawned when the enemy dies")]
        public GameObject DeathVfx;

        [Tooltip("The point at which the death VFX is spawned")]
        public Transform DeathVfxSpawnPoint;

        [Header("Loot")] [Tooltip("The object this enemy can drop when dying")]
        public GameObject LootPrefab;

        [Tooltip("The chance the object has to drop")] [Range(0, 1)]
        public float DropRate = 1f;

        [Header("Debug Display")] [Tooltip("Color of the sphere gizmo representing the path reaching range")]
        public Color PathReachingRangeColor = Color.yellow;

        [Tooltip("Color of the sphere gizmo representing the attack range")]
        public Color AttackRangeColor = Color.red;

        [Tooltip("Color of the sphere gizmo representing the detection range")]
        public Color DetectionRangeColor = Color.blue;

        // События для подклассов — атака, обнаружение, потеря цели, урон.
        public UnityAction onAttack;
        public UnityAction onDetectedTarget;
        public UnityAction onLostTarget;
        public UnityAction onDamaged;

        List<RendererIndexData> m_BodyRenderers = new List<RendererIndexData>();
        MaterialPropertyBlock m_BodyFlashMaterialPropertyBlock;
        float m_LastTimeDamaged = float.NegativeInfinity;

        RendererIndexData m_EyeRendererData;
        MaterialPropertyBlock m_EyeColorMaterialPropertyBlock;

        public PatrolPath PatrolPath { get; set; }
        // Прокси к DetectionModule — наследникам удобнее не идти через цепочку точек.
        public GameObject KnownDetectedTarget => DetectionModule.KnownDetectedTarget;
        public bool IsTargetInAttackRange => DetectionModule.IsTargetInAttackRange;
        public bool IsSeeingTarget => DetectionModule.IsSeeingTarget;
        public bool HadKnownTarget => DetectionModule.HadKnownTarget;
        public NavMeshAgent NavMeshAgent { get; private set; }
        public DetectionModule DetectionModule { get; private set; }

        int m_PathDestinationNodeIndex;
        IEnemyManager m_EnemyManager;
        IActorsManager m_ActorsManager;
        Health m_Health;
        Actor m_Actor;
        // Свои коллайдеры — нужно игнорировать при проверке line-of-sight, чтобы
        // враг не «врезался лучом в свою же грудь».
        Collider[] m_SelfColliders;
        IGameFlowManager m_GameFlowManager;
        bool m_WasDamagedThisFrame;
        // Дросселирование детекции: считаем не каждый кадр (см. CLAUDE.md).
        float m_NextDetectionTime;
        // Если false — Update НЕ дёргает SetPropertyBlock (экономия).
        bool m_FlashActive;
        float m_LastTimeWeaponSwapped = Mathf.NegativeInfinity;
        int m_CurrentWeaponIndex;
        WeaponController m_CurrentWeapon;
        WeaponController[] m_Weapons;
        NavigationModule m_NavigationModule;

        void Start()
        {
            // Поиск менеджеров. См. memory/project_di_vcontainer — это легаси, мигрируется на DI.
            var enemyManager = FindAnyObjectByType<EnemyManager>();
            DebugUtility.HandleErrorIfNullFindObject<EnemyManager, EnemyController>(enemyManager, this);
            m_EnemyManager = enemyManager;

            var actorsManager = FindAnyObjectByType<ActorsManager>();
            DebugUtility.HandleErrorIfNullFindObject<ActorsManager, EnemyController>(actorsManager, this);
            m_ActorsManager = actorsManager;

            // Регистрация в EnemyManager — увеличит счётчик NumberOfEnemiesTotal.
            m_EnemyManager.RegisterEnemy(this);

            m_Health = GetComponent<Health>();
            DebugUtility.HandleErrorIfNullGetComponent<Health, EnemyController>(m_Health, this, gameObject);

            m_Actor = GetComponent<Actor>();
            DebugUtility.HandleErrorIfNullGetComponent<Actor, EnemyController>(m_Actor, this, gameObject);

            NavMeshAgent = GetComponent<NavMeshAgent>();
            m_SelfColliders = GetComponentsInChildren<Collider>();

            // Snap agent to the nearest NavMesh point so the agent can initialize even
            // when the prefab is placed slightly above or off the surface.
            // Враг может быть положен чуть выше пола — Warp подтянет его на NavMesh.
            // Без этого NavMeshAgent.isOnNavMesh = false и SetDestination ничего не сделает.
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                NavMeshAgent.Warp(hit.position);
            }
            else
            {
                Debug.LogWarning($"[EnemyController] {name} could not be placed on NavMesh — no surface within 2m.", this);
            }

            var gameFlowManager = FindAnyObjectByType<GameFlowManager>();
            DebugUtility.HandleErrorIfNullFindObject<GameFlowManager, EnemyController>(gameFlowManager, this);
            m_GameFlowManager = gameFlowManager;

            // Subscribe to damage & death actions
            m_Health.OnDie += OnDie;
            m_Health.OnDamaged += OnDamaged;

            // Find and initialize all weapons
            FindAndInitializeAllWeapons();
            var weapon = GetCurrentWeapon();
            weapon.ShowWeapon(true);

            // DetectionModule должен быть РОВНО ОДИН на враге.
            var detectionModules = GetComponentsInChildren<DetectionModule>();
            DebugUtility.HandleErrorIfNoComponentFound<DetectionModule, EnemyController>(detectionModules.Length, this,
                gameObject);
            DebugUtility.HandleWarningIfDuplicateObjects<DetectionModule, EnemyController>(detectionModules.Length,
                this, gameObject);
            // Initialize detection module
            DetectionModule = detectionModules[0];
            // Прокидываем события — наш onDetectedTarget сработает, когда обнаружит.
            DetectionModule.onDetectedTarget += OnDetectedTarget;
            DetectionModule.onLostTarget += OnLostTarget;
            // А наш onAttack триггерит анимацию атаки в модуле.
            onAttack += DetectionModule.OnAttack;

            // NavigationModule (опционально) — параметры скорости/ускорения для NavMeshAgent.
            var navigationModules = GetComponentsInChildren<NavigationModule>();
            DebugUtility.HandleWarningIfDuplicateObjects<NavigationModule, EnemyController>(navigationModules.Length,
                this, gameObject);
            // Override navmesh agent data
            if (navigationModules.Length > 0)
            {
                m_NavigationModule = navigationModules[0];
                NavMeshAgent.speed = m_NavigationModule.MoveSpeed;
                NavMeshAgent.angularSpeed = m_NavigationModule.AngularSpeed;
                NavMeshAgent.acceleration = m_NavigationModule.Acceleration;
            }

            // Сбор рендереров — для эмиссии «глаза» и «тела».
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    if (renderer.sharedMaterials[i] == EyeColorMaterial)
                    {
                        m_EyeRendererData = new RendererIndexData(renderer, i);
                    }

                    if (renderer.sharedMaterials[i] == BodyMaterial)
                    {
                        m_BodyRenderers.Add(new RendererIndexData(renderer, i));
                    }
                }
            }

            m_BodyFlashMaterialPropertyBlock = new MaterialPropertyBlock();
            // Set initial body emission to the gradient's rest state (t=1)
            // Конец градиента — состояние «не бьют», стартовое.
            if (m_BodyRenderers.Count > 0)
            {
                m_BodyFlashMaterialPropertyBlock.SetColor("_EmissionColor", OnHitBodyGradient.Evaluate(1f));
                foreach (var data in m_BodyRenderers)
                    data.Renderer.SetPropertyBlock(m_BodyFlashMaterialPropertyBlock, data.MaterialIndex);
            }

            // Check if we have an eye renderer for this enemy
            if (m_EyeRendererData.Renderer != null)
            {
                m_EyeColorMaterialPropertyBlock = new MaterialPropertyBlock();
                m_EyeColorMaterialPropertyBlock.SetColor("_EmissionColor", DefaultEyeColor);
                m_EyeRendererData.Renderer.SetPropertyBlock(m_EyeColorMaterialPropertyBlock,
                    m_EyeRendererData.MaterialIndex);
            }
        }

        void Update()
        {
            EnsureIsWithinLevelBounds();

            // Дросселирование: детекция максимум 10 раз в секунду.
            // 0.1с интервал не сильно влияет на ощущение от игры, но экономит CPU
            // при большом количестве врагов в сцене. См. CLAUDE.md.
            if (Time.time >= m_NextDetectionTime)
            {
                DetectionModule.HandleTargetDetection(m_Actor, m_SelfColliders);
                m_NextDetectionTime = Time.time + 0.1f;
            }

            // Анимация вспышки. Если флаг false — пропускаем SetPropertyBlock.
            if (m_FlashActive)
            {
                float ratio = Mathf.Min((Time.time - m_LastTimeDamaged) / FlashOnHitDuration, 1f);
                m_BodyFlashMaterialPropertyBlock.SetColor("_EmissionColor", OnHitBodyGradient.Evaluate(ratio));
                foreach (var data in m_BodyRenderers)
                    data.Renderer.SetPropertyBlock(m_BodyFlashMaterialPropertyBlock, data.MaterialIndex);
                if (ratio >= 1f)
                    m_FlashActive = false;
            }

            m_WasDamagedThisFrame = false;
        }

        // Падение за карту — самоуничтожение.
        void EnsureIsWithinLevelBounds()
        {
            // at every frame, this tests for conditions to kill the enemy
            if (transform.position.y < SelfDestructYHeight)
            {
                m_EnemyManager?.UnregisterEnemy(this);
                Destroy(gameObject);
                return;
            }
        }

        // Цель потеряна — гасим красный глаз.
        void OnLostTarget()
        {
            onLostTarget.Invoke();

            // Set the eye attack color and property block if the eye renderer is set
            if (m_EyeRendererData.Renderer != null)
            {
                m_EyeColorMaterialPropertyBlock.SetColor("_EmissionColor", DefaultEyeColor);
                m_EyeRendererData.Renderer.SetPropertyBlock(m_EyeColorMaterialPropertyBlock,
                    m_EyeRendererData.MaterialIndex);
            }
        }

        // Цель обнаружена — зажигаем красный глаз.
        void OnDetectedTarget()
        {
            onDetectedTarget.Invoke();

            // Set the eye default color and property block if the eye renderer is set
            if (m_EyeRendererData.Renderer != null)
            {
                m_EyeColorMaterialPropertyBlock.SetColor("_EmissionColor", AttackEyeColor);
                m_EyeRendererData.Renderer.SetPropertyBlock(m_EyeColorMaterialPropertyBlock,
                    m_EyeRendererData.MaterialIndex);
            }
        }

        // Плавный поворот к точке. ProjectOnPlane по Vector3.up — игнорируем вертикаль
        // (враг не запрокидывает голову вверх к цели).
        public void OrientTowards(Vector3 lookPosition)
        {
            Vector3 lookDirection = Vector3.ProjectOnPlane(lookPosition - transform.position, Vector3.up).normalized;
            if (lookDirection.sqrMagnitude != 0f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation =
                    Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * OrientationSpeed);
            }
        }

        bool IsPathValid()
        {
            return PatrolPath && PatrolPath.PathNodes.Count > 0;
        }

        public void ResetPathDestination()
        {
            m_PathDestinationNodeIndex = 0;
        }

        // Найти ближайшую к нам точку на маршруте — нужно при «возвращении к патрулю»
        // (например, после преследования игрока).
        public void SetPathDestinationToClosestNode()
        {
            if (IsPathValid())
            {
                int closestPathNodeIndex = 0;
                for (int i = 0; i < PatrolPath.PathNodes.Count; i++)
                {
                    float distanceToPathNode = PatrolPath.GetDistanceToNode(transform.position, i);
                    if (distanceToPathNode < PatrolPath.GetDistanceToNode(transform.position, closestPathNodeIndex))
                    {
                        closestPathNodeIndex = i;
                    }
                }

                m_PathDestinationNodeIndex = closestPathNodeIndex;
            }
            else
            {
                m_PathDestinationNodeIndex = 0;
            }
        }

        public Vector3 GetDestinationOnPath()
        {
            if (IsPathValid())
            {
                return PatrolPath.GetPositionOfPathNode(m_PathDestinationNodeIndex);
            }
            else
            {
                return transform.position;
            }
        }

        // Безопасно установить пункт назначения. Защиты от «агент не на NavMesh'е».
        public void SetNavDestination(Vector3 destination)
        {
            if (NavMeshAgent && NavMeshAgent.isActiveAndEnabled && NavMeshAgent.isOnNavMesh)
            {
                NavMeshAgent.SetDestination(destination);
            }
        }

        // Двигать индекс маршрута, когда дошли до текущей точки. inverseOrder — для разворота.
        public void UpdatePathDestination(bool inverseOrder = false)
        {
            if (IsPathValid())
            {
                // Check if reached the path destination
                if ((transform.position - GetDestinationOnPath()).magnitude <= PathReachingRadius)
                {
                    // increment path destination index
                    m_PathDestinationNodeIndex =
                        inverseOrder ? (m_PathDestinationNodeIndex - 1) : (m_PathDestinationNodeIndex + 1);
                    // Зацикливание — переходим через ноль/конец.
                    if (m_PathDestinationNodeIndex < 0)
                    {
                        m_PathDestinationNodeIndex += PatrolPath.PathNodes.Count;
                    }

                    if (m_PathDestinationNodeIndex >= PatrolPath.PathNodes.Count)
                    {
                        m_PathDestinationNodeIndex -= PatrolPath.PathNodes.Count;
                    }
                }
            }
        }

        void OnDamaged(float damage, GameObject damageSource)
        {
            // test if the damage source is the player
            // Не реагируем на урон от другого врага (например, дружественный огонь).
            if (damageSource && !damageSource.GetComponent<EnemyController>())
            {
                // pursue the player
                DetectionModule.OnDamaged(damageSource);

                onDamaged?.Invoke();
                m_LastTimeDamaged = Time.time;

                // play the damage tick sound
                // Один тик звука за кадр — иначе при многослойном уроне (от взрыва)
                // получится «гул» из перекрывающихся звуков.
                if (DamageTick && !m_WasDamagedThisFrame)
                    AudioUtility.CreateSFX(DamageTick, transform.position, AudioUtility.AudioGroups.DamageTick, 0f);

                m_WasDamagedThisFrame = true;
                m_FlashActive = true;
            }
        }

        void OnDie()
        {
            // spawn a particle system when dying
            var vfx = Instantiate(DeathVfx, DeathVfxSpawnPoint.position, Quaternion.identity);
            Destroy(vfx, 5f);

            // tells the game flow manager to handle the enemy destuction
            m_EnemyManager.UnregisterEnemy(this);

            // loot an object
            if (TryDropItem())
            {
                Instantiate(LootPrefab, transform.position, Quaternion.identity);
            }

            // this will call the OnDestroy function
            // Задержка нужна для проигрыша анимации смерти.
            Destroy(gameObject, DeathDuration);
        }

        // Чистим все подписки. Если этого не сделать — мёртвые ссылки в делегатах.
        void OnDestroy()
        {
            if (m_Health != null)
            {
                m_Health.OnDie -= OnDie;
                m_Health.OnDamaged -= OnDamaged;
            }

            if (DetectionModule != null)
            {
                DetectionModule.onDetectedTarget -= OnDetectedTarget;
                DetectionModule.onLostTarget -= OnLostTarget;
                onAttack -= DetectionModule.OnAttack;
            }
        }

        // Рисуем три сферы в редакторе: реквест-радиус, дальность обнаружения и атаки.
        void OnDrawGizmosSelected()
        {
            // Path reaching range
            Gizmos.color = PathReachingRangeColor;
            Gizmos.DrawWireSphere(transform.position, PathReachingRadius);

            if (DetectionModule != null)
            {
                // Detection range
                Gizmos.color = DetectionRangeColor;
                Gizmos.DrawWireSphere(transform.position, DetectionModule.DetectionRange);

                // Attack range
                Gizmos.color = AttackRangeColor;
                Gizmos.DrawWireSphere(transform.position, DetectionModule.AttackRange);
            }
        }

        // Поворачиваем все оружия в точку (нужно для турелей и врагов с несколькими стволами).
        public void OrientWeaponsTowards(Vector3 lookPosition)
        {
            for (int i = 0; i < m_Weapons.Length; i++)
            {
                // orient weapon towards player
                Vector3 weaponForward = (lookPosition - m_Weapons[i].WeaponRoot.transform.position).normalized;
                m_Weapons[i].transform.forward = weaponForward;
            }
        }

        // Попытка атаки. Поворачиваем оружие, проверяем кулдаун, стреляем.
        // Возвращает true, если действительно выстрелили.
        public bool TryAttack(Vector3 enemyPosition)
        {
            if (m_GameFlowManager.GameIsEnding)
                return false;

            OrientWeaponsTowards(enemyPosition);

            // Кулдаун после смены оружия — игрок видит «бот сменил пушку и не атакует мгновенно».
            if ((m_LastTimeWeaponSwapped + DelayAfterWeaponSwap) >= Time.time)
                return false;

            // Shoot the weapon
            // HandleShootInputs(false, true, false) = «я держу спуск» — годится для авто-оружия.
            bool didFire = GetCurrentWeapon().HandleShootInputs(false, true, false);

            if (didFire && onAttack != null)
            {
                onAttack.Invoke();

                // Если включена авто-смена — переключаемся на следующее оружие после выстрела.
                if (SwapToNextWeapon && m_Weapons.Length > 1)
                {
                    int nextWeaponIndex = (m_CurrentWeaponIndex + 1) % m_Weapons.Length;
                    SetCurrentWeapon(nextWeaponIndex);
                }
            }

            return didFire;
        }

        // Случайный дроп: 0 — никогда, 1 — всегда, иначе по Random.value.
        public bool TryDropItem()
        {
            if (DropRate == 0 || LootPrefab == null)
                return false;
            else if (DropRate == 1)
                return true;
            else
                return (Random.value <= DropRate);
        }

        void FindAndInitializeAllWeapons()
        {
            // Check if we already found and initialized the weapons
            // Lazy: ищем один раз и кешируем.
            if (m_Weapons == null)
            {
                m_Weapons = GetComponentsInChildren<WeaponController>();
                DebugUtility.HandleErrorIfNoComponentFound<WeaponController, EnemyController>(m_Weapons.Length, this,
                    gameObject);

                for (int i = 0; i < m_Weapons.Length; i++)
                {
                    m_Weapons[i].Owner = gameObject;
                }
            }
        }

        public WeaponController GetCurrentWeapon()
        {
            FindAndInitializeAllWeapons();
            // Check if no weapon is currently selected
            if (m_CurrentWeapon == null)
            {
                // Set the first weapon of the weapons list as the current weapon
                SetCurrentWeapon(0);
            }

            DebugUtility.HandleErrorIfNullGetComponent<WeaponController, EnemyController>(m_CurrentWeapon, this,
                gameObject);

            return m_CurrentWeapon;
        }

        void SetCurrentWeapon(int index)
        {
            m_CurrentWeaponIndex = index;
            m_CurrentWeapon = m_Weapons[m_CurrentWeaponIndex];
            // Запоминаем время смены, чтобы TryAttack не стрельнул мгновенно.
            if (SwapToNextWeapon)
            {
                m_LastTimeWeaponSwapped = Time.time;
            }
            else
            {
                m_LastTimeWeaponSwapped = Mathf.NegativeInfinity;
            }
        }
    }
}
