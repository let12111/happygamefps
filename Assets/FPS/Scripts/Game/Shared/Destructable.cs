using UnityEngine;

namespace Unity.FPS.Game
{
    public class Destructable : MonoBehaviour
    {
        [Header("Hit Flash")]
        [Tooltip("Gradient of the emission color flash when hit")]
        [GradientUsageAttribute(true)]
        public Gradient OnHitGradient;

        [Tooltip("Duration of the hit flash")]
        public float FlashDuration = 0.5f;

        [Header("Audio")]
        [Tooltip("Sound played when taking damage (optional)")]
        public AudioClip DamageTick;

        Health m_Health;

        Renderer[] m_Renderers;
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
        }

        void Update()
        {
            if (!m_FlashActive)
                return;

            float ratio = Mathf.Min((Time.time - m_LastTimeDamaged) / FlashDuration, 1f);
            m_FlashPropertyBlock.SetColor("_EmissionColor", OnHitGradient.Evaluate(ratio));
            foreach (var r in m_Renderers)
                r.SetPropertyBlock(m_FlashPropertyBlock);

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
