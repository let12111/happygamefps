using UnityEngine;
using System.Collections.Generic;
using Unity.FPS.Game;

namespace Unity.FPS.Gameplay
{
    // ============================================================================
    // OverheatBehavior — визуализация перегрева оружия (например, миниган).
    // По мере «расходования» ammo:
    //  - меняется цвет emission на материале (краснеет);
    //  - усиливается эмиссия пара (steam VFX);
    //  - проигрывается звук охлаждения когда оружие IsCooling.
    //
    // Хитрая структура m_OverheatingRenderersData: один меш может иметь несколько
    // материалов, мы помним ИНДЕКС нужного материала чтобы PropertyBlock применился
    // только к нему.
    // ============================================================================
    public class OverheatBehavior : MonoBehaviour
    {
        // Пара «рендерер + индекс материала». Inspector рисует её
        // как два поля рядом благодаря [Serializable].
        [System.Serializable]
        public struct RendererIndexData
        {
            public Renderer Renderer;
            public int MaterialIndex;

            public RendererIndexData(Renderer renderer, int index)
            {
                this.Renderer = renderer;
                this.MaterialIndex = index;
            }
        }

        [Header("Visual")] [Tooltip("The VFX to scale the spawn rate based on the ammo ratio")]
        public ParticleSystem SteamVfx;

        [Tooltip("The emission rate for the effect when fully overheated")]
        public float SteamVfxEmissionRateMax = 8f;

        //Set gradient field to HDR
        // HDR-градиент позволяет цветам быть ярче белого — для эмиссии светящихся материалов.
        [GradientUsage(true)] [Tooltip("Overheat color based on ammo ratio")]
        public Gradient OverheatGradient;

        // Материал, чьи копии менять. Ищем все рендереры, использующие его.
        [Tooltip("The material for overheating color animation")]
        public Material OverheatingMaterial;

        [Header("Sound")] [Tooltip("Sound played when a cell are cooling")]
        public AudioClip CoolingCellsSound;

        // Кривая для перевода ammo ratio в громкость — нелинейность даёт более естественный звук.
        [Tooltip("Curve for ammo to volume ratio")]
        public AnimationCurve AmmoToVolumeRatioCurve;


        WeaponController m_Weapon;
        AudioSource m_AudioSource;
        List<RendererIndexData> m_OverheatingRenderersData;
        MaterialPropertyBlock m_OverheatMaterialPropertyBlock;
        float m_LastAmmoRatio;
        ParticleSystem.EmissionModule m_SteamVfxEmissionModule;

        void Awake()
        {
            // Гасим эмиссию на старте.
            var emissionModule = SteamVfx.emission;
            emissionModule.rateOverTimeMultiplier = 0f;

            // Собираем все Renderer'ы, использующие OverheatingMaterial.
            // (true в GetComponentsInChildren = включая неактивные).
            m_OverheatingRenderersData = new List<RendererIndexData>();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    if (renderer.sharedMaterials[i] == OverheatingMaterial)
                        m_OverheatingRenderersData.Add(new RendererIndexData(renderer, i));
                }
            }

            m_OverheatMaterialPropertyBlock = new MaterialPropertyBlock();
            m_SteamVfxEmissionModule = SteamVfx.emission;

            m_Weapon = GetComponent<WeaponController>();
            DebugUtility.HandleErrorIfNullGetComponent<WeaponController, OverheatBehavior>(m_Weapon, this, gameObject);

            m_AudioSource = gameObject.AddComponent<AudioSource>();
            m_AudioSource.clip = CoolingCellsSound;
            m_AudioSource.outputAudioMixerGroup = AudioUtility.GetAudioGroup(AudioUtility.AudioGroups.WeaponOverheat);
        }

        void Update()
        {
            // visual smoke shooting out of the gun
            float currentAmmoRatio = m_Weapon.CurrentAmmoRatio;
            // Меняем только когда ratio изменился — экономим SetPropertyBlock.
            if (currentAmmoRatio != m_LastAmmoRatio)
            {
                // 1 - ratio: пустая обойма = максимальный перегрев = конец градиента.
                m_OverheatMaterialPropertyBlock.SetColor("_EmissionColor",
                    OverheatGradient.Evaluate(1f - currentAmmoRatio));

                foreach (var data in m_OverheatingRenderersData)
                {
                    // SetPropertyBlock с индексом — применить только к нужному материалу.
                    data.Renderer.SetPropertyBlock(m_OverheatMaterialPropertyBlock, data.MaterialIndex);
                }

                // Чем меньше ammo, тем сильнее дымит.
                m_SteamVfxEmissionModule.rateOverTimeMultiplier = SteamVfxEmissionRateMax * (1f - currentAmmoRatio);
            }

            // cooling sound
            // Звук охлаждения: играет когда оружие активно, не полный заряд, и идёт регенерация.
            if (CoolingCellsSound)
            {
                if (!m_AudioSource.isPlaying
                    && currentAmmoRatio != 1
                    && m_Weapon.IsWeaponActive
                    && m_Weapon.IsCooling)
                {
                    m_AudioSource.Play();
                }
                else if (m_AudioSource.isPlaying
                         && (currentAmmoRatio == 1 || !m_Weapon.IsWeaponActive || !m_Weapon.IsCooling))
                {
                    m_AudioSource.Stop();
                    return;
                }

                // Громкость по кривой — мягче чем линейно.
                m_AudioSource.volume = AmmoToVolumeRatioCurve.Evaluate(1 - currentAmmoRatio);
            }

            m_LastAmmoRatio = currentAmmoRatio;
        }
    }
}
