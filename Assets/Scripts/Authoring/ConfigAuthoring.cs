using Unity.Collections;
using Unity.Entities;
using Unity.Logging;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Galaxy
{
    public class ConfigAuthoring : MonoBehaviour
    {
        public TeamInfo[] TeamInfos = new TeamInfo[0];
        public Color NeutralTeamColor;

        [Header("Prefabs")] 
        public GameObject[] ShipsCollection;
        public GameObject[] BuildingsCollection;
        public GameObject SpatialDatabasePrefab; 
        public GameObject TeamManagerPrefab;
        public GameObject TeamManagerReferencesPrefab;
        public GameObject PlanetPrefab;
        public GameObject MoonPrefab;
        public GameObject LaserPrefab;

        [Header("General")] 
        public bool AutoInitializeGame = true;
        public bool UseNonDeterministicRandomSeed = false;
        public uint GameInitializationRandomSeed = 12345;
        public bool UseFixedSimulationDeltaTime = true;
        public float FixedDeltaTime = 0.0333333f;
        public float StartCameraDistanceRatio = 1.4f;
        public int MaxTotalShips;
        public int MaxShipsPerTeam;
        public ShipSpawnParams[] InitialShipSpawns;
        public int InitialBuildingIndex;
        
        [Header("Planets")]
        public float HomePlanetSpawnRadius; 
        public float MinPlanetSize;
        public float MaxPlanetSize;
        public int PlanetShipAssessmentsPerUpdate;
        public int NeutralPlanetsCount; 
        public float3 HomePlanetResourceGenerationRate;
        public float3 ResourceGenerationProbabilities;
        public float3 ResourceGenerationRateMin;
        public float3 ResourceGenerationRateMax;
        public float3 PlanetResourceMaxStorage;

        [Header("Moons")] 
        public float MoonDistanceFromSurface;
        public int NumMoonsHomePlanet;
        public int2 NumMoonsRange;
        public float2 MoonSizeRange;
        
        [Header("Acceleration Structures")] 
        public bool BuildSpatialDatabaseParallel = true;
        public float SimulationBoundsPadding;
        public int SpatialDatabaseSubdivisions = 5;
        public int PlanetNavigationGridSubdivisions = 4;
        public int PlanetsNetworkCapacity;
        public int ShipsSpatialDatabaseCellCapacity = 256;
        
        [Header("VFX")]
        public float LaserEmissionPower = 3f;
        public float ThrusterEmissionPower = 300f;
        public float LaserSparksEmissionPower = 300f;
        
        private class Baker : Baker<ConfigAuthoring>
        {
            public override void Bake(ConfigAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new Config
                {
                    NeutralTeamColor = (Vector4)authoring.NeutralTeamColor,
                    
                    SpatialDatabasePrefab = GetEntity(authoring.SpatialDatabasePrefab, TransformUsageFlags.None),
                    TeamManagerPrefab = GetEntity(authoring.TeamManagerPrefab, TransformUsageFlags.None),
                    TeamManagerReferencesPrefab = GetEntity(authoring.TeamManagerReferencesPrefab, TransformUsageFlags.None),
                    PlanetPrefab = GetEntity(authoring.PlanetPrefab, TransformUsageFlags.None),
                    MoonPrefab = GetEntity(authoring.MoonPrefab, TransformUsageFlags.None),
                    LaserPrefab = GetEntity(authoring.LaserPrefab, TransformUsageFlags.None),
                    
                    UseNonDeterministicRandomSeed = authoring.UseNonDeterministicRandomSeed,
                    GameInitializationRandomSeed = authoring.GameInitializationRandomSeed,
                    StartCameraDistanceRatio = authoring.StartCameraDistanceRatio,
                    InitialBuildingIndex = authoring.InitialBuildingIndex,
                    
                    HomePlanetSpawnRadius = authoring.HomePlanetSpawnRadius,
                    MinPlanetSize = authoring.MinPlanetSize,
                    MaxPlanetSize = authoring.MaxPlanetSize,
                    MaxTotalShips = authoring.MaxTotalShips,
                    MaxShipsPerTeam = authoring.MaxShipsPerTeam,
                    NeutralPlanetsCount = authoring.NeutralPlanetsCount,
                    PlanetShipAssessmentsPerUpdate = authoring.PlanetShipAssessmentsPerUpdate,
                    HomePlanetResourceGenerationRate = authoring.HomePlanetResourceGenerationRate,
                    ResourceGenerationProbabilities = authoring.ResourceGenerationProbabilities,
                    ResourceGenerationRateMin = authoring.ResourceGenerationRateMin,
                    ResourceGenerationRateMax = authoring.ResourceGenerationRateMax,
                    PlanetResourceMaxStorage = authoring.PlanetResourceMaxStorage,
                    
                    MoonDistanceFromSurface = authoring.MoonDistanceFromSurface,
                    NumMoonsHomePlanet = authoring.NumMoonsHomePlanet,
                    NumMoonsRange = authoring.NumMoonsRange,
                    MoonSizeRange = authoring.MoonSizeRange,
                    
                    BuildSpatialDatabaseParallel = authoring.BuildSpatialDatabaseParallel,
                    SimulationBoundsPadding = authoring.SimulationBoundsPadding,
                    SpatialDatabaseSubdivisions = authoring.SpatialDatabaseSubdivisions,
                    PlanetNavigationGridSubdivisions = authoring.PlanetNavigationGridSubdivisions,
                    PlanetsNetworkCapacity = authoring.PlanetsNetworkCapacity,
                    ShipsSpatialDatabaseCellCapacity = authoring.ShipsSpatialDatabaseCellCapacity,
                    
                    LaserEmissionPower = authoring.LaserEmissionPower,
                    LaserSparksEmissionPower = authoring.LaserSparksEmissionPower,
                    ThrusterEmissionPower = authoring.ThrusterEmissionPower,
                    
                    AutoInitializeGame = authoring.AutoInitializeGame,
                    MustInitializeGame = authoring.AutoInitializeGame,
                });
                AddComponent(entity, new SimulationRate
                {
                    UseFixedRate = authoring.UseFixedSimulationDeltaTime,
                    FixedTimeStep = authoring.FixedDeltaTime,
                    TimeScale = 1f,
                    Update = true,
                });

                DynamicBuffer<ShipCollection> shipsCollectionBuffer = AddBuffer<ShipCollection>(entity);
                for (int i = 0; i < authoring.ShipsCollection.Length; i++)
                {
                    shipsCollectionBuffer.Add(new ShipCollection
                    {
                        PrefabEntity = GetEntity(authoring.ShipsCollection[i], TransformUsageFlags.None),
                        ShipData = BlobAuthoringUtility.BakeToBlob(this, authoring.ShipsCollection[i].GetComponent<ShipAuthoring>().ShipData),
                    });
                }
                
                DynamicBuffer<BuildingCollection> buildingsCollectionBuffer = AddBuffer<BuildingCollection>(entity);
                for (int i = 0; i < authoring.BuildingsCollection.Length; i++)
                {
                    buildingsCollectionBuffer.Add(new BuildingCollection()
                    {
                        PrefabEntity = GetEntity(authoring.BuildingsCollection[i], TransformUsageFlags.None),
                        BuildingData = BlobAuthoringUtility.BakeToBlob(this, authoring.BuildingsCollection[i].GetComponent<BuildingAuthoring>().BuildingData),
                    });
                }

                DynamicBuffer<ShipSpawnParams> shipSpawnsBuffer = AddBuffer<ShipSpawnParams>(entity);
                for (int i = 0; i < authoring.InitialShipSpawns.Length; i++)
                {
                    shipSpawnsBuffer.Add(authoring.InitialShipSpawns[i]);
                }
                
                DynamicBuffer<TeamConfig> teamConfigs = AddBuffer<TeamConfig>(entity);
                teamConfigs.Length = authoring.TeamInfos.Length;
                for (int i = 0; i < authoring.TeamInfos.Length; i++)
                {
                    FixedString128Bytes name = new FixedString128Bytes();
                    name.CopyFromTruncated(authoring.TeamInfos[i].Name);
                    
                    TeamConfig tc = new TeamConfig();
                    tc.Name = name;
                    if (authoring.TeamInfos[i].RandomColor)
                    {
                        Color randomCol = UnityEngine.Random.ColorHSV(0f, 1f, 0.5f, 1f, 1f, 1f);
                        tc.Color =  (Vector4)randomCol;
                    }
                    else
                    {
                        tc.Color = (Vector4)authoring.TeamInfos[i].Color;
                    }
                    teamConfigs[i] = tc;
                }
            }
        }
    }
}