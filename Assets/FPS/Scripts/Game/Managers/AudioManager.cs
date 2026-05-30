using UnityEngine;
using UnityEngine.Audio;

namespace Unity.FPS.Game
{
    // ============================================================================
    // AudioManager — обёртка над массивом AudioMixer'ов.
    //
    // Зачем: в проекте может быть несколько микшеров (например, разделение
    // музыки и SFX). Чтобы код не знал какой именно содержит нужную группу,
    // AudioManager пробует один за другим и возвращает первый успех.
    //
    // Сам класс простой: при Awake передаёт себя в AudioUtility, который уже
    // делает основную работу.
    // ============================================================================
    public class AudioManager : MonoBehaviour
    {
        // Массив микшеров. Назначается в Inspector.
        public AudioMixer[] AudioMixers;

        // При старте регистрируемся в утилите — она кеширует ссылку.
        void Awake() => AudioUtility.Initialize(this);

        // Поиск групп — перебираем все микшеры, возвращаем первый непустой результат.
        public AudioMixerGroup[] FindMatchingGroups(string subPath)
        {
            for (int i = 0; i < AudioMixers.Length; i++)
            {
                AudioMixerGroup[] results = AudioMixers[i].FindMatchingGroups(subPath);
                if (results != null && results.Length != 0)
                {
                    return results;
                }
            }

            return null;
        }

        // Установка экспонированного параметра во ВСЕХ микшерах сразу.
        // Это упрощение для случая, когда «MasterVolume» есть у каждого микшера.
        public void SetFloat(string name, float value)
        {
            for (int i = 0; i < AudioMixers.Length; i++)
            {
                if (AudioMixers[i] != null)
                {
                    AudioMixers[i].SetFloat(name, value);
                }
            }
        }

        // Чтение — возвращаем значение из ПЕРВОГО ненулевого микшера.
        // out — обязательный паттерн для GetFloat в AudioMixer (так задумано в API).
        public void GetFloat(string name, out float value)
        {
            value = 0f;
            for (int i = 0; i < AudioMixers.Length; i++)
            {
                if (AudioMixers[i] != null)
                {
                    AudioMixers[i].GetFloat(name, out value);
                    break;
                }
            }
        }
    }
}
