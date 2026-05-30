using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.FPS.Game
{
    // ============================================================================
    // GameFlowManager — управляет финалом игры: победа/поражение, fade-to-black,
    // загрузка финальной сцены, звук победы.
    //
    // Слушает два события через EventManager:
    //   AllObjectivesCompletedEvent → победа.
    //   PlayerDeathEvent → поражение.
    //
    // Логика затемнения: при EndGame ставим время загрузки в будущем,
    // в Update каждый кадр считаем «дошли ли», плавно поднимаем alpha
    // экрана-затемнения и убавляем громкость.
    // ============================================================================
    public class GameFlowManager : MonoBehaviour, IGameFlowManager
    {
        [Header("Parameters")] [Tooltip("Duration of the fade-to-black at the end of the game")]
        public float EndSceneLoadDelay = 3f;

        // CanvasGroup — UI-компонент с одной альфой на всю группу. Удобно для
        // полноэкранного затемнения: меняем один параметр, темнеют все элементы.
        [Tooltip("The canvas group of the fade-to-black screen")]
        public CanvasGroup EndGameFadeCanvasGroup;

        [Header("Win")] [Tooltip("This string has to be the name of the scene you want to load when winning")]
        public string WinSceneName = "WinScene";

        [Tooltip("Duration of delay before the fade-to-black, if winning")]
        public float DelayBeforeFadeToBlack = 4f;

        [Tooltip("Win game message")]
        public string WinGameMessage;
        [Tooltip("Duration of delay before the win message")]
        public float DelayBeforeWinMessage = 2f;

        [Tooltip("Sound played on win")] public AudioClip VictorySound;

        [Header("Lose")] [Tooltip("This string has to be the name of the scene you want to load when losing")]
        public string LoseSceneName = "LoseScene";


        // Внешним коду полезно знать «игра уже завершилась?» (например,
        // PlayerInputHandler не реагирует на ввод, когда GameIsEnding).
        public bool GameIsEnding { get; private set; }

        // Абсолютное время, когда надо вызвать LoadScene.
        float m_TimeLoadEndGameScene;
        // Имя сцены для загрузки (Win или Lose).
        string m_SceneToLoad;

        void Awake()
        {
            // Подписываемся на события. RemoveListener'ы в OnDestroy.
            EventManager.AddListener<AllObjectivesCompletedEvent>(OnAllObjectivesCompleted);
            EventManager.AddListener<PlayerDeathEvent>(OnPlayerDeath);
        }

        void Start()
        {
            // Начинаем игру с полной громкости (на случай если в прошлой сессии
            // мы выходили на «тихом» fade).
            AudioUtility.SetMasterVolume(1);
        }

        void Update()
        {
            if (GameIsEnding)
            {
                // ratio: 0 в начале затемнения, 1 в конце.
                // (m_TimeLoadEndGameScene - Time.time) — сколько ещё осталось.
                // 1 - оставшееся/полное = пройденное.
                float timeRatio = 1 - (m_TimeLoadEndGameScene - Time.time) / EndSceneLoadDelay;
                EndGameFadeCanvasGroup.alpha = timeRatio;

                // Параллельно тушим звук.
                AudioUtility.SetMasterVolume(1 - timeRatio);

                // See if it's time to load the end scene (after the delay)
                if (Time.time >= m_TimeLoadEndGameScene)
                {
                    SceneManager.LoadScene(m_SceneToLoad);
                    // Сбрасываем флаг чтобы Update больше не делал работу
                    // (хотя сцена и так выгрузится).
                    GameIsEnding = false;
                }
            }
        }

        // Лямбды-обёртки, чтобы матчить сигнатуру Action<T>.
        void OnAllObjectivesCompleted(AllObjectivesCompletedEvent evt) => EndGame(true);
        void OnPlayerDeath(PlayerDeathEvent evt) => EndGame(false);

        void EndGame(bool win)
        {
            // unlocks the cursor before leaving the scene, to be able to click buttons
            // В геймплее курсор обычно залочен в центре. На экранах меню он нужен видимым.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Remember that we need to load the appropriate end scene after a delay
            GameIsEnding = true;
            EndGameFadeCanvasGroup.gameObject.SetActive(true);
            if (win)
            {
                m_SceneToLoad = WinSceneName;
                // У победы дополнительная задержка перед началом затемнения
                // (показать сообщение и сыграть фанфары).
                m_TimeLoadEndGameScene = Time.time + EndSceneLoadDelay + DelayBeforeFadeToBlack;

                // play a sound on win
                // Создаём AudioSource на лету. PlayScheduled с dspTime — точное
                // планирование в часах аудиоподсистемы (точнее, чем Time.time).
                var audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.clip = VictorySound;
                audioSource.playOnAwake = false;
                audioSource.outputAudioMixerGroup = AudioUtility.GetAudioGroup(AudioUtility.AudioGroups.HUDVictory);
                audioSource.PlayScheduled(AudioSettings.dspTime + DelayBeforeWinMessage);

                // Просим UI показать сообщение победы с задержкой.
                DisplayMessageEvent displayMessage = Events.DisplayMessageEvent;
                displayMessage.Message = WinGameMessage;
                displayMessage.DelayBeforeDisplay = DelayBeforeWinMessage;
                EventManager.Broadcast(displayMessage);
            }
            else
            {
                m_SceneToLoad = LoseSceneName;
                m_TimeLoadEndGameScene = Time.time + EndSceneLoadDelay;
            }
        }

        // Обязательно отписываемся — иначе при перезагрузке сцены статический
        // EventManager будет держать ссылку на этот (уже удалённый) объект.
        void OnDestroy()
        {
            EventManager.RemoveListener<AllObjectivesCompletedEvent>(OnAllObjectivesCompleted);
            EventManager.RemoveListener<PlayerDeathEvent>(OnPlayerDeath);
        }
    }
}
