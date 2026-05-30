using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    // ============================================================================
    // ChargedProjectileEffectsHandler — визуальный «следок» зарядки уже на самом
    // снаряде. После выстрела по InitialCharge меняет масштаб и цвет:
    //  - 0.1 заряда → маленький бледный шарик;
    //  - 1.0 заряда → большой яркий снаряд.
    //
    // Подписка на OnShoot в OnEnable/OnDisable — снаряд переиспользуется из пула,
    // поэтому подписки надо переподключать при каждой выдаче.
    // ============================================================================
    public class ChargedProjectileEffectsHandler : MonoBehaviour
    {
        [Tooltip("Object that will be affected by charging scale & color changes")]
        public GameObject ChargingObject;

        [Tooltip("Scale of the charged object based on charge")]
        public MinMaxVector3 Scale;

        [Tooltip("Color of the charged object based on charge")]
        public MinMaxColor Color;

        MeshRenderer[] m_AffectedRenderers;
        ProjectileBase m_ProjectileBase;
        MaterialPropertyBlock m_PropertyBlock;
        // Кешируем ID имени свойства — Shader.PropertyToID конвертирует один раз.
        // Иначе SetColor("_Color") каждый кадр делает хеш строки.
        static readonly int k_ColorPropertyId = Shader.PropertyToID("_Color");

        void Awake()
        {
            m_ProjectileBase = GetComponent<ProjectileBase>();
            DebugUtility.HandleErrorIfNullGetComponent<ProjectileBase, ChargedProjectileEffectsHandler>(
                m_ProjectileBase, this, gameObject);

            m_AffectedRenderers = ChargingObject.GetComponentsInChildren<MeshRenderer>();
            m_PropertyBlock = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            m_ProjectileBase.OnShoot += OnShoot;
        }

        void OnDisable()
        {
            m_ProjectileBase.OnShoot -= OnShoot;
        }

        void OnShoot()
        {
            // Применяем размер и цвет один раз при выстреле — заряд после этого
            // уже не меняется (это снаряд, не оружие).
            ChargingObject.transform.localScale = Scale.GetValueFromRatio(m_ProjectileBase.InitialCharge);
            UnityEngine.Color targetColor = Color.GetValueFromRatio(m_ProjectileBase.InitialCharge);

            // PropertyBlock — override материала без клонирования (см. Destructable.cs).
            foreach (var ren in m_AffectedRenderers)
            {
                ren.GetPropertyBlock(m_PropertyBlock);
                m_PropertyBlock.SetColor(k_ColorPropertyId, targetColor);
                ren.SetPropertyBlock(m_PropertyBlock);
            }
        }
    }
}
