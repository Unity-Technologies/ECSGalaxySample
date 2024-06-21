using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct Config : IComponentData
{
    public float4 NeutralTeamColor;
    
    public Entity SpatialDatabasePrefab;
    public Entity TeamManagerPrefab;
    public Entity TeamManagerReferencesPrefab;
    public Entity PlanetPrefab;
    public Entity MoonPrefab;
    public Entity LaserPrefab;
        
    public bool UseNonDeterministicRandomSeed;
    public uint GameInitializationRandomSeed;
    public float StartCameraDistanceRatio;
    public int InitialBuildingIndex;
    
    public float HomePlanetSpawnRadius;  
    public float MinPlanetSize;
    public float MaxPlanetSize;
    public int MaxTotalShips;
    public int MaxShipsPerTeam;
    public int NeutralPlanetsCount;
    public int PlanetShipAssessmentsPerUpdate;
    public float3 HomePlanetResourceGenerationRate;
    public float3 ResourceGenerationProbabilities;
    public float3 ResourceGenerationRateMin;
    public float3 ResourceGenerationRateMax;
    public float3 PlanetResourceMaxStorage;
    
    public float MoonDistanceFromSurface;
    public int NumMoonsHomePlanet;
    public int2 NumMoonsRange;
    public float2 MoonSizeRange;

    public bool BuildSpatialDatabaseParallel;
    public float SimulationBoundsPadding;
    public int SpatialDatabaseSubdivisions;
    public int PlanetNavigationGridSubdivisions;
    public int PlanetsNetworkCapacity;
    public int ShipsSpatialDatabaseCellCapacity;

    public float LaserEmissionPower;
    public float LaserSparksEmissionPower;
    public float ThrusterEmissionPower;
    
    public bool AutoInitializeGame;
    public bool MustInitializeGame;
}

public struct SimulationRate : IComponentData
{
    public bool UseFixedRate;
    public float FixedTimeStep;
    public float TimeScale;

    public float UnscaledDeltaTime;

    public bool Update;
}

public struct ShipCollection : IBufferElementData
{
    public Entity PrefabEntity;
    public BlobAssetReference<ShipData> ShipData;
}

public struct BuildingCollection : IBufferElementData
{
    public Entity PrefabEntity;
    public BlobAssetReference<BuildingData> BuildingData;
}

[Serializable]
public struct ShipSpawnParams : IBufferElementData
{
    public int IndexInCollection;
    public int SpawnCount;
}

[Serializable]
public struct TeamInfo
{
    public string Name;
    public bool RandomColor;
    public UnityEngine.Color Color;
}

public struct TeamConfig : IBufferElementData
{
    public FixedString128Bytes Name;
    public float4 Color;
}