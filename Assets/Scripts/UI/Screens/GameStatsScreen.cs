using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

public class AdvancedTeamStats
{
    private VisualElement m_RootElement;
    private VisualElement m_TeamColor;
    private Label m_TeamNameLabel;
    private Label m_ShipCountLabel;
    private Label m_PlanetCountLabel;
    private Label m_MoonCountLabel;
    private Label m_BuildingCountLabel;
    private Label m_GenerationRateLabel;
    private Label m_StorageTotalLabel;
    private Label m_StorageMinLabel;
    private Label m_StorageMaxLabel;

    public AdvancedTeamStats(VisualElement parentElement, string teamName, Color teamColor)
    {
        m_RootElement = parentElement;
        SetVisualElements();

        // Set Initial Data
        m_TeamColor.style.backgroundColor = teamColor;
        m_TeamNameLabel.text = teamName;
    }

    private void SetVisualElements()
    {
        m_TeamColor = m_RootElement.Q<VisualElement>("team-color");
        m_TeamNameLabel = m_RootElement.Q<Label>("stats-team-name-label");
        m_ShipCountLabel = m_RootElement.Q<Label>("ship-count-label");
        m_PlanetCountLabel = m_RootElement.Q<Label>("planet-count-label");
        m_MoonCountLabel = m_RootElement.Q<Label>("moon-count-label");
        m_BuildingCountLabel = m_RootElement.Q<Label>("building-count-label");
        m_GenerationRateLabel = m_RootElement.Q<Label>("generation-rate-label");
        m_StorageTotalLabel = m_RootElement.Q<Label>("storage-total-label");
        m_StorageMinLabel = m_RootElement.Q<Label>("storage-min-label");
        m_StorageMaxLabel = m_RootElement.Q<Label>("storage-max-label");
    }

    public void SetTeamData(TeamData teamData)
    {
        m_ShipCountLabel.text = teamData.ShipsCount.ToString();
        m_PlanetCountLabel.text = teamData.PlanetsCount.ToString();
        m_MoonCountLabel.text = teamData.MoonsCount.ToString();
        m_BuildingCountLabel.text = teamData.BuildingsCount.ToString();
        m_GenerationRateLabel.text =
            $"{teamData.GenerationRate.x}/{teamData.GenerationRate.y}/{teamData.GenerationRate.z}";
        m_StorageTotalLabel.text = $"{teamData.StorageTotal.x}/{teamData.StorageTotal.y}/{teamData.StorageTotal.z}";
        m_StorageMinLabel.text = $"{teamData.StorageMin.x}/{teamData.StorageMin.y}/{teamData.StorageMin.z}";
        m_StorageMaxLabel.text = $"{teamData.StorageMax.x}/{teamData.StorageMax.y}/{teamData.StorageMax.z}";
    }
}

public struct TeamData
{
    public int ShipsCount;
    public int PlanetsCount;
    public int MoonsCount;
    public int BuildingsCount;
    public int3 GenerationRate;
    public int3 StorageTotal;
    public int3 StorageMin;
    public int3 StorageMax;
}

public class GameStatsScreen : MonoBehaviour
{
    private Label m_FPSAvgLabel;
    private Label m_FPSWorstLabel;
    private Label m_ShipCountLabel;
    private VisualElement m_StatsContainer;
    private VisualElement m_CollapseButton;
    private VisualElement m_RootElement;
    private Foldout m_AdvancedStatsFoldout;

    private float m_FPSPollDuration = 1f;
    private float m_AccumulatedFPSDeltaTimes;
    private int m_AccumulatedFPSFrames;
    private float m_MaxFPSDeltaTime;
    private float m_DeltaAvg;
    private float m_DeltaWorst;

    private const string k_CollapsedStyle = "collapsed";

    private VisualTreeAsset m_AdvancedTeamStatsTemplate;
    private Dictionary<int, AdvancedTeamStats> m_AdvancedTeamStatsDictionary = new();
    private ScrollView m_AdvancedStatsScrollView;
    
    private bool m_Collapsed;

    private void Start()
    {
        m_RootElement = GetComponent<UIDocument>().rootVisualElement;
        SetVisualElements();
        RegisterCallbacks();
    }

    private void SetVisualElements()
    {
        m_FPSAvgLabel = m_RootElement.Q<Label>("fps-avg-label");
        m_FPSWorstLabel = m_RootElement.Q<Label>("fps-worst-label");
        m_ShipCountLabel = m_RootElement.Q<Label>("ship-count-label");
        m_StatsContainer = m_RootElement.Q<VisualElement>("game-stats__container");
        m_StatsContainer = m_RootElement.Q<VisualElement>("game-stats__container");
        m_CollapseButton = m_RootElement.Q<VisualElement>("collapse-button");
        m_AdvancedStatsFoldout = m_RootElement.Q<Foldout>("advanced-stats-foldout");
        m_AdvancedStatsScrollView = m_RootElement.Q<ScrollView>("advanced-stats-team-scroll-view");
        m_AdvancedStatsFoldout.value = false;

        m_AdvancedTeamStatsTemplate = Resources.Load<VisualTreeAsset>("UI/AdvancedStatsTeamTemplate");
    }

    private void RegisterCallbacks()
    {
        m_CollapseButton.RegisterCallback<ClickEvent>(OnCollapseClicked);
    }

    private void OnCollapseClicked(ClickEvent evt)
    {
        m_StatsContainer.ToggleInClassList(k_CollapsedStyle);
        m_AdvancedStatsFoldout.value = false;
        m_Collapsed = !m_Collapsed;
    }

    private void Update()
    {
        // Don't update if collapsed
        if (m_Collapsed)
            return;
        
        // Framerate
        m_MaxFPSDeltaTime = math.max(m_MaxFPSDeltaTime, Time.deltaTime);
        m_AccumulatedFPSDeltaTimes += Time.deltaTime;
        m_AccumulatedFPSFrames++;
        if (m_AccumulatedFPSDeltaTimes >= m_FPSPollDuration)
        {
            m_DeltaAvg = m_AccumulatedFPSDeltaTimes / m_AccumulatedFPSFrames;
            m_DeltaWorst = m_MaxFPSDeltaTime;

            m_AccumulatedFPSFrames = 0;
            m_AccumulatedFPSDeltaTimes -= m_FPSPollDuration;
            m_MaxFPSDeltaTime = 0f;
        }

        m_FPSAvgLabel.text = $"{1f / m_DeltaAvg:0} ({m_DeltaAvg * 1000f:0.0}ms)";
        m_FPSWorstLabel.text = $"{1f / m_DeltaWorst:0} ({m_DeltaWorst * 1000f:0.0}ms)";

        // Ship count
        World world = World.DefaultGameObjectInjectionWorld;
        EntityManager entityManager = world.EntityManager;
        entityManager.CompleteAllTrackedJobs();
        EntityQuery shipsQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Ship>().Build(entityManager);

        m_ShipCountLabel.text = $"{shipsQuery.CalculateEntityCount()}";

        if (m_AdvancedStatsFoldout.value)
        {
            EntityQuery teamManagerQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<TeamManager, TeamManagerAI>()
                .Build(entityManager);
            NativeArray<Entity> teamManagerEntities = teamManagerQuery.ToEntityArray(Allocator.Temp);
            ShowAdvancedStats(teamManagerEntities, entityManager);
        }
    }

    private void ShowAdvancedStats(NativeArray<Entity> teamManagerEntities, EntityManager entityManager)
    {
        for (int i = 0; i < teamManagerEntities.Length; i++)
        {
            Entity teamManagerEntity = teamManagerEntities[i];
            Team team = entityManager.GetComponentData<Team>(teamManagerEntity);
            TeamManager teamManager = entityManager.GetComponentData<TeamManager>(teamManagerEntity);
            TeamManagerAI teamManagerAI = entityManager.GetComponentData<TeamManagerAI>(teamManagerEntity);
            DynamicBuffer<PlanetIntel> planetIntels = entityManager.GetBuffer<PlanetIntel>(teamManagerEntity);

            Color teamColor = new Color(teamManager.Color.x, teamManager.Color.y, teamManager.Color.z, 1f);

            // Check if team stats already exist
            if (!m_AdvancedTeamStatsDictionary.TryGetValue(team.Index, out AdvancedTeamStats advancedTeamStats))
            {
                var container = m_AdvancedTeamStatsTemplate.CloneTree();
                m_AdvancedStatsScrollView.contentContainer.Add(container);
                advancedTeamStats = new AdvancedTeamStats(container, $"TEAM {team.Index}", teamColor);
                m_AdvancedTeamStatsDictionary.Add(team.Index, advancedTeamStats);
            }

            int planetsCount = 0;
            int moonsCount = 0;
            int buildingsCount = 0;

            float3 totalResourcesGenerationRate = float3.zero;
            float3 totalResourcesStorage = float3.zero;
            float3 minResourcesStorage = new float3(float.MaxValue);
            float3 maxResourcesStorage = float3.zero;

            for (int j = 0; j < planetIntels.Length; j++)
            {
                PlanetIntel planetIntel = planetIntels[j];
                if (planetIntel.IsOwned == 1)
                {
                    planetsCount++;
                    totalResourcesGenerationRate += planetIntel.ResourceGenerationRate;
                    totalResourcesStorage += planetIntel.CurrentResourceStorage;
                    minResourcesStorage = math.min(minResourcesStorage, planetIntel.CurrentResourceStorage);
                    maxResourcesStorage = math.max(maxResourcesStorage, planetIntel.CurrentResourceStorage);
                    moonsCount += planetIntel.TotalMoonsCount;
                    buildingsCount += planetIntel.BuildingsCount();
                }
            }

            if (planetsCount <= 0)
            {
                minResourcesStorage = default;
            }

            int3 totalResourcesGenerationRateInt = (int3) totalResourcesGenerationRate;
            int3 totalResourcesStorageInt = (int3) totalResourcesStorage;
            int3 minResourcesStorageInt = (int3) minResourcesStorage;
            int3 maxResourcesStorageInt = (int3) maxResourcesStorage;

            TeamData teamData = new TeamData
            {
                ShipsCount = teamManagerAI.EmpireStatistics.FightersAssessment.Count +
                             teamManagerAI.EmpireStatistics.WorkersAssessment.Count +
                             teamManagerAI.EmpireStatistics.TradersAssessment.Count,
                PlanetsCount = planetsCount,
                MoonsCount = moonsCount,
                BuildingsCount = buildingsCount,
                GenerationRate = totalResourcesGenerationRateInt,
                StorageTotal = totalResourcesStorageInt,
                StorageMin = minResourcesStorageInt,
                StorageMax = maxResourcesStorageInt
            };

            advancedTeamStats.SetTeamData(teamData);
        }
    }
}