using System;
using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;

[Serializable]
public struct TurretData
{
    public float RotationSharpness;
    public float AttackRange;
    public float AttackDelay;
    public float AttackDamage;
    public float ShipDetectionInterval;
    public float ResourceCost;
    
    public static TurretData Default()
    {
        return new TurretData
        {
            RotationSharpness = 8f,
            AttackRange = 5f,
            AttackDelay = 1f,
            AttackDamage = 1f,
            ShipDetectionInterval = 1f,
        };
    }
}

[System.Serializable]
public class TurretDataObject : IBlobAuthoring<TurretData>
{
    public TurretData Data = TurretData.Default();

    public void BakeToBlobData(ref TurretData data, ref BlobBuilder blobBuilder)
    {
        data = Data;
    }
}