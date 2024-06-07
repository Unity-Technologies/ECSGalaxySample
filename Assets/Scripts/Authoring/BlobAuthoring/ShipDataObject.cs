using System;
using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;

[Serializable]
public struct ShipData
{
    [Header("General Movement")]
    public float MaxSpeed;
    public float Acceleration;
    public float SteeringSharpness;

    [Header("Planet navigation")] 
    public bool ShouldAvoidTargetPlanet;
    public float PlanetOrbitOffset;
    public float PlanetAvoidanceDistance;
    public float PlanetAvoidanceRelativeOffset;
    public float MaxDistanceSqForPlanetProximityImportanceScaling;
    public float2 PlanetProximityImportanceRemap;

    [Header("Construction")] 
    public float Value;
    public float BuildProbabilityForShipType;
    public float3 ResourcesCost;
    public float BuildTime;
    
    [Header("VFX")]
    public float2 ExplosionScaleRange;
    public float3 ThrusterLocalPosition;
    public float ThrusterSize;
    public float ThrusterLength;

    public static ShipData Default()
    {
        return new ShipData
        {
            Value = 1f,
            
            MaxSpeed = 10f,
            Acceleration = 2f,
            SteeringSharpness = 2f,
            
            ShouldAvoidTargetPlanet = false,
            PlanetOrbitOffset = 1f,
            PlanetAvoidanceDistance = 30f,
            PlanetAvoidanceRelativeOffset = 1.25f,
            MaxDistanceSqForPlanetProximityImportanceScaling = 100f * 100f,
            PlanetProximityImportanceRemap = new float2(0.7f, 1f),
            
            BuildProbabilityForShipType = 1,
            ResourcesCost = new float3(1f),
            BuildTime = 1f,
            
            ExplosionScaleRange = new float2(1f, 2f),
        };
    }
}

[System.Serializable]
public class ShipDataObject : IBlobAuthoring<ShipData>
{
    public ShipData Data = ShipData.Default();

    public void BakeToBlobData(ref ShipData data, ref BlobBuilder blobBuilder)
    {
        data = Data;
    }
}