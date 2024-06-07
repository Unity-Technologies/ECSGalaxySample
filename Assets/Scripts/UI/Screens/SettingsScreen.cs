using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy
{
    public class SettingsScreen : UIScreen
    {
        private TabMenu m_SettingTabMenu;
        private Button m_BackButton;

        // Simulation Settings
        private TextField m_TimeScaleField;
        private TextField m_TeamCountField;
        private Toggle m_UseNonDeterministicRandomSeedToggle;
        private Toggle m_UseFixedSimulationRateToggle;
        private TextField m_FixedRateField;
        private TextField m_MaxTotalShipsField;
        private TextField m_MaxTeamShipsField;
        private TextField m_HomePlanetSpawnRadiusField;
        private TextField m_NeutralPlanetCountField;
        private TextField m_NumMoonsHomePlanetField;
        private Vector2Field m_NumMoonsRangeField;
        private Toggle m_BuildSpatialDatabaseParallelToggle;
        private Vector3Field m_HomePlanerResourceGenerationRateField;
        private Vector3Field m_ResourceGenerationProbabilityField;
        private Vector3Field m_ResourceGenerationRateMinField;
        private Vector3Field m_ResourceGenerationRateMaxField;
        private Vector3Field m_PlanetResourceMaxStorageField;

        // Ship Settings
        private VisualElement m_ShipSettingsContainer;
        private VisualTreeAsset m_ShipSettingsTemplate;
        private VisualTreeAsset m_FighterSettingsTemplate;
        private VisualTreeAsset m_WorkerSettingsTemplate;
        private VisualTreeAsset m_TraderSettingsTemplate;

        private List<ShipSettingData> m_ShipSettingDataList = new();
        private List<FighterSettingsData> m_FigtherSettingDataList = new();
        private List<WorkerSettingsData> m_WorkerSettingDataList = new();
        private List<TraderSettingsData> m_TraderSettingDataList = new();

        private const string k_HideInSimulationClass = "hide-in-simulation";

        public SettingsScreen(VisualElement parentElement) : base(parentElement)
        {
            SetVisualElements();
            SubscribeToEvents();
            RegisterCallbacks();

            m_IsTransparent = true;
        }

        private void SubscribeToEvents()
        {
            UIEvents.InitializeUISettings += InitializeUISettings;
            UIEvents.OnRequestSimulationStart += OnSimulationStarted;
        }

        private void SetVisualElements()
        {
            m_BackButton = m_RootElement.Q<Button>("settings-back-button");

            // Simulation Settings
            m_TimeScaleField = m_RootElement.Q<TextField>("time-scale-field");
            m_TeamCountField = m_RootElement.Q<TextField>("team-count-field");
            m_UseNonDeterministicRandomSeedToggle = m_RootElement.Q<Toggle>("use-non-deterministic-random-seed-toggle");
            m_UseFixedSimulationRateToggle = m_RootElement.Q<Toggle>("use-fixed-simulation-deltatime-toggle");
            m_FixedRateField = m_RootElement.Q<TextField>("fixed-deltatime-field");
            m_MaxTotalShipsField = m_RootElement.Q<TextField>("max-total-ships-field");
            m_MaxTeamShipsField = m_RootElement.Q<TextField>("max-team-ships-field");
            m_HomePlanetSpawnRadiusField = m_RootElement.Q<TextField>("home-planet-spawn-radius-field");
            m_NeutralPlanetCountField = m_RootElement.Q<TextField>("neutral-planet-count-field");
            m_NumMoonsHomePlanetField = m_RootElement.Q<TextField>("num-moons-home-planet-field");
            m_NumMoonsRangeField = m_RootElement.Q<Vector2Field>("num-moons-range-field");
            m_BuildSpatialDatabaseParallelToggle = m_RootElement.Q<Toggle>("build-spatial-database-parallel-toggle");
            m_HomePlanerResourceGenerationRateField =
                m_RootElement.Q<Vector3Field>("home-planet-resource-generation-rate-field");
            m_ResourceGenerationProbabilityField =
                m_RootElement.Q<Vector3Field>("resource-generation-probability-field");
            m_ResourceGenerationRateMinField = m_RootElement.Q<Vector3Field>("resource-generation-rate-min-field");
            m_ResourceGenerationRateMaxField = m_RootElement.Q<Vector3Field>("resource-generation-rate-max-field");
            m_PlanetResourceMaxStorageField = m_RootElement.Q<Vector3Field>("planet-resource-max-storage-field");

            // Ship Settings
            m_ShipSettingsContainer = m_RootElement.Q<VisualElement>("ship-settings-container");

            // Tab Menu
            m_SettingTabMenu = new TabMenu(m_RootElement);

            // Get Ship Settings Template
            m_ShipSettingsTemplate = Resources.Load<VisualTreeAsset>("UI/ShipSettingsTemplate");
            m_FighterSettingsTemplate = Resources.Load<VisualTreeAsset>("UI/FighterSettingsTemplate");
            m_WorkerSettingsTemplate = Resources.Load<VisualTreeAsset>("UI/WorkerSettingsTemplate");
            m_TraderSettingsTemplate = Resources.Load<VisualTreeAsset>("UI/TraderSettingsTemplate");
        }

        private void RegisterCallbacks()
        {
            m_SettingTabMenu.RegisterTabCallbacks();
            m_EventRegistry.RegisterCallback<ClickEvent>(m_BackButton, CloseWindow);
            
            m_RootElement.RegisterCallback<ChangeEvent<string>>(_ => ApplyGameSettings());
            m_RootElement.RegisterCallback<ChangeEvent<Vector2>>(_ => ApplyGameSettings());
            m_RootElement.RegisterCallback<ChangeEvent<Vector3>>(_ => ApplyGameSettings());
            m_RootElement.RegisterCallback<ChangeEvent<bool>>(_ => ApplyGameSettings());
        }

        private void InitializeUISettings()
        {
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            entityManager.CompleteAllTrackedJobs();
            if (GameUtilities.TryGetSingleton(entityManager, out SimulationRate simulationRate) &&
                GameUtilities.TryGetSingletonEntity<Config>(entityManager, out Entity configEntity) &&
                GameUtilities.TryGetSingleton(entityManager, out Config config))
            {
                DynamicBuffer<TeamConfig> teamBuffer = entityManager.GetBuffer<TeamConfig>(configEntity);
                
                // Simulation Settings
                m_TimeScaleField.SetValueWithoutNotify(simulationRate.TimeScale.ToString());
                m_TeamCountField.SetValueWithoutNotify(teamBuffer.Length.ToString());
                m_UseNonDeterministicRandomSeedToggle.SetValueWithoutNotify(config.UseNonDeterministicRandomSeed);
                m_UseFixedSimulationRateToggle.SetValueWithoutNotify(simulationRate.UseFixedRate);
                m_FixedRateField.SetValueWithoutNotify(simulationRate.FixedTimeStep.ToString());
                m_MaxTotalShipsField.SetValueWithoutNotify(config.MaxTotalShips.ToString());
                m_MaxTeamShipsField.SetValueWithoutNotify(config.MaxShipsPerTeam.ToString());
                m_HomePlanetSpawnRadiusField.SetValueWithoutNotify(config.HomePlanetSpawnRadius.ToString());
                m_NeutralPlanetCountField.SetValueWithoutNotify(config.NeutralPlanetsCount.ToString());
                m_NumMoonsHomePlanetField.SetValueWithoutNotify(config.NumMoonsHomePlanet.ToString());
                m_NumMoonsRangeField.SetValueWithoutNotify(new Vector2(config.NumMoonsRange.x, config.NumMoonsRange.y));
                m_BuildSpatialDatabaseParallelToggle.SetValueWithoutNotify(config.BuildSpatialDatabaseParallel);
                m_HomePlanerResourceGenerationRateField.SetValueWithoutNotify(config.HomePlanetResourceGenerationRate);
                m_ResourceGenerationProbabilityField.SetValueWithoutNotify(config.ResourceGenerationProbabilities);
                m_ResourceGenerationRateMinField.SetValueWithoutNotify(config.ResourceGenerationRateMin);
                m_ResourceGenerationRateMaxField.SetValueWithoutNotify(config.ResourceGenerationRateMax);
                m_PlanetResourceMaxStorageField.SetValueWithoutNotify(config.PlanetResourceMaxStorage);

                // Ship settings
                NativeArray<ShipCollection> shipCollectionBuffer =
                    entityManager.GetBuffer<ShipCollection>(configEntity).ToNativeArray(Allocator.Temp);
                for (int i = 0; i < shipCollectionBuffer.Length; i++)
                {
                    ShipCollection shipCollectionElement = shipCollectionBuffer[i];
                    Entity shipPrefabEntity = shipCollectionElement.PrefabEntity;
                    string shipName = entityManager.GetComponentData<EntityName>(shipCollectionElement.PrefabEntity).NameData.Value.Name
                        .ToString();
                    
                    VisualElement shipSettings = m_ShipSettingsTemplate.CloneTree();
                    ShipSettingData shipSettingData = new ShipSettingData(
                        shipSettings,
                        shipCollectionElement, 
                        i,
                        shipName);
                    
                    m_ShipSettingsContainer.Add(shipSettings);
                    m_ShipSettingDataList.Add(shipSettingData);

                    if (entityManager.HasComponent<Fighter>(shipPrefabEntity))
                    {
                        VisualElement typeData = m_FighterSettingsTemplate.CloneTree();
                        FighterSettingsData fighterSettingsData =
                            new FighterSettingsData(typeData,
                                entityManager.GetComponentData<Fighter>(shipPrefabEntity)
                                    .FighterData.Value, i);
                            
                        m_ShipSettingsContainer.Add(typeData);
                        m_FigtherSettingDataList.Add(fighterSettingsData);
                    }
                    else if (entityManager.HasComponent<Worker>(shipPrefabEntity))
                    {
                        VisualElement typeData = m_WorkerSettingsTemplate.CloneTree();
                        WorkerSettingsData workerSettingsData =
                            new WorkerSettingsData(typeData,
                                entityManager.GetComponentData<Worker>(shipPrefabEntity)
                                    .WorkerData.Value, i);
                            
                        m_ShipSettingsContainer.Add(typeData);
                        m_WorkerSettingDataList.Add(workerSettingsData);
                    }
                    else if (entityManager.HasComponent<Trader>(shipPrefabEntity))
                    {
                        VisualElement typeData = m_TraderSettingsTemplate.CloneTree();
                        TraderSettingsData traderSettingsData =
                            new TraderSettingsData(typeData,
                                entityManager.GetComponentData<Trader>(shipPrefabEntity)
                                    .TraderData.Value, i);
                            
                        m_ShipSettingsContainer.Add(typeData);
                        m_TraderSettingDataList.Add(traderSettingsData);
                    }
                }

                shipCollectionBuffer.Dispose();
            }
        }
        
        private void OnSimulationStarted()
        {
            var settingsToHide = m_RootElement.Query(className: k_HideInSimulationClass).ToList();
            foreach (var setting in settingsToHide)
            {
                setting.SetEnabled(false);
            }
        }

        private void ApplyGameSettings()
        {
            m_TeamCountField.value = math.min(255, int.Parse(m_TeamCountField.value)).ToString();

            WorldUnmanaged worldUnmanaged = World.DefaultGameObjectInjectionWorld.Unmanaged;
            EntityManager entityManager = worldUnmanaged.EntityManager;
            entityManager.CompleteAllTrackedJobs();
            if (GameUtilities.TryGetSingletonEntity<Config>(entityManager, out Entity configEntity) &&
                GameUtilities.TryGetSingletonRW(entityManager, out RefRW<Config> config))
            {
                // Config
                config.ValueRW.UseNonDeterministicRandomSeed = m_UseNonDeterministicRandomSeedToggle.value;
                config.ValueRW.MaxTotalShips = int.Parse(m_MaxTotalShipsField.value);
                config.ValueRW.MaxShipsPerTeam = int.Parse(m_MaxTeamShipsField.value);
                config.ValueRW.HomePlanetSpawnRadius = float.Parse(m_HomePlanetSpawnRadiusField.value);
                config.ValueRW.NeutralPlanetsCount = int.Parse(m_NeutralPlanetCountField.value);
                config.ValueRW.NumMoonsHomePlanet = int.Parse(m_NumMoonsHomePlanetField.value);
                config.ValueRW.NumMoonsRange = new int2((int)m_NumMoonsRangeField.value.x, (int)m_NumMoonsRangeField.value.y);
                config.ValueRW.BuildSpatialDatabaseParallel =m_BuildSpatialDatabaseParallelToggle.value;
                config.ValueRW.HomePlanetResourceGenerationRate = m_HomePlanerResourceGenerationRateField.value;
                config.ValueRW.ResourceGenerationProbabilities = m_ResourceGenerationProbabilityField.value;
                config.ValueRW.ResourceGenerationRateMin = m_ResourceGenerationRateMinField.value;
                config.ValueRW.ResourceGenerationRateMax = m_ResourceGenerationRateMaxField.value;
                config.ValueRW.PlanetResourceMaxStorage = m_PlanetResourceMaxStorageField.value;

                // Simulation rate
                GameUtilities.SetUseFixedRate(worldUnmanaged, m_UseFixedSimulationRateToggle.value);
                GameUtilities.SetFixedTimeStep(worldUnmanaged, float.Parse(m_FixedRateField.value));
                GameUtilities.SetTimeScale(worldUnmanaged, float.Parse(m_TimeScaleField.value));

                // Teams
                {
                    DynamicBuffer<TeamConfig> teamBuffer = entityManager.GetBuffer<TeamConfig>(configEntity);
                    teamBuffer.Clear();

                    Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex(29);
                    for (int i = 0; i < int.Parse(m_TeamCountField.value); i++)
                    {
                        float randomHue = random.NextFloat(0f, 1f);
                        float4 randomColor = (Vector4)Color.HSVToRGB(randomHue, 1f, 1f);
                        teamBuffer.Add(new TeamConfig
                        {
                            Name = new FixedString128Bytes($"Team {i}"),
                            Color = randomColor,
                        });
                    }
                }
            }
        }

        private void CloseWindow()
        {
            UIEvents.ScreenClosed?.Invoke();
        }
    }
}