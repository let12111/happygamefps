using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.Game
{
    // ============================================================================
    // ProjectileBase — абстрактный базовый класс снарядов.
    // Хранит метаданные выстрела (кто, откуда, куда, с какой зарядкой) и событие
    // OnShoot. Конкретное движение и удар реализует наследник (ProjectileStandard).
    //
    // Зачем абстракт: позволяет иметь разные снаряды (стандартная пуля, ракета,
    // лазер) с общим интерфейсом «выстреливай через Shoot».
    // ============================================================================
    public abstract class ProjectileBase : MonoBehaviour
    {
        // Кто стреляет (нужен чтобы не наносить урон самому себе).
        public GameObject Owner { get; private set; }
        // Где и куда был выстрел — может пригодиться наследнику для расчётов.
        public Vector3 InitialPosition { get; private set; }
        public Vector3 InitialDirection { get; private set; }
        // Скорость дула (движение игрока в момент выстрела). Прибавляется
        // к скорости пули — иначе бегущий назад игрок «отбрасывает» свои пули вперёд.
        public Vector3 InheritedMuzzleVelocity { get; private set; }
        // Степень зарядки (для оружия с накопительным выстрелом).
        public float InitialCharge { get; private set; }

        // Событие «снаряд вылетел». На него подписаны эффекты заряженной пули
        // (ChargedProjectileEffectsHandler), чтобы настроить размер по заряду.
        // Важно: подписки на это событие должны очищаться в OnDisable —
        // снаряд переиспользуется из пула, иначе подписки накопятся.
        public UnityAction OnShoot;

        public void Shoot(WeaponController controller)
        {
            // Запоминаем все стартовые параметры — после выстрела они
            // нужны для движения, проверки самоудара и т.д.
            Owner = controller.Owner;
            InitialPosition = transform.position;
            InitialDirection = transform.forward;
            InheritedMuzzleVelocity = controller.MuzzleWorldVelocity;
            InitialCharge = controller.CurrentCharge;

            // Уведомляем подписчиков. Наследники в своих Start или OnEnable
            // могут подписаться через OnShoot += ... .
            OnShoot?.Invoke();
        }
    }
}
