using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    // ============================================================================
    // ProjectileChargeParameters — модификация параметров снаряда по силе зарядки.
    //
    // Идея: один и тот же префаб снаряда может вылетать с разной силой.
    // Это компонент-«крышка», который при OnShoot читает InitialCharge (0..1)
    // и переписывает Damage/Radius/Speed/Gravity на ProjectileStandard.
    //
    // Так у одного снаряда могут быть «слабый» и «мощный» варианты — без копий префаба.
    // ============================================================================
    public class ProjectileChargeParameters : MonoBehaviour
    {
        // Min — слабый выстрел, Max — заряженный полностью.
        public MinMaxFloat Damage;
        public MinMaxFloat Radius;
        public MinMaxFloat Speed;
        public MinMaxFloat GravityDownAcceleration;
        public MinMaxFloat AreaOfEffectDistance;

        ProjectileBase m_ProjectileBase;

        // OnEnable вместо Awake — потому что снаряд пуленный, и подписаться надо при каждой выдаче.
        void OnEnable()
        {
            m_ProjectileBase = GetComponent<ProjectileBase>();
            DebugUtility.HandleErrorIfNullGetComponent<ProjectileBase, ProjectileChargeParameters>(m_ProjectileBase,
                this, gameObject);

            m_ProjectileBase.OnShoot += OnShoot;
        }

        // Замечание: тут НЕТ OnDisable -= OnShoot — это потенциальная утечка подписки.
        // (можно поправить, если будут проблемы при возврате в пул).

        void OnShoot()
        {
            // Apply the parameters based on projectile charge
            ProjectileStandard proj = GetComponent<ProjectileStandard>();
            if (proj)
            {
                proj.Damage = Damage.GetValueFromRatio(m_ProjectileBase.InitialCharge);
                proj.Radius = Radius.GetValueFromRatio(m_ProjectileBase.InitialCharge);
                proj.Speed = Speed.GetValueFromRatio(m_ProjectileBase.InitialCharge);
                proj.GravityDownAcceleration =
                    GravityDownAcceleration.GetValueFromRatio(m_ProjectileBase.InitialCharge);
            }
        }
    }
}
