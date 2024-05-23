using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace Galaxy
{
    /// <summary>
    /// The UI Manager manages the UI screens (View base class) using GameEvents paired
    /// to each View screen. A stack maintains a history of previously shown screens, so
    /// the UI Manager can "go back" until it reaches the default UI screen, the home screen.
    ///
    /// To add a new UIScreen under the UIManager's management:
    ///    -Define a new UIScreen field
    ///    -Create a new instance of that screen in Initialize (e.g. new SplashScreen(root.Q<VisualElement>("splash__container"));
    ///    -Register the UIScreen in the RegisterScreens method
    ///    -Subscribe/unsubscribe from the appropriate UIEvent to show the screen
    ///
    /// Alternatively, use Reflection to add the UIScreen to the RegisterScreens method
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Tooltip("Required UI Document")] [SerializeField]
        UIDocument m_Document;

        // UI screens
        private UIScreen m_HomeScreen;
        private UIScreen m_SettingsScreen;
        private UIScreen m_StatsScreen;
        private UIScreen m_HUDScreen;

        // The currently active UIScreen
        private UIScreen m_CurrentScreen;

        // A stack of previously displayed UIScreens
        private Stack<UIScreen> m_History = new();

        // A list of all Views to show/hide
        private List<UIScreen> m_Screens = new();
        public UIScreen CurrentScreen => m_CurrentScreen;
        public UIDocument Document => m_Document;

        // Register event listeners to game events
        private void OnEnable()
        {
            SubscribeToEvents();
            Initialize();

            Show(m_HomeScreen, false);
        }

        // Unregister the listeners to prevent errors
        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void SubscribeToEvents()
        {
            // Pair GameEvents with methods to Show each screen
            UIEvents.SettingsShown += UIEvents_SettingsShown;
            UIEvents.ScreenClosed += UIEvents_ScreenClosed;
            UIEvents.HomeScreenShown += UIEvents_HomeScreenShown;
            UIEvents.HUDScreenShown += UIEvents_HUDScreenShown;
            UIEvents.DisableUI += DisableUI;
            UIEvents.TogglePause += UIEvents_TogglePause;
        }

        private void UnsubscribeFromEvents()
        {
            UIEvents.SettingsShown -= UIEvents_SettingsShown;
            UIEvents.ScreenClosed -= UIEvents_ScreenClosed;
            UIEvents.HomeScreenShown -= UIEvents_HomeScreenShown;
            UIEvents.HUDScreenShown -= UIEvents_HUDScreenShown;
            UIEvents.DisableUI -= DisableUI;
            UIEvents.TogglePause -= UIEvents_TogglePause;
        }

        private void DisableUI()
        {
            UIEvents.IgnoreCameraInput?.Invoke(false);
            gameObject.SetActive(false);
        }

        // Event-handling methods

        private void UIEvents_SettingsShown()
        {
            Show(m_SettingsScreen);
        }

        private void UIEvents_HomeScreenShown()
        {
            Show(m_HomeScreen);
        }

        private void UIEvents_HUDScreenShown()
        {
            Show(m_HUDScreen);
        }

        private void UIEvents_TogglePause()
        {
            if (m_HUDScreen.IsHidden)
            {
                Show(m_HUDScreen);
                UIEvents.IgnoreCameraInput?.Invoke(false);
                CursorUtils.HideCursor();
            }
            else
            {
                Show(m_HomeScreen);
                UIEvents.IgnoreCameraInput?.Invoke(true);
                CursorUtils.ShowCursor();
            }
        }

        // Remove the top UI screen from the stack and make that active (i.e., go back one screen)
        public void UIEvents_ScreenClosed()
        {
            if (m_History.Count != 0)
            {
                Show(m_History.Pop(), false);
            }
        }

        // Methods

        // Clears history and hides all Views except the Start Screen
        private void Initialize()
        {
            VisualElement root = m_Document.rootVisualElement;

            m_HomeScreen = new HomeScreen(root.Q<VisualElement>("menu__container"));
            m_SettingsScreen = new SettingsScreen(root.Q<VisualElement>("settings__container"));
            m_StatsScreen = new PlanetStatsScreen(root.Q<VisualElement>("stats__container"));
            m_HUDScreen = new HUDScreen(root.Q<VisualElement>("hud__container"));

            RegisterScreens();
            HideScreens();

            // TODO: Call event when finish loading
            Show(m_HomeScreen, false);
            // Disable camera input by default
            UIEvents.IgnoreCameraInput?.Invoke(true);
        }

        // Store each UIScreen into a master list so we can hide all of them easily.
        private void RegisterScreens()
        {
            m_Screens = new List<UIScreen>
            {
                m_HomeScreen,
                m_SettingsScreen,
                m_StatsScreen,
                m_HUDScreen
            };
        }

        // Clear history and hide all Views
        private void HideScreens()
        {
            m_History.Clear();

            foreach (var screen in m_Screens)
            {
                screen.Hide();
            }
        }

        // Finds the first registered UI View of the specified type T
        public T GetScreen<T>() where T : UIScreen
        {
            foreach (var screen in m_Screens)
            {
                if (screen is T typeOfScreen)
                {
                    return typeOfScreen;
                }
            }

            return null;
        }

        // Shows a View of a specific type T, with the option to add it
        // to the history stack
        public void Show<T>(bool keepInHistory = true) where T : UIScreen
        {
            foreach (var screen in m_Screens)
            {
                if (screen is T)
                {
                    Show(screen, keepInHistory);
                    break;
                }
            }
        }

        // Shows a UIScreen with the option to add it to the history stack
        public void Show(UIScreen screen, bool keepInHistory = true)
        {
            if (screen == null)
                return;

            if (m_CurrentScreen != null)
            {
                if (!screen.IsTransparent)
                    m_CurrentScreen.Hide();

                if (keepInHistory)
                {
                    m_History.Push(m_CurrentScreen);
                }
            }

            screen.Show();
            m_CurrentScreen = screen;
        }
    }
}