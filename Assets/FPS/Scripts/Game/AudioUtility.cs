using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Unity.FPS.Game
{
    // ============================================================================
    // AudioUtility — статическая обёртка над AudioMixer и AudioSourcePool.
    // Зачем: вместо того чтобы каждый скрипт сам создавал AudioSource, искал
    // микшер и вызывал PlayOneShot, всё это делается одним вызовом CreateSFX.
    //
    // Плюсы такого подхода:
    //  - источники переиспользуются через пул (не создаём GameObject на каждый звук);
    //  - все звуки автоматически идут через нужную группу микшера (громкость регулируется централизованно);
    //  - кеш групп микшера — FindMatchingGroups делается ОДИН раз на группу.
    // ============================================================================
    public class AudioUtility
    {
        // Ссылка на сам микшер. Заполняется через Initialize.
        // Static — чтобы любому скрипту был доступен без поиска.
        static AudioManager s_AudioManager;
        // Кеш «enum-имя группы → AudioMixerGroup». Без него каждый звук вызывал бы
        // FindMatchingGroups, а это поиск строк в дереве микшера — недёшево.
        // readonly — ссылка на словарь не меняется (но содержимое — да).
        static readonly Dictionary<AudioGroups, AudioMixerGroup> s_AudioGroupCache =
            new Dictionary<AudioGroups, AudioMixerGroup>();

        // Перечисление — типобезопасные имена групп микшера. Лучше чем строки:
        // компилятор поймает опечатку, IDE покажет автодополнение.
        // Имена ДОЛЖНЫ совпадать с именами групп в AudioMixer (см. ToString() ниже).
        public enum AudioGroups
        {
            DamageTick,
            Impact,
            EnemyDetection,
            Pickup,
            WeaponShoot,
            WeaponOverheat,
            WeaponChargeBuildup,
            WeaponChargeLoop,
            HUDVictory,
            HUDObjective,
            EnemyAttack
        }

        // Вызывается из AudioManager при старте. Передаём ему ссылку и
        // сбрасываем кеш (актуально при перезагрузке сцены).
        public static void Initialize(AudioManager audioManager)
        {
            s_AudioManager = audioManager;
            s_AudioGroupCache.Clear();
        }

        // Главный метод: «проиграй звук clip в точке position».
        // spatialBlend = 0 — 2D (звук без позиции, играет одинаково везде),
        // spatialBlend = 1 — полностью 3D (громкость зависит от расстояния).
        // rolloffDistanceMin — на каком расстоянии звук начинает затухать.
        public static void CreateSFX(AudioClip clip, Vector3 position, AudioGroups audioGroup, float spatialBlend,
            float rolloffDistanceMin = 1f)
        {
            // Берём свободный AudioSource из пула в нужной точке.
            var source = AudioSourcePool.Instance.Get(position);
            source.clip = clip;
            source.spatialBlend = spatialBlend;
            source.minDistance = rolloffDistanceMin;
            // Пускаем звук через нужную группу — громкость, фильтры, реверб.
            source.outputAudioMixerGroup = GetAudioGroup(audioGroup);
            source.Play();
            // Возвращаем источник в пул сразу после окончания клипа.
            // Без этого пул бы быстро исчерпался.
            AudioSourcePool.Instance.ReturnAfterDelay(source, clip.length);
        }

        // Достаём AudioMixerGroup по enum-имени. Кешируем, чтобы FindMatchingGroups
        // вызвалось максимум один раз на каждую группу.
        public static AudioMixerGroup GetAudioGroup(AudioGroups group)
        {
            // Быстрый путь — уже искали раньше.
            if (s_AudioGroupCache.TryGetValue(group, out AudioMixerGroup cached))
                return cached;

            // group.ToString() возвращает имя enum-значения как строку,
            // которое должно совпадать с именем группы в микшере.
            var groups = s_AudioManager.FindMatchingGroups(group.ToString());
            AudioMixerGroup result = groups.Length > 0 ? groups[0] : null;

            // Если в микшере нет такой группы — предупредим, но не упадём.
            if (result == null)
                Debug.LogWarning("Didn't find audio group for " + group.ToString());

            // Кешируем даже null — чтобы не искать впустую снова и снова.
            s_AudioGroupCache[group] = result;
            return result;
        }

        // Установка громкости мастер-канала.
        // Громкость в микшере хранится в децибелах: 0 dB = 100%, -80 dB = тишина.
        // Преобразуем линейное значение 0..1 в dB: 20 * log10(value).
        public static void SetMasterVolume(float value)
        {
            // log10(0) = минус бесконечность, поэтому ноль заменяем на «почти ноль».
            if (value <= 0)
                value = 0.001f;
            float valueInDb = Mathf.Log10(value) * 20;
            // "MasterVolume" — имя экспонированного параметра в AudioMixer asset.
            s_AudioManager.SetFloat("MasterVolume", valueInDb);
        }

        // Обратное преобразование: dB → линейное 0..1.
        // Формула: 10^(dB / 20).
        public static float GetMasterVolume()
        {
            s_AudioManager.GetFloat("MasterVolume", out var valueInDb);
            return Mathf.Pow(10f, valueInDb / 20.0f);
        }
    }
}
