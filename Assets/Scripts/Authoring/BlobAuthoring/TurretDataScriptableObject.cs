using System;
using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;

[CreateAssetMenu(menuName = "Game/TurretData", fileName = "TurretData")]
public class TurretDataScriptableObject : ScriptableObject, IBlobAuthoring<TurretData>
{
    public TurretData Data = TurretData.Default();

    public void BakeToBlobData(ref TurretData data, ref BlobBuilder blobBuilder)
    {
        data = Data;
    }
}