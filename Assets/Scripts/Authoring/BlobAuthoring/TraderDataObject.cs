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

[CreateAssetMenu(fileName = "NewTraderData", menuName = "Game/TraderIData")]
public class TraderDataObject : BakedScriptableObject<TraderData>
{
    public TraderData Data = TraderData.Default();
    
    protected override void BakeToBlobData(ref TraderData data, ref BlobBuilder blobBuilder)
    {
        data = Data;
    }
}
