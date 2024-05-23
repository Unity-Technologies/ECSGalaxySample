using UnityEngine.UIElements;

namespace Galaxy
{
    public class HUDScreen : UIScreen
    {
        private VisualElement m_SettingsButton;

        public HUDScreen(VisualElement parentElement) : base(parentElement)
        {
            SetVisualElements();
            RegisterCallbacks();
        }

        private void SetVisualElements()
        {
            m_SettingsButton = m_RootElement.Q<VisualElement>("settings-button");
        }

        private void RegisterCallbacks()
        {
            m_EventRegistry.RegisterCallback<ClickEvent>(m_SettingsButton, _ => UIEvents.HomeScreenShown.Invoke());
        }
    }
}