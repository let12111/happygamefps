using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Game
{
    // ============================================================================
    // ActorsManager — центральный реестр всех Actor'ов в сцене (игрок + враги).
    //
    // Зачем: вместо того чтобы каждый раз делать FindObjectsOfType<Actor>()
    // (это дорогая операция, ищет по всей сцене), любая система берёт список
    // отсюда. Actors сами регистрируются в Start через свой Actor.Start().
    //
    // Также хранит явную ссылку на игрока — частный случай, нужен очень часто
    // (AI наводит огонь, UI показывает HUD).
    //
    // Реализует IActorsManager — это нужно для DI/моков в тестах.
    // ============================================================================
    public class ActorsManager : MonoBehaviour, IActorsManager
    {
        // List, а не HashSet, потому что чаще нужен последовательный обход,
        // а добавления/удаления происходят редко (только при спавне/смерти).
        public List<Actor> Actors { get; private set; }
        public GameObject Player { get; private set; }

        // Сеттер игрока — нужен потому что свойство в интерфейсе только для чтения,
        // но PlayerInputHandler должен иметь возможность зарегистрироваться.
        public void SetPlayer(GameObject player) => Player = player;

        void Awake()
        {
            // Awake вызывается ДО Start у других объектов. Поэтому, когда Actor.Start
            // попытается добавить себя в список — список уже готов.
            Actors = new List<Actor>();
        }
    }
}
