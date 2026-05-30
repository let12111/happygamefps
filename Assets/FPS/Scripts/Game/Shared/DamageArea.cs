using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Game
{
    // ============================================================================
    // DamageArea — радиальный (взрывной) урон по сфере. Висит на снарядах-гранатах,
    // на бочках, на ауре. Сам не стреляет — кто-то снаружи (ProjectileStandard
    // при взрыве) вызывает InflictDamageInArea с нужным центром и величиной.
    //
    // Особенности:
    //  - кривая падения урона по расстоянию (AnimationCurve) — гибче формулы;
    //  - NonAlloc-перекрытие сферы — буфер статичный, GC не страдает;
    //  - дедупликация по Health — если у врага 3 коллайдера, не нанесём 3 раза.
    // ============================================================================
    public class DamageArea : MonoBehaviour
    {
        [Tooltip("Area of damage when the projectile hits something")]
        public float AreaOfEffectDistance = 5f;

        // Кривая 0..1: вход — нормализованное расстояние от центра,
        // выход — множитель урона. Обычно: 1 в центре, 0 на границе.
        [Tooltip("Damage multiplier over distance for area of effect")]
        public AnimationCurve DamageRatioOverDistance;

        [Header("Debug")] [Tooltip("Color of the area of effect radius")]
        public Color AreaOfEffectColor = Color.red * 0.5f;

        // Буфер для OverlapSphereNonAlloc — переиспользуется на каждый взрыв.
        // static — общий на все DamageArea: одновременно взрывов не бывает,
        // и не хочется иметь по 64 ячейки в каждом экземпляре.
        // 64 коллайдера — потолок одного взрыва.
        static readonly Collider[] s_OverlapBuffer = new Collider[64];
        // Словарь для дедупликации: «один Health = одно повреждение».
        // readonly — ссылка не меняется, содержимое чистится через Clear.
        readonly Dictionary<Health, Damageable> m_UniqueHealths = new Dictionary<Health, Damageable>();

        public void InflictDamageInArea(float damage, Vector3 center, LayerMask layers,
            QueryTriggerInteraction interaction, GameObject owner)
        {
            // Чистим прошлый набор, не создавая новый словарь.
            m_UniqueHealths.Clear();

            // OverlapSphereNonAlloc — версия без аллокации, кладёт результаты в буфер.
            // Возвращает фактическое число найденных коллайдеров.
            int count = Physics.OverlapSphereNonAlloc(center, AreaOfEffectDistance, s_OverlapBuffer, layers, interaction);
            for (int i = 0; i < count; i++)
            {
                Damageable damageable = s_OverlapBuffer[i].GetComponent<Damageable>();
                if (damageable)
                {
                    // Идём вверх по иерархии до Health.
                    Health health = damageable.GetComponentInParent<Health>();
                    // Если у врага несколько коллайдеров с Damageable —
                    // первый зарегистрируется, остальные пропустятся.
                    if (health && !m_UniqueHealths.ContainsKey(health))
                        m_UniqueHealths.Add(health, damageable);
                }
            }

            // Apply damages with distance falloff
            // Теперь по каждому уникальному Health — урон с учётом расстояния.
            foreach (Damageable uniqueDamageable in m_UniqueHealths.Values)
            {
                float distance = Vector3.Distance(uniqueDamageable.transform.position, transform.position);
                // distance / AreaOfEffectDistance даёт 0..1, по которому
                // вычисляем множитель из кривой.
                // true → isExplosionDamage = true, чтобы не считались криты.
                uniqueDamageable.InflictDamage(
                    damage * DamageRatioOverDistance.Evaluate(distance / AreaOfEffectDistance), true, owner);
            }
        }

        // Визуализация радиуса взрыва в редакторе, когда объект выделен.
        void OnDrawGizmosSelected()
        {
            Gizmos.color = AreaOfEffectColor;
            Gizmos.DrawSphere(transform.position, AreaOfEffectDistance);
        }
    }
}
