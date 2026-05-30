using System.IO;
using System.Linq;
using Unity.FPS.Game;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FPS.EditorTools
{
    // ============================================================================
    // GrenadeLauncherSetup — РЕДАКТОРНЫЙ инструмент. Лежит в Assets/FPS/Editor/,
    // поэтому компилируется ТОЛЬКО для редактора Unity, в билд игры не попадает.
    //
    // Что делает: автоматическая настройка префаба гранатомёта:
    //  1) Берёт FBX-модель;
    //  2) Извлекает из неё клипы анимации Fire и Reload;
    //  3) Создаёт AnimatorController с состояниями Idle/Shoot/Reload и переходами;
    //  4) Внедряет модель в префаб Weapon_GrenadeLauncher под GunRoot;
    //  5) Связывает Animator с WeaponController.
    //
    // Это однократная утилита для подготовки контента. Удобнее, чем настраивать
    // всё руками в инспекторе.
    //
    // [MenuItem("Tools/FPS/Setup Grenade Launcher")] добавляет пункт в верхнее меню Unity.
    // ============================================================================
    public static class GrenadeLauncherSetup
    {
        // Жёстко прописанные пути — это инструмент под один конкретный ассет.
        const string FbxPath = "Assets/FPS/ModelsTest/rpgmodel.fbx";
        const string PrefabPath = "Assets/FPS/Prefabs/Weapons/Weapon_GrenadeLauncher.prefab";
        const string ControllerPath = "Assets/FPS/Animation/Controllers/GrenadeLauncher_AnimationController.controller";

        [MenuItem("Tools/FPS/Setup Grenade Launcher")]
        public static void Setup()
        {
            // Загружаем FBX-ассет.
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null)
            {
                Debug.LogError($"[GrenadeLauncherSetup] FBX not found at {FbxPath}");
                return;
            }

            // Загружаем целевой префаб оружия.
            var weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (weaponPrefab == null)
            {
                Debug.LogError($"[GrenadeLauncherSetup] Weapon prefab not found at {PrefabPath}");
                return;
            }

            // Достаём все AnimationClip'ы из FBX. LoadAllAssetsAtPath возвращает
            // и сабассеты — в FBX'е они есть. Фильтруем __preview__ — это
            // внутренние превью Unity, не настоящие клипы.
            var clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToArray();

            if (clips.Length == 0)
            {
                Debug.LogError("[GrenadeLauncherSetup] No animation clips in FBX. Check FBX importer 'Import Animation' setting.");
                return;
            }

            Debug.Log("[GrenadeLauncherSetup] Found clips: " + string.Join(", ", clips.Select(c => c.name)));

            // Ищем клипы по имени. FireRpg в названии — побочный клип (например,
            // «полёт ракеты»), мы предпочитаем чистый Fire. Если нет — берём любой Fire.
            var fireClip = clips.FirstOrDefault(c => MatchesAction(c.name, "Fire") && !c.name.Contains("FireRpg"))
                           ?? clips.FirstOrDefault(c => MatchesAction(c.name, "Fire"));
            var reloadClip = clips.FirstOrDefault(c => MatchesAction(c.name, "Reload"));

            if (fireClip == null || reloadClip == null)
            {
                Debug.LogError("[GrenadeLauncherSetup] Couldn't find Fire/Reload clips by name.");
                return;
            }

            Debug.Log($"[GrenadeLauncherSetup] Using Fire='{fireClip.name}', Reload='{reloadClip.name}'");

            var controller = CreateController(fireClip, reloadClip);

            // EditPrefabContentsScope открывает префаб для редактирования и
            // автоматически сохраняет его в Dispose (отсюда using). Это правильный
            // способ менять префабы из кода в Unity 2018.3+.
            using (var scope = new PrefabUtility.EditPrefabContentsScope(PrefabPath))
            {
                var root = scope.prefabContentsRoot;
                var gunRoot = root.transform.Find("GunRoot");
                if (gunRoot == null)
                {
                    Debug.LogError("[GrenadeLauncherSetup] GunRoot child not found in prefab");
                    return;
                }

                // Если модели ещё нет под GunRoot — вставляем FBX и обнуляем transform.
                GameObject modelInstance;
                if (gunRoot.childCount == 0)
                {
                    modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbx, gunRoot);
                    modelInstance.transform.localPosition = Vector3.zero;
                    modelInstance.transform.localRotation = Quaternion.identity;
                    modelInstance.transform.localScale = Vector3.one;
                    // 10 — слой FPS-оружия (рисуется отдельной WeaponCamera).
                    SetLayerRecursively(modelInstance, 10);
                    Debug.Log("[GrenadeLauncherSetup] Embedded rpgmodel.fbx under GunRoot");
                }
                else
                {
                    // Модель уже там — переиспользуем.
                    modelInstance = gunRoot.GetChild(0).gameObject;
                    Debug.Log($"[GrenadeLauncherSetup] Using existing model under GunRoot: {modelInstance.name}");
                }

                // Привязываем Animator с нашим контроллером.
                var animator = modelInstance.GetComponent<Animator>();
                if (animator == null)
                    animator = modelInstance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                // AlwaysAnimate — оружие в FPS-камере не отсекается стандартным
                // frustum culling, поэтому нужно принудительно анимировать его.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var weaponController = root.GetComponent<WeaponController>();
                weaponController.WeaponAnimator = animator;
                // SetDirty — пометить ассет как изменённый, чтобы Unity его сохранил.
                EditorUtility.SetDirty(weaponController);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GrenadeLauncherSetup] Done. Open Weapon_GrenadeLauncher prefab — model + Animator are wired.");
        }

        // Эвристика «похоже на действие»: точное совпадение или суффикс/подстрока.
        // Это терпимо к именованию вроде "rpg|Fire", "rpg_Fire", "FireAnim" и т.п.
        static bool MatchesAction(string clipName, string action)
        {
            var lower = clipName.ToLowerInvariant();
            var act = action.ToLowerInvariant();
            return lower == act || lower.EndsWith("|" + act) || lower.EndsWith("_" + act) || lower.Contains(act);
        }

        static AnimatorController CreateController(AnimationClip fireClip, AnimationClip reloadClip)
        {
            // Убеждаемся что папка существует, иначе создаём.
            var dir = Path.GetDirectoryName(ControllerPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            // Если контроллер уже есть — удаляем и пересоздаём. Это идемпотентность:
            // запуск утилиты повторно даёт тот же результат.
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            // Триггеры — параметры, которые «срабатывают» один раз и сбрасываются.
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Reload", AnimatorControllerParameterType.Trigger);

            // Настраиваем визуальный layout, чтобы в редакторе блоки не лезли друг на друга.
            var sm = controller.layers[0].stateMachine;
            sm.entryPosition = new Vector3(50, 0, 0);
            sm.exitPosition = new Vector3(800, 0, 0);
            sm.anyStatePosition = new Vector3(50, 100, 0);

            // Три состояния: Idle, Shoot, Reload.
            var idleState = sm.AddState("Idle", new Vector3(300, 100, 0));
            var shootState = sm.AddState("Shoot", new Vector3(550, 50, 0));
            shootState.motion = fireClip;
            var reloadState = sm.AddState("Reload", new Vector3(550, 200, 0));
            reloadState.motion = reloadClip;
            sm.defaultState = idleState;

            // Idle → Shoot по триггеру Attack.
            // hasExitTime=false → переходим МОМЕНТАЛЬНО (не дожидаясь конца Idle).
            var idleToShoot = idleState.AddTransition(shootState);
            idleToShoot.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            idleToShoot.hasExitTime = false;
            idleToShoot.duration = 0.05f;
            idleToShoot.canTransitionToSelf = false;

            // Shoot → Reload автоматически на 95% длительности Shoot.
            var shootToReload = shootState.AddTransition(reloadState);
            shootToReload.hasExitTime = true;
            shootToReload.exitTime = 0.95f;
            shootToReload.duration = 0.1f;

            // Reload → Idle также автоматически на 95%.
            var reloadToIdle = reloadState.AddTransition(idleState);
            reloadToIdle.hasExitTime = true;
            reloadToIdle.exitTime = 0.95f;
            reloadToIdle.duration = 0.1f;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        // Рекурсивный обход иерархии — присваиваем слой объекту и всем его детям.
        static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
}
