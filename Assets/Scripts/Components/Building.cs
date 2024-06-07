using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Serialization;
using Random = Unity.Mathematics.Random;

public struct Building : IComponentData
{
    public BlobAssetReference<BuildingData> BuildingData;
    
    public Entity PlanetEntity;
    public Entity MoonEntity;
    public Random Random;
}

public struct Factory : IComponentData
{
    public Entity CurrentProducedPrefab;
    public float ProductionTimer;
    public ResearchBonuses ProductionResearchBonuses;
}

public struct Turret : IComponentData
{
    public BlobAssetReference<TurretData> TurretData;
    
    public float DetectionTimer;
    public float AttackTimer;
    public Entity ActiveTarget;
    public float3 ActiveTargetPosition;
    public byte MustAttack;
}

[Serializable]
public struct ResearchBonuses
{
    public float ShipSpeedMultiplier;
    public float ShipAccelerationMultiplier;
    public float ShipMaxHealthMultiplier;
    public float ShipDamageMultiplier;
    public float FactoryBuildSpeedMultiplier;
    public float3 PlanetResourceGenerationRadeAdd;

    public void Reset()
    {
        ShipSpeedMultiplier = 1f;
        ShipAccelerationMultiplier = 1f;
        ShipMaxHealthMultiplier = 1f;
        ShipDamageMultiplier = 1f;
        FactoryBuildSpeedMultiplier = 1f;
        PlanetResourceGenerationRadeAdd = float3.zero;
    }

    public void Add(ResearchBonuses other)
    {
        ShipSpeedMultiplier += other.ShipSpeedMultiplier;
        ShipAccelerationMultiplier += other.ShipAccelerationMultiplier;
        ShipMaxHealthMultiplier += other.ShipMaxHealthMultiplier;
        ShipDamageMultiplier += other.ShipDamageMultiplier;
        FactoryBuildSpeedMultiplier += other.FactoryBuildSpeedMultiplier;
        PlanetResourceGenerationRadeAdd += other.PlanetResourceGenerationRadeAdd;
    }
}

public struct Research : IComponentData
{
    public BlobAssetReference<ResearchData> ResearchData;
}