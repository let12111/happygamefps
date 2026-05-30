using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    // ============================================================================
    // AmmoCounter — иконка-полоска одного оружия в HUD. Несколько таких счётчиков
    // показывают весь инвентарь игрока; активное выделяется яркостью и масштабом.
    //
    // Источник данных — WeaponController. Слушает AmmoPickupEvent чтобы обновить
    // число «носимых» патронов вне основной анимации.
    //
    // FillBarColorChange (в RequireComponent) — добавляет «вспышки» полоски при
    // пустой обойме / полном восстановлении.
    // ============================================================================
    [RequireComponent(typeof(FillBarColorChange))]
    public class AmmoCounter : MonoBehaviour
    {
        [Tooltip("CanvasGroup to fade the ammo UI")]
        public CanvasGroup CanvasGroup;

        [Tooltip("Image for the weapon icon")] public Image WeaponImage;

        [Tooltip("Image component for the background")]
        public Image AmmoBackgroundImage;

        [Tooltip("Image component to display fill ratio")]
        public Image AmmoFillImage;

        [Tooltip("Text for Weapon index")]
        public TextMeshProUGUI WeaponIndexText;

        [Tooltip("Text for Bullet Counter")]
        public TextMeshProUGUI BulletCounter;

        [Tooltip("Reload Text for Weapons with physical bullets")]
        public RectTransform Reload;

        [Header("Selection")] [Range(0, 1)] [Tooltip("Opacity when weapon not selected")]
        public float UnselectedOpacity = 0.5f;

        [Tooltip("Scale when weapon not selected")]
        public Vector3 UnselectedScale = Vector3.one * 0.8f;

        [Tooltip("Root for the control keys")] public GameObject ControlKeysRoot;

        [Header("Feedback")] [Tooltip("Component to animate the color when empty or full")]
        public FillBarColorChange FillBarColorChange;

        [Tooltip("Sharpness for the fill ratio movements")]
        public float AmmoFillMovementSharpness = 20f;

        public int WeaponCounterIndex { get; set; }

        PlayerWeaponsManager m_PlayerWeaponsManager;
        WeaponController m_Weapon;
        // Кешируем — обновляем UI только при изменении (а не каждый кадр).
        int m_LastPhysicalBullets = -1;

        void Awake()
        {
            EventManager.AddListener<AmmoPickupEvent>(OnAmmoPickup);
        }

        // Реакция на подбор патронов — обновляем счётчик носимых.
        void OnAmmoPickup(AmmoPickupEvent evt)
        {
            if (evt.Weapon == m_Weapon)
            {
                m_LastPhysicalBullets = m_Weapon.GetCarriedPhysicalBullets();
                BulletCounter.text = m_LastPhysicalBullets.ToString();
            }
        }

        public void Initialize(WeaponController weapon, int weaponIndex, PlayerWeaponsManager playerWeaponsManager)
        {
            m_Weapon = weapon;
            m_PlayerWeaponsManager = playerWeaponsManager;
            WeaponCounterIndex = weaponIndex;
            WeaponImage.sprite = weapon.WeaponIcon;
            // Если у оружия нет физических патронов — скрываем счётчик.
            if (!weapon.HasPhysicalBullets)
                BulletCounter.transform.parent.gameObject.SetActive(false);
            else
            {
                m_LastPhysicalBullets = weapon.GetCarriedPhysicalBullets();
                BulletCounter.text = m_LastPhysicalBullets.ToString();
            }

            Reload.gameObject.SetActive(false);

            // Цифра «слот» (1, 2, 3...) для подсказки клавиш.
            WeaponIndexText.text = (WeaponCounterIndex + 1).ToString();

            FillBarColorChange.Initialize(1f, m_Weapon.GetAmmoNeededToShoot());
        }

        void Update()
        {
            float currenFillRatio = m_Weapon.CurrentAmmoRatio;
            // Плавно «гонимся» за реальным числом — не дёргается на доли кадра.
            AmmoFillImage.fillAmount = Mathf.Lerp(AmmoFillImage.fillAmount, currenFillRatio,
                Time.deltaTime * AmmoFillMovementSharpness);

            // Обновляем счётчик носимых патронов при изменении.
            if (m_Weapon.HasPhysicalBullets)
            {
                int currentBullets = m_Weapon.GetCarriedPhysicalBullets();
                if (currentBullets != m_LastPhysicalBullets)
                {
                    m_LastPhysicalBullets = currentBullets;
                    BulletCounter.text = currentBullets.ToString();
                }
            }

            // Активное оружие — яркое и большое; остальные затемнены и уменьшены.
            bool isActiveWeapon = m_Weapon == m_PlayerWeaponsManager.GetActiveWeapon();

            CanvasGroup.alpha = Mathf.Lerp(CanvasGroup.alpha, isActiveWeapon ? 1f : UnselectedOpacity,
                Time.deltaTime * 10);
            transform.localScale = Vector3.Lerp(transform.localScale, isActiveWeapon ? Vector3.one : UnselectedScale,
                Time.deltaTime * 10);
            // Цифровые клавиши показываем только для НЕактивного оружия — на активное-то они не нужны.
            ControlKeysRoot.SetActive(!isActiveWeapon);

            FillBarColorChange.UpdateVisual(currenFillRatio);

            // «RELOAD» подсказка — когда обойма пуста, но патроны есть.
            Reload.gameObject.SetActive(m_Weapon.GetCarriedPhysicalBullets() > 0 && m_Weapon.GetCurrentAmmo() == 0 && m_Weapon.IsWeaponActive);
        }

        void OnDestroy()
        {
            EventManager.RemoveListener<AmmoPickupEvent>(OnAmmoPickup);
        }
    }
}
