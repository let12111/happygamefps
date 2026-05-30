using UnityEngine;

namespace Unity.FPS.Game
{
    // ============================================================================
    // Damageable — «приёмник» урона. Висит на коллайдере, через который наносят урон.
    //
    // Зачем отдельно от Health: один враг = одно Health, но коллайдеров на нём
    // может быть много (голова, тело, конечности), и у каждого свой множитель
    // (хед-шот x2, нога x0.5). Damageable хранит этот множитель и пересылает
    // итоговый урон в Health (который обычно лежит на корневом объекте).
    //
    // Поток: пуля → коллайдер → Damageable.InflictDamage → Health.TakeDamage.
    // ============================================================================
    public class Damageable : MonoBehaviour
    {
        // Множитель урона. 2.0 — это хед-шот, 0.5 — ноги, 1.0 — обычное тело.
        [Tooltip("Multiplier to apply to the received damage")]
        public float DamageMultiplier = 1f;

        // Чувствительность к самонанесённому урону (например, граната под ноги).
        // [Range(0,1)] в Unity рисует слайдер в Inspector — нагляднее, чем числовое поле.
        [Range(0, 1)] [Tooltip("Multiplier to apply to self damage")]
        public float SensibilityToSelfdamage = 0.5f;

        // Ссылка на «настоящее» Health. private set — внешние системы только читают.
        public Health Health { get; private set; }

        void Awake()
        {
            // find the health component either at the same level, or higher in the hierarchy
            // Сначала ищем Health на этом же объекте.
            Health = GetComponent<Health>();
            // Если нет — у родителя. Типичный случай: голова — дочерний объект тела.
            if (!Health)
            {
                Health = GetComponentInParent<Health>();
            }
        }

        // Главный метод: «возьми damage и переправь в Health».
        public void InflictDamage(float damage, bool isExplosionDamage, GameObject damageSource)
        {
            // Если Health не нашли — урон уходит «в никуда». Это норма для
            // декораций с коллайдером, но без HP.
            if (Health)
            {
                var totalDamage = damage;

                // skip the crit multiplier if it's from an explosion
                // Взрыв не даёт критов — нельзя «хед-шотнуть» гранатой.
                // Это игровая условность для баланса.
                if (!isExplosionDamage)
                {
                    totalDamage *= DamageMultiplier;
                }

                // potentially reduce damages if inflicted by self
                // Сам себе нанёс — снижаем урон (например, прыжок с ракетницы).
                if (Health.gameObject == damageSource)
                {
                    totalDamage *= SensibilityToSelfdamage;
                }

                // apply the damages
                Health.TakeDamage(totalDamage, damageSource);
            }
        }
    }
}
