using System;
using Unity.Entities;
using UnityEngine;

[Serializable]
public struct WorkerData
{
    public float CaptureRange;
    public float CaptureSpeed;
    public float BuildRange;
    public float BuildSpeed;
    
    public static WorkerData Default()
    {
        return new WorkerData
        {
            CaptureSpeed =  1,
            CaptureRange = 3,
            BuildRange =  1,
            BuildSpeed = 1,
        };
    }
}

[CreateAssetMenu(fileName = "NewWorkerData", menuName = "Game/WorkerData")]
public class WorkerDataObject : BakedScriptableObject<WorkerData>
{
    public WorkerData Data = WorkerData.Default();
    
    protected override void BakeToBlobData(ref WorkerData data, ref BlobBuilder blobBuilder)
    {
        data = Data;
    }
}
