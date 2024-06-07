using System;
using Unity.Entities;
using UnityEngine;

[Serializable]
public struct FighterData
{
    public float DetectionRange;
    public float AttackRange;
    public float AttackDelay;
    public float AttackDamage;
    public float DotProdThresholdForTargetInSights;
    public float ShipDetectionInterval;
    
    public static FighterData Default()
    {
        return new FighterData
        {
            DetectionRange = 10f,
            AttackRange = 20f,
            AttackDelay = 1f,
            AttackDamage = 1f,
            DotProdThresholdForTargetInSights = 0.5f,
            ShipDetectionInterval = 1f,
        };
    }
}

[System.Serializable]
public class FighterDataObject : IBlobAuthoring<FighterData>
{
    public FighterData Data = FighterData.Default();
    
    public void BakeToBlobData(ref FighterData data, ref BlobBuilder blobBuilder)
    {
        data = Data;
    }
}
