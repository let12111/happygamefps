using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Unity.FPS.UI
{
    // ============================================================================
    // CrosshairManager — управляет прицелом в центре экрана.
    //
    // Два состояния: «обычный» и «целюсь во врага» — оба настроены прямо на
    // WeaponController (CrosshairDataDefault / CrosshairDataTargetInSight).
    // Когда PlayerWeaponsManager.IsPointingAtEnemy меняется, мы плавно
    // переходим между двумя наборами (sprite + size + color).
    // ============================================================================
    public class CrosshairManager : MonoBehaviour
    {
        public Image CrosshairImage;
        // Заменяем sprite на эту заглушку когда оружия нет вовсе.
        public Sprite NullCrosshairSprite;
        public float CrosshairUpdateSharpness = 5f;

        PlayerWeaponsManager m_WeaponsManager;
        bool m_WasPointingAtEnemy;
        RectTransform m_CrosshairRectTransform;
        CrosshairData m_CrosshairDataDefault;
        CrosshairData m_CrosshairDataTarget;
        // Текущий набор — к нему мы лерпимся.
        CrosshairData m_CurrentCrosshair;

        [Inject]
        public void Construct(PlayerWeaponsManager weaponsManager)
        {
            m_WeaponsManager = weaponsManager;
        }

        void Start()
        {
            m_CrosshairRectTransform = CrosshairImage.GetComponent<RectTransform>();
            DebugUtility.HandleErrorIfNullGetComponent<RectTransform, CrosshairManager>(m_CrosshairRectTransform,
                this, CrosshairImage.gameObject);

            // Сразу подцепляем данные текущего оружия (если есть).
            OnWeaponChanged(m_WeaponsManager.GetActiveWeapon());

            m_WeaponsManager.OnSwitchedToWeapon += OnWeaponChanged;
        }

        void OnDestroy()
        {
            if (m_WeaponsManager != null)
                m_WeaponsManager.OnSwitchedToWeapon -= OnWeaponChanged;
        }

        void Update()
        {
            UpdateCrosshairPointingAtEnemy(false);
            // Запоминаем, чтобы в следующем кадре заметить переход.
            m_WasPointingAtEnemy = m_WeaponsManager.IsPointingAtEnemy;
        }

        void UpdateCrosshairPointingAtEnemy(bool force)
        {
            // Без default-крестика нечего показывать.
            if (m_CrosshairDataDefault.CrosshairSprite == null)
                return;

            // Был НЕ на враге → стал на враге — переключаем sprite моментально.
            if ((force || !m_WasPointingAtEnemy) && m_WeaponsManager.IsPointingAtEnemy)
            {
                m_CurrentCrosshair = m_CrosshairDataTarget;
                CrosshairImage.sprite = m_CurrentCrosshair.CrosshairSprite;
                m_CrosshairRectTransform.sizeDelta = m_CurrentCrosshair.CrosshairSize * Vector2.one;
            }
            // Обратный переход.
            else if ((force || m_WasPointingAtEnemy) && !m_WeaponsManager.IsPointingAtEnemy)
            {
                m_CurrentCrosshair = m_CrosshairDataDefault;
                CrosshairImage.sprite = m_CurrentCrosshair.CrosshairSprite;
                m_CrosshairRectTransform.sizeDelta = m_CurrentCrosshair.CrosshairSize * Vector2.one;
            }

            // Цвет и размер плавно лерпятся к целевым (между переключениями sprite).
            CrosshairImage.color = Color.Lerp(CrosshairImage.color, m_CurrentCrosshair.CrosshairColor,
                Time.deltaTime * CrosshairUpdateSharpness);

            m_CrosshairRectTransform.sizeDelta = Mathf.Lerp(m_CrosshairRectTransform.sizeDelta.x,
                m_CurrentCrosshair.CrosshairSize,
                Time.deltaTime * CrosshairUpdateSharpness) * Vector2.one;
        }

        void OnWeaponChanged(WeaponController newWeapon)
        {
            if (newWeapon)
            {
                CrosshairImage.enabled = true;
                m_CrosshairDataDefault = newWeapon.CrosshairDataDefault;
                m_CrosshairDataTarget = newWeapon.CrosshairDataTargetInSight;
            }
            else
            {
                // Без оружия — либо ставим заглушку, либо вообще прячем прицел.
                if (NullCrosshairSprite)
                {
                    CrosshairImage.sprite = NullCrosshairSprite;
                }
                else
                {
                    CrosshairImage.enabled = false;
                }
            }

            // force=true — моментальный пересчёт без оглядки на m_WasPointingAtEnemy.
            UpdateCrosshairPointingAtEnemy(true);
        }
    }
}
