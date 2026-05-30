using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Unity.FPS.UI
{
    // Composition root for VContainer DI.
    // Add this component to a GameObject only in gameplay scenes (MainScene, SecondaryScene).
    // ============================================================================
    // GameLifetimeScope — «корневой контейнер» VContainer для DI в игровых сценах.
    //
    // Зачем: до этого скрипты искали зависимости через FindAnyObjectByType, что:
    //  - медленно (поиск по всей сцене);
    //  - плохо тестируется (нельзя подменить на мок);
    //  - не показывает зависимости явно.
    //
    // Эта рефакторинг-миграция отражена в memory/project_di_vcontainer.md.
    //
    // LifetimeScope автоматически вызывает Configure() при загрузке сцены.
    // Мы регистрируем все сервисы как Instance'ы (уже существующие в сцене),
    // потом в BuildCallback вкатываем зависимости во все потребители.
    // ============================================================================
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // --- Register services (injectable by others) ---
            // FindObjectsInactive.Include — менеджер мог стоять на выключенном
            // объекте (например, до загрузки уровня). Без Include мы бы его не нашли.
            //
            // AsImplementedInterfaces — регистрируем под все имплементируемые интерфейсы
            // (IActorsManager, IGameFlowManager). AsSelf — ещё и под сам класс.
            // Это позволяет инжектить как «по интерфейсу», так и «по конкретному типу».

            var actorsManager = FindAnyObjectByType<ActorsManager>(FindObjectsInactive.Include);
            if (actorsManager) builder.RegisterInstance(actorsManager).AsImplementedInterfaces().AsSelf();

            var gameFlowManager = FindAnyObjectByType<GameFlowManager>(FindObjectsInactive.Include);
            if (gameFlowManager) builder.RegisterInstance(gameFlowManager).AsImplementedInterfaces().AsSelf();

            var audioManager = FindAnyObjectByType<AudioManager>(FindObjectsInactive.Include);
            if (audioManager) builder.RegisterInstance(audioManager);

            var enemyManager = FindAnyObjectByType<EnemyManager>(FindObjectsInactive.Include);
            if (enemyManager) builder.RegisterInstance(enemyManager).AsImplementedInterfaces().AsSelf();

            var playerController = FindAnyObjectByType<PlayerCharacterController>(FindObjectsInactive.Include);
            if (playerController) builder.RegisterInstance(playerController);

            var playerInput = FindAnyObjectByType<PlayerInputHandler>(FindObjectsInactive.Include);
            if (playerInput) builder.RegisterInstance(playerInput);

            var weaponsManager = FindAnyObjectByType<PlayerWeaponsManager>(FindObjectsInactive.Include);
            if (weaponsManager) builder.RegisterInstance(weaponsManager);

            var jetpack = FindAnyObjectByType<Jetpack>(FindObjectsInactive.Include);
            if (jetpack) builder.RegisterInstance(jetpack);

            var compass = FindAnyObjectByType<Compass>(FindObjectsInactive.Include);
            if (compass) builder.RegisterInstance(compass);

            var framerateCounter = FindAnyObjectByType<FramerateCounter>(FindObjectsInactive.Include);
            if (framerateCounter) builder.RegisterInstance(framerateCounter);

            // --- Inject dependencies into scene components after container is built ---
            // VContainer сам не вкатывает зависимости в существующие на сцене MonoBehaviour'ы —
            // только в те, что он сам создал. Поэтому пробегаем по всем потребителям руками.
            builder.RegisterBuildCallback(InjectSceneComponents);
        }

        void InjectSceneComponents(IObjectResolver container)
        {
            InjectAll<PlayerInputHandler>(container);
            InjectAll<Compass>(container);
            InjectAll<CompassElement>(container);
            InjectAll<CrosshairManager>(container);
            InjectAll<EnemyCounter>(container);
            InjectAll<FeedbackFlashHUD>(container);
            InjectAll<InGameMenuManager>(container);
            InjectAll<JetpackCounter>(container);
            InjectAll<NotificationHUDManager>(container);
            InjectAll<PlayerHealthBar>(container);
            InjectAll<StanceHUD>(container);
            InjectAll<WeaponHUDManager>(container);
        }

        // Generic-helper: для каждого экземпляра T в сцене вызвать container.Inject —
        // это найдёт [Inject]-методы и вызовет их с подходящими зависимостями.
        void InjectAll<T>(IObjectResolver container) where T : MonoBehaviour
        {
            foreach (var c in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                container.Inject(c);
        }
    }
}
