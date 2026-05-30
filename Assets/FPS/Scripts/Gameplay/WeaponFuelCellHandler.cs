using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    // ============================================================================
    // WeaponFuelCellHandler — визуализирует расход топлива/ammo через «выезжающие»
    // топливные ячейки на корпусе оружия. Чем меньше ammo, тем глубже спрятаны ячейки.
    //
    // Два режима:
    //  - Simultaneous: все ячейки одновременно лерпятся по ammo ratio.
    //  - Sequential: каждая ячейка реагирует на «свой» отрезок 0..1.
    //    Например, при 4 ячейках первая реагирует на 0-0.25 ratio, вторая на 0.25-0.5, и т.д.
    //    Это даёт эффект «постепенного опустошения».
    // ============================================================================
    [RequireComponent(typeof(WeaponController))]
    public class WeaponFuelCellHandler : MonoBehaviour
    {
        [Tooltip("Retract All Fuel Cells Simultaneously")]
        public bool SimultaneousFuelCellsUsage = false;

        [Tooltip("List of GameObjects representing the fuel cells on the weapon")]
        public GameObject[] FuelCells;

        [Tooltip("Cell local position when used")]
        public Vector3 FuelCellUsedPosition;

        [Tooltip("Cell local position before use")]
        public Vector3 FuelCellUnusedPosition = new Vector3(0f, -0.1f, 0f);

        WeaponController m_Weapon;
        bool[] m_FuelCellsCooled;

        void Start()
        {
            m_Weapon = GetComponent<WeaponController>();
            DebugUtility.HandleErrorIfNullGetComponent<WeaponController, WeaponFuelCellHandler>(m_Weapon, this,
                gameObject);

            m_FuelCellsCooled = new bool[FuelCells.Length];
            for (int i = 0; i < m_FuelCellsCooled.Length; i++)
            {
                m_FuelCellsCooled[i] = true;
            }
        }

        void Update()
        {
            if (FuelCells.Length == 0)
                return;

            if (SimultaneousFuelCellsUsage)
            {
                // Все ячейки двигаются вместе.
                for (int i = 0; i < FuelCells.Length; i++)
                {
                    FuelCells[i].transform.localPosition = Vector3.Lerp(FuelCellUsedPosition, FuelCellUnusedPosition,
                        m_Weapon.CurrentAmmoRatio);
                }
            }
            else
            {
                // Каждая ячейка реагирует на «свой» 1/N интервал.
                float length = FuelCells.Length;
                for (int i = 0; i < FuelCells.Length; i++)
                {
                    // lim1..lim2 — диапазон ammo ratio, на который реагирует ячейка i.
                    float lim1 = i / length;
                    float lim2 = (i + 1) / length;

                    // InverseLerp возвращает 0..1 относительно отрезка.
                    // Если ratio ниже lim1 — 0; выше lim2 — 1; внутри — пропорция.
                    float value = Mathf.Clamp01(Mathf.InverseLerp(lim1, lim2, m_Weapon.CurrentAmmoRatio));

                    FuelCells[i].transform.localPosition =
                        Vector3.Lerp(FuelCellUsedPosition, FuelCellUnusedPosition, value);
                }
            }
        }
    }
}
