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

[System.Serializable]
public class WorkerDataObject : IBlobAuthoring<WorkerData>
{
    public WorkerData Data = WorkerData.Default();
    
    public void BakeToBlobData(ref WorkerData data, ref BlobBuilder blobBuilder)
    {
        data = Data;
    }
}
