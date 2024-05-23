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

[CreateAssetMenu(fileName = "NewResearchData", menuName = "Game/ResearchData")]
public class ResearchDataObject : BakedScriptableObject<ResearchData>
{
    public ResearchData Data = ResearchData.Default();

    protected override void BakeToBlobData(ref ResearchData data, ref BlobBuilder blobBuilder)
    {
        data = Data;
    }
}