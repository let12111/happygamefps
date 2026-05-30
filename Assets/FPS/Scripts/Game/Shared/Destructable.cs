using UnityEngine;

namespace Unity.FPS.Game
{
    // ============================================================================
    // Destructable — простой разрушаемый объект (ящик, бочка). Слушает Health
    // и делает две вещи: при уроне мигает красным, при смерти удаляет себя.
    //
    // Тонкость с MaterialPropertyBlock: чтобы не плодить копии материалов
    // (каждый объект-копия = доп. память + ломает batching), мы меняем цвет
    // через PropertyBlock — это override поверх материала без его клонирования.
    // ============================================================================
    public class Destructable : MonoBehaviour
    {
        [Header("Hit Flash")]
        [Tooltip("Color of the flash when hit")]
        public Color HitColor = new Color(1f, 0.2f, 0.2f, 1f);

        [Tooltip("Duration of the hit flash")]
        public float FlashDuration = 0.3f;

        [Header("Audio")]
        [Tooltip("Sound played when taking damage (optional)")]
        public AudioClip DamageTick;

        Health m_Health;

        // Все рендереры на объекте (могут быть в дочерних мешах).
        Renderer[] m_Renderers;
        // Кеш «нормальных» цветов, чтобы вернуть их по окончании вспышки.
        Color[] m_OriginalColors;
        // PropertyBlock — переиспользуемый контейнер для override-свойств шейдера.
        MaterialPropertyBlock m_FlashPropertyBlock;
        // Когда последний раз ударили — для расчёта прогресса вспышки.
        float m_LastTimeDamaged = float.NegativeInfinity;
        // Идёт ли сейчас вспышка. Когда false — Update не делает работу
        // (оптимизация: не дёргаем SetPropertyBlock каждый кадр зря).
        bool m_FlashActive;

        void Start()
        {
            m_Health = GetComponent<Health>();
            DebugUtility.HandleErrorIfNullGetComponent<Health, Destructable>(m_Health, this, gameObject);

            // Подписываемся на события Health через +=. Это значит «добавить
            // в список вызываемых». Если бы было =, мы бы вытерли других слушателей.
            m_Health.OnDie += OnDie;
            m_Health.OnDamaged += OnDamaged;

            m_Renderers = GetComponentsInChildren<Renderer>();
            m_FlashPropertyBlock = new MaterialPropertyBlock();

            // кэшируем исходные цвета каждого рендерера
            m_OriginalColors = new Color[m_Renderers.Length];
            for (int i = 0; i < m_Renderers.Length; i++)
            {
                // GetPropertyBlock читает текущие override-значения, если они есть.
                m_Renderers[i].GetPropertyBlock(m_FlashPropertyBlock);
                // _BaseColor — стандартное имя свойства цвета в URP.
                m_OriginalColors[i] = m_Renderers[i].sharedMaterial != null
                    ? m_Renderers[i].sharedMaterial.GetColor("_BaseColor")
                    : Color.white;
            }
        }

        void Update()
        {
            // Ранний выход, когда вспышки нет — экономим CPU.
            if (!m_FlashActive)
                return;

            // ratio: 0 — момент удара, 1 — конец вспышки.
            float ratio = Mathf.Min((Time.time - m_LastTimeDamaged) / FlashDuration, 1f);
            for (int i = 0; i < m_Renderers.Length; i++)
            {
                // Lerp от HitColor к нормальному — плавный «выход» из красного.
                Color c = Color.Lerp(HitColor, m_OriginalColors[i], ratio);
                m_FlashPropertyBlock.SetColor("_BaseColor", c);
                m_Renderers[i].SetPropertyBlock(m_FlashPropertyBlock);
            }

            // Достигли конца — выключаем флаг до следующего удара.
            if (ratio >= 1f)
                m_FlashActive = false;
        }

        void OnDamaged(float damage, GameObject damageSource)
        {
            m_LastTimeDamaged = Time.time;
            m_FlashActive = true;

            // Опциональный «тик» звука получения урона.
            // spatialBlend = 0 → 2D (играет на любой громкости независимо от позиции).
            if (DamageTick)
                AudioUtility.CreateSFX(DamageTick, transform.position, AudioUtility.AudioGroups.DamageTick, 0f);
        }

        void OnDie()
        {
            // Простое удаление. В сложном случае тут можно было бы спавнить
            // обломки, VFX, и т.д. — Destructable это базовый вариант.
            Destroy(gameObject);
        }
    }
}
