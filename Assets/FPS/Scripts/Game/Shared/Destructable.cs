using UnityEngine;

namespace Unity.FPS.Game
{
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

        Renderer[] m_Renderers;
        Color[] m_OriginalColors;
        MaterialPropertyBlock m_FlashPropertyBlock;
        float m_LastTimeDamaged = float.NegativeInfinity;
        bool m_FlashActive;

        void Start()
        {
            m_Health = GetComponent<Health>();
            DebugUtility.HandleErrorIfNullGetComponent<Health, Destructable>(m_Health, this, gameObject);

            m_Health.OnDie += OnDie;
            m_Health.OnDamaged += OnDamaged;

            m_Renderers = GetComponentsInChildren<Renderer>();
            m_FlashPropertyBlock = new MaterialPropertyBlock();

            // кэшируем исходные цвета каждого рендерера
            m_OriginalColors = new Color[m_Renderers.Length];
            for (int i = 0; i < m_Renderers.Length; i++)
            {
                m_Renderers[i].GetPropertyBlock(m_FlashPropertyBlock);
                m_OriginalColors[i] = m_Renderers[i].sharedMaterial != null
                    ? m_Renderers[i].sharedMaterial.GetColor("_BaseColor")
                    : Color.white;
            }
        }

        void Update()
        {
            if (!m_FlashActive)
                return;

            float ratio = Mathf.Min((Time.time - m_LastTimeDamaged) / FlashDuration, 1f);
            for (int i = 0; i < m_Renderers.Length; i++)
            {
                Color c = Color.Lerp(HitColor, m_OriginalColors[i], ratio);
                m_FlashPropertyBlock.SetColor("_BaseColor", c);
                m_Renderers[i].SetPropertyBlock(m_FlashPropertyBlock);
            }

            if (ratio >= 1f)
                m_FlashActive = false;
        }

        void OnDamaged(float damage, GameObject damageSource)
        {
            m_LastTimeDamaged = Time.time;
            m_FlashActive = true;

            if (DamageTick)
                AudioUtility.CreateSFX(DamageTick, transform.position, AudioUtility.AudioGroups.DamageTick, 0f);
        }

        void OnDie()
        {
            Destroy(gameObject);
        }
    }
}
