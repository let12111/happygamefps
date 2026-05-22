using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Game
{
    public class DamageArea : MonoBehaviour
    {
        [Tooltip("Area of damage when the projectile hits something")]
        public float AreaOfEffectDistance = 5f;

        [Tooltip("Damage multiplier over distance for area of effect")]
        public AnimationCurve DamageRatioOverDistance;

        [Header("Debug")] [Tooltip("Color of the area of effect radius")]
        public Color AreaOfEffectColor = Color.red * 0.5f;

        static readonly Collider[] s_OverlapBuffer = new Collider[64];
        readonly Dictionary<Health, Damageable> m_UniqueHealths = new Dictionary<Health, Damageable>();

        public void InflictDamageInArea(float damage, Vector3 center, LayerMask layers,
            QueryTriggerInteraction interaction, GameObject owner)
        {
            m_UniqueHealths.Clear();

            int count = Physics.OverlapSphereNonAlloc(center, AreaOfEffectDistance, s_OverlapBuffer, layers, interaction);
            for (int i = 0; i < count; i++)
            {
                Damageable damageable = s_OverlapBuffer[i].GetComponent<Damageable>();
                if (damageable)
                {
                    Health health = damageable.GetComponentInParent<Health>();
                    if (health && !m_UniqueHealths.ContainsKey(health))
                        m_UniqueHealths.Add(health, damageable);
                }
            }

            // Apply damages with distance falloff
            foreach (Damageable uniqueDamageable in m_UniqueHealths.Values)
            {
                float distance = Vector3.Distance(uniqueDamageable.transform.position, transform.position);
                uniqueDamageable.InflictDamage(
                    damage * DamageRatioOverDistance.Evaluate(distance / AreaOfEffectDistance), true, owner);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = AreaOfEffectColor;
            Gizmos.DrawSphere(transform.position, AreaOfEffectDistance);
        }
    }
}