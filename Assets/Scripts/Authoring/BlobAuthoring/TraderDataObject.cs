using System;
using Unity.Entities;
using UnityEngine;

[Serializable]
public struct TraderData
{
    public float ResourceExchangeRange;
    public float ResourceCarryCapacity;
    public float ResourceCarryMinimumLoad;
    
    public static TraderData Default()
    {
        return new TraderData
        {
            ResourceExchangeRange = 1f,
            ResourceCarryCapacity = 20f,
            ResourceCarryMinimumLoad = 3f,
        };
    }
}

[System.Serializable]
public class TraderDataObject : IBlobAuthoring<TraderData>
{
    public TraderData Data = TraderData.Default();
    
    public void BakeToBlobData(ref TraderData data, ref BlobBuilder blobBuilder)
    {
        data = Data;
    }
}
