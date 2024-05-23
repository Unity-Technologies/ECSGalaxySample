using System;
using Unity.Entities;
using UnityEngine;

[Serializable]
public struct FactoryData
{
    public static FactoryData Default()
    {
        return new FactoryData();
    }
}

[CreateAssetMenu(fileName = "NewFactoryData", menuName = "Game/FactoryData")]
public class FactoryDataObject : BakedScriptableObject<FactoryData>
{
    public FactoryData Data = FactoryData.Default();

    protected override void BakeToBlobData(ref FactoryData data, ref BlobBuilder blobBuilder)
    {
        data = Data;
    }
}