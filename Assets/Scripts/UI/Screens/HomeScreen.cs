using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Galaxy
{
    public class HomeScreen : UIScreen
    {
        private VisualElement m_SimulateButton;
        private VisualElement m_SettingsButton;
        private VisualElement m_ExitButton;
        private VisualElement m_ReturnButton;
        private Label m_ExitButtonLabel;
        private bool m_SimulationStarted;

        public HomeScreen(VisualElement parentElement) : base(parentElement)
        {
            SubscribeToEvents();
            SetVisualElements();
            RegisterCallbacks();
            // Initialize the simulation state
            m_SimulationStarted = false;
        }

        private void SubscribeToEvents()
        {
            UIEvents.SimulateGame += SimulateGame;
        }

        private void SetVisualElements()
        {
            m_SimulateButton = m_RootElement.Q<VisualElement>("simulate-button");
            m_SettingsButton = m_RootElement.Q<VisualElement>("settings-button");
            m_ExitButton = m_RootElement.Q<VisualElement>("exit-button");
            m_ReturnButton = m_RootElement.Q<VisualElement>("return-button");
            m_ExitButtonLabel = m_ExitButton.Q<Label>("button-label");
        }

        private void RegisterCallbacks()
        {
            m_EventRegistry.RegisterCallback<ClickEvent>(m_SettingsButton, OnSettingsButtonClicked);
            m_EventRegistry.RegisterCallback<ClickEvent>(m_SimulateButton, OnSimulateButtonClicked);
            m_EventRegistry.RegisterCallback<ClickEvent>(m_ReturnButton, OnReturnButtonClicked);
            m_EventRegistry.RegisterCallback<ClickEvent>(m_ExitButton, OnExitButtonClicked);
        }

        private void OnExitButtonClicked()
        {
            if (m_SimulationStarted)
            {
                World world = World.DefaultGameObjectInjectionWorld;
                world.EntityManager.CompleteAllTrackedJobs();

                // Dispose and recreate the default world
                world.Dispose();
                {
                    world = new World("DefaultWorld", WorldFlags.Game);
                    if (World.DefaultGameObjectInjectionWorld == null)
                        World.DefaultGameObjectInjectionWorld = world;

                    IReadOnlyList<Type> systems =
                        DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
                    DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
                    ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
                }
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                m_SimulationStarted = false;
            }
            else
            {
                // TODO: Add a confirmation dialog
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        private void OnReturnButtonClicked()
        {
            UIEvents.TogglePause?.Invoke();
        }

        private void OnSimulateButtonClicked(ClickEvent obj)
        {
            SimulateGame();
        }

        private void SimulateGame()
        {
            UIEvents.OnRequestSimulationStart?.Invoke();
            UIEvents.HUDScreenShown?.Invoke();
            UIEvents.IgnoreCameraInput?.Invoke(false);
            m_SimulationStarted = true;
        }

        private void OnSettingsButtonClicked()
        {
            UIEvents.SettingsShown?.Invoke();
            m_SettingsButton.SetEnabled(false);
            m_SimulateButton.style.display = DisplayStyle.None;
            m_ExitButton.style.display = DisplayStyle.None;
            m_ReturnButton.style.display = DisplayStyle.None;
        }

        public override void Show()
        {
            base.Show();

            CursorUtils.ShowCursor();

            if (m_SimulationStarted)
            {
                m_ReturnButton.style.display = DisplayStyle.Flex;
                m_SimulateButton.style.display = DisplayStyle.None;
                m_ExitButtonLabel.text = "MAIN MENU";
            }
            else
            {
                m_ReturnButton.style.display = DisplayStyle.None;
                m_SimulateButton.style.display = DisplayStyle.Flex;
                m_ExitButtonLabel.text = "EXIT";
            }

            m_SettingsButton.SetEnabled(true);
            m_SettingsButton.style.display = DisplayStyle.Flex;
            m_ExitButton.style.display = DisplayStyle.Flex;
        }
    }
}