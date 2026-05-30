using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.Game
{
    // ============================================================================
    // Health — компонент здоровья. Висит на любом «живом» объекте: игрок, враг,
    // разрушаемый ящик. Хранит текущее/максимальное HP, события урона/лечения/смерти.
    //
    // Идея: ОДИН класс на всё, что может умереть. UI слушает OnDamaged игрока,
    // EnemyController.OnDie вешает дроп лута, Destructable.OnDie разрушает ящик.
    // Health сам не знает, кто его слушает — все коммуникации через события.
    // ============================================================================
    public class Health : MonoBehaviour
    {
        [Tooltip("Maximum amount of health")] public float MaxHealth = 10f;

        // На какой доле HP начнётся «критическая виньетка» в UI (красная рамка).
        [Tooltip("Health ratio at which the critical health vignette starts appearing")]
        public float CriticalHealthRatio = 0.3f;

        // События в стиле Unity (UnityAction — это псевдоним обычного delegate).
        // OnDamaged: (величина урона, кто нанёс) — нужен damageSource, чтобы враги
        // могли запомнить агрессора и пометить как цель.
        public UnityAction<float, GameObject> OnDamaged;
        public UnityAction<float> OnHealed;
        public UnityAction OnDie;

        // Текущее HP. Сеттер публичный — иногда внешние системы лечат напрямую
        // (например, при респауне).
        public float CurrentHealth { get; set; }
        // Флаг неуязвимости (например, после респауна, в катсцене).
        public bool Invincible { get; set; }
        // Игрок может подобрать аптечку только если не на максимуме — иначе
        // пикап «исчезает впустую». Используется HealthPickup'ом.
        public bool CanPickup() => CurrentHealth < MaxHealth;

        // 0..1 — нормализованное HP. Удобно для шкал в UI.
        public float GetRatio() => CurrentHealth / MaxHealth;
        public bool IsCritical() => GetRatio() <= CriticalHealthRatio;

        // Защита от двойной смерти: OnDie должен сработать ровно один раз.
        bool m_IsDead;

        void Start()
        {
            // Полное HP на старте. Это здесь, а не в Awake, чтобы внешние
            // системы успели подписаться на события до инициализации.
            CurrentHealth = MaxHealth;
        }

        public void Heal(float healAmount)
        {
            // Запоминаем «было», чтобы потом узнать «реальное» количество HP,
            // на которое подняли (не больше потолка).
            float healthBefore = CurrentHealth;
            CurrentHealth += healAmount;
            // Clamp — зажать в диапазон [0, MaxHealth]. Без него лечение
            // могло бы загнать HP выше максимума.
            CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, MaxHealth);

            // call OnHeal action
            // Если реально подлечили — оповещаем подписчиков.
            // Тернарный «?.» вызывает Invoke только если OnHealed не null.
            float trueHealAmount = CurrentHealth - healthBefore;
            if (trueHealAmount > 0f)
            {
                OnHealed?.Invoke(trueHealAmount);
            }
        }

        public void TakeDamage(float damage, GameObject damageSource)
        {
            // Неуязвимость — пуля «прошла мимо».
            if (Invincible)
                return;

            float healthBefore = CurrentHealth;
            CurrentHealth -= damage;
            CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, MaxHealth);

            // call OnDamage action
            float trueDamageAmount = healthBefore - CurrentHealth;
            if (trueDamageAmount > 0f)
            {
                OnDamaged?.Invoke(trueDamageAmount, damageSource);
            }

            // После урона проверяем смерть.
            HandleDeath();
        }

        // Принудительное «убить». Используется при падении в пропасть,
        // в скриптах катсцен, в OnPlayerDeath.
        public void Kill()
        {
            CurrentHealth = 0f;

            // call OnDamage action
            // damageSource = null — некому приписать удар.
            OnDamaged?.Invoke(MaxHealth, null);

            HandleDeath();
        }

        void HandleDeath()
        {
            // Защита: если уже мертвы — не дёргаем OnDie повторно.
            if (m_IsDead)
                return;

            // call OnDie action
            if (CurrentHealth <= 0f)
            {
                m_IsDead = true;
                OnDie?.Invoke();
            }
        }
    }
}
