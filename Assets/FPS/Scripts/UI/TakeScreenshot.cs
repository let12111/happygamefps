using System.IO;
using Unity.FPS.Game;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    // ============================================================================
    // TakeScreenshot — функция «сделать скриншот» из меню паузы.
    // Работает ТОЛЬКО в редакторе Unity (в билде кнопка отключается).
    //
    // Алгоритм: на кнопке выставляется флаг → следующий кадр скрываем UI меню,
    // вызываем ScreenCapture.CaptureScreenshot → следующий кадр загружаем
    // получившийся PNG обратно в UI как предпросмотр.
    //
    // Между «скрыть UI» и «снять» должен пройти кадр — иначе на скриншот попадёт сам UI.
    // ============================================================================
    public class TakeScreenshot : MonoBehaviour
    {
        [Tooltip("Root of the screenshot panel in the menu")]
        public GameObject ScreenshotPanel;

        [Tooltip("Name for the screenshot file")]
        public string FileName = "Screenshot";

        [Tooltip("Image to display the screenshot in")]
        public RawImage PreviewImage;

        CanvasGroup m_MenuCanvas = null;
        Texture2D m_Texture;

        bool m_TakeScreenshot;
        bool m_ScreenshotTaken;
        bool m_IsFeatureDisable;

        string GetPath() => k_ScreenshotPath + FileName + ".png";

        const string k_ScreenshotPath = "Assets/";

        void Awake()
        {
#if !UNITY_EDITOR
        // this feature is available only in the editor
        // В билде сразу прячем панель и блокируем логику.
        ScreenshotPanel.SetActive(false);
        m_IsFeatureDisable = true;
#else
            m_IsFeatureDisable = false;

            var gameMenuManager = GetComponent<InGameMenuManager>();
            DebugUtility.HandleErrorIfNullGetComponent<InGameMenuManager, TakeScreenshot>(gameMenuManager, this,
                gameObject);

            m_MenuCanvas = gameMenuManager.MenuRoot.GetComponent<CanvasGroup>();
            DebugUtility.HandleErrorIfNullGetComponent<CanvasGroup, TakeScreenshot>(m_MenuCanvas, this,
                gameMenuManager.MenuRoot.gameObject);

            LoadScreenshot();
#endif
        }

        void Update()
        {
            // RawImage без текстуры — некрасиво. Прячем пока её нет.
            PreviewImage.enabled = PreviewImage.texture != null;

            if (m_IsFeatureDisable)
                return;

            if (m_TakeScreenshot)
            {
                // Прячем UI и снимаем экран.
                m_MenuCanvas.alpha = 0;
                ScreenCapture.CaptureScreenshot(GetPath());
                m_TakeScreenshot = false;
                m_ScreenshotTaken = true;
                return;
            }

            // На следующий кадр — загружаем скриншот в UI и возвращаем меню.
            if (m_ScreenshotTaken)
            {
                LoadScreenshot();
#if UNITY_EDITOR
                // Чтобы файл появился в Project view.
                AssetDatabase.Refresh();
#endif

                m_MenuCanvas.alpha = 1;
                m_ScreenshotTaken = false;
            }
        }

        // Привязывается из Button.OnClick в Inspector.
        public void OnTakeScreenshotButtonPressed()
        {
            m_TakeScreenshot = true;
        }

        void LoadScreenshot()
        {
            if (File.Exists(GetPath()))
            {
                var bytes = File.ReadAllBytes(GetPath());

                // Размер 2x2 — заглушка, LoadImage переопределит реальным.
                m_Texture = new Texture2D(2, 2);
                m_Texture.LoadImage(bytes);
                m_Texture.Apply();
                PreviewImage.texture = m_Texture;
            }
        }
    }
}
