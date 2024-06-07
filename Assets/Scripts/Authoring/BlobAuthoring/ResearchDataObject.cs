using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public struct ResearchData
{
    [FormerlySerializedAs("ResourcesDrain")] public float3 ResourcesConsumptionRate;

    public ResearchBonuses ResearchBonuses;
    
    public static ResearchData Default()
    {
        return new ResearchData
        {
            ResourcesConsumptionRate = float3.zero,
            ResearchBonuses = default,
        };
    }
}

[System.Serializable]
public class ResearchDataObject : IBlobAuthoring<ResearchData>
{
    public ResearchData Data = ResearchData.Default();

    public void BakeToBlobData(ref ResearchData data, ref BlobBuilder blobBuilder)
    {
        data = Data;
    }
}