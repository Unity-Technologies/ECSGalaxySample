using UnityEngine;
using UnityEngine.UIElements;
using System;

namespace Galaxy
{
    /// <summary>
    /// Base class for managing UI Toolkit-based screens in conjunction with the
    /// UI Manager component. Derive classes to manage the main parts of the UI
    /// (i.e. SettingsScreen, MainMenuScreen, GameScreen, etc.)
    ///
    /// View includes to methods to:
    ///     - Initialize the button click events and document settings
    ///     - Hide and show the parent UI element
    /// </summary>
    public abstract class UIScreen
    {
        public const string k_VisibleClass = "screen-visible";
        public const string k_HiddenClass = "screen-hidden";

        #region Inspector fields
        protected bool m_HideOnAwake = true;

        // Is the UI partially see-through? (i.e. use overlay effect)
        protected bool m_IsTransparent;

        // The topmost visual element of the screen (this often is the rootVisualElement
        // but can be a different element if necessary)
        protected VisualElement m_RootElement;
        protected EventRegistry m_EventRegistry;
        #endregion

        #region Properties
        public VisualElement ParentElement => m_RootElement;

        public bool IsTransparent => m_IsTransparent;
        public bool IsHidden => m_RootElement.style.display == DisplayStyle.None;
        
        #endregion

        // Constructor
        public UIScreen(VisualElement parentElement)
        {
            // Required topmost VisualElement 
            m_RootElement = parentElement ?? throw new ArgumentNullException(nameof(parentElement));
            Initialize();
        }

        #region Methods
        // Registers a callback, TransitionEndEvent, on the m_ParentElement; creates a default EventRegistry
        public virtual void Initialize()
        {
            if (m_HideOnAwake)
            {
                Hide();
            }

            m_EventRegistry = new EventRegistry();
        }

        // Unregister events from an external objects
        public virtual void Disable()
        {
            m_EventRegistry.Dispose();
        }

        public virtual void Show()
        {
            m_RootElement.style.display = DisplayStyle.Flex;
        }

        public virtual void Hide()
        {
            m_RootElement.style.display = DisplayStyle.None;
        }
        
        #endregion
    }
}
