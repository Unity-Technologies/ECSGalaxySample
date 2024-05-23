using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public partial struct UISystem : ISystem
{
    private bool m_SettingInitialized;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<Config>();
    }

    public void OnUpdate(ref SystemState state)
    {
        Config config = SystemAPI.GetSingleton<Config>();

        // If the config is already initialized, hide all screens
        if (!ShouldUseUI(config))
        {
            state.Enabled = false;
            return;
        }

        // Handle initializing the UI settings
        if (!m_SettingInitialized)
        {
            UIEvents.InitializeUISettings?.Invoke();
            m_SettingInitialized = true;
        }
    }

    private bool ShouldUseUI(Config config)
    {
        // If the config is already initialized, hide all screens
        if (config is {AutoInitializeGame: true})
        {
            Debug.Log("Config is already initialized");
            UIEvents.DisableUI?.Invoke();
            return false;
        }

        return true;
    }
}