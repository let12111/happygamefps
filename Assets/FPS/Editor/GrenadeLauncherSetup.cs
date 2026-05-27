using System.IO;
using System.Linq;
using Unity.FPS.Game;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FPS.EditorTools
{
    public static class GrenadeLauncherSetup
    {
        const string FbxPath = "Assets/FPS/ModelsTest/rpgmodel.fbx";
        const string PrefabPath = "Assets/FPS/Prefabs/Weapons/Weapon_GrenadeLauncher.prefab";
        const string ControllerPath = "Assets/FPS/Animation/Controllers/GrenadeLauncher_AnimationController.controller";

        [MenuItem("Tools/FPS/Setup Grenade Launcher")]
        public static void Setup()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null)
            {
                Debug.LogError($"[GrenadeLauncherSetup] FBX not found at {FbxPath}");
                return;
            }

            var weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (weaponPrefab == null)
            {
                Debug.LogError($"[GrenadeLauncherSetup] Weapon prefab not found at {PrefabPath}");
                return;
            }

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

            using (var scope = new PrefabUtility.EditPrefabContentsScope(PrefabPath))
            {
                var root = scope.prefabContentsRoot;
                var gunRoot = root.transform.Find("GunRoot");
                if (gunRoot == null)
                {
                    Debug.LogError("[GrenadeLauncherSetup] GunRoot child not found in prefab");
                    return;
                }

                GameObject modelInstance;
                if (gunRoot.childCount == 0)
                {
                    modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbx, gunRoot);
                    modelInstance.transform.localPosition = Vector3.zero;
                    modelInstance.transform.localRotation = Quaternion.identity;
                    modelInstance.transform.localScale = Vector3.one;
                    SetLayerRecursively(modelInstance, 10);
                    Debug.Log("[GrenadeLauncherSetup] Embedded rpgmodel.fbx under GunRoot");
                }
                else
                {
                    modelInstance = gunRoot.GetChild(0).gameObject;
                    Debug.Log($"[GrenadeLauncherSetup] Using existing model under GunRoot: {modelInstance.name}");
                }

                var animator = modelInstance.GetComponent<Animator>();
                if (animator == null)
                    animator = modelInstance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var weaponController = root.GetComponent<WeaponController>();
                weaponController.WeaponAnimator = animator;
                EditorUtility.SetDirty(weaponController);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GrenadeLauncherSetup] Done. Open Weapon_GrenadeLauncher prefab — model + Animator are wired.");
        }

        static bool MatchesAction(string clipName, string action)
        {
            var lower = clipName.ToLowerInvariant();
            var act = action.ToLowerInvariant();
            return lower == act || lower.EndsWith("|" + act) || lower.EndsWith("_" + act) || lower.Contains(act);
        }

        static AnimatorController CreateController(AnimationClip fireClip, AnimationClip reloadClip)
        {
            var dir = Path.GetDirectoryName(ControllerPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Reload", AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;
            sm.entryPosition = new Vector3(50, 0, 0);
            sm.exitPosition = new Vector3(800, 0, 0);
            sm.anyStatePosition = new Vector3(50, 100, 0);

            var idleState = sm.AddState("Idle", new Vector3(300, 100, 0));
            var shootState = sm.AddState("Shoot", new Vector3(550, 50, 0));
            shootState.motion = fireClip;
            var reloadState = sm.AddState("Reload", new Vector3(550, 200, 0));
            reloadState.motion = reloadClip;
            sm.defaultState = idleState;

            var idleToShoot = idleState.AddTransition(shootState);
            idleToShoot.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            idleToShoot.hasExitTime = false;
            idleToShoot.duration = 0.05f;
            idleToShoot.canTransitionToSelf = false;

            var shootToReload = shootState.AddTransition(reloadState);
            shootToReload.hasExitTime = true;
            shootToReload.exitTime = 0.95f;
            shootToReload.duration = 0.1f;

            var reloadToIdle = reloadState.AddTransition(idleState);
            reloadToIdle.hasExitTime = true;
            reloadToIdle.exitTime = 0.95f;
            reloadToIdle.duration = 0.1f;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
}
