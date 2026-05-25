using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
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
            ChargingObject.transform.localScale = Scale.GetValueFromRatio(m_ProjectileBase.InitialCharge);
            UnityEngine.Color targetColor = Color.GetValueFromRatio(m_ProjectileBase.InitialCharge);

            foreach (var ren in m_AffectedRenderers)
            {
                ren.GetPropertyBlock(m_PropertyBlock);
                m_PropertyBlock.SetColor(k_ColorPropertyId, targetColor);
                ren.SetPropertyBlock(m_PropertyBlock);
            }
        }
    }
}