using System;
using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;

[Serializable]
public struct BuildingData
{
    [Header("Construction")] 
    public float Value;
    public float BuildProbability;
    public float BuildTime;
    
    [Header("VFX")]
    public float2 ExplosionScaleRange;
    
    public static BuildingData Default()
    {
        return new BuildingData
        {
            Value = 1f,
            BuildProbability = 1f,
            BuildTime = 1f,
            ExplosionScaleRange = new float2(4f, 5f),
        };
    }
}

[System.Serializable]
public class BuildingDataObject : IBlobAuthoring<BuildingData>
{
    public BuildingData Data = BuildingData.Default();

    public void BakeToBlobData(ref BuildingData data, ref BlobBuilder blobBuilder)
    {
        data = Data;
    }
}