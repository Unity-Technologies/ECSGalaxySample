using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Galaxy
{
    [RequireComponent(typeof(ShipAuthoring))]
    public class WorkerAuthoring : MonoBehaviour
    {
        public WorkerDataObject WorkerData;
        
        private class Baker : Baker<WorkerAuthoring>
        {
            public override void Bake(WorkerAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Worker
                {
                    WorkerData = BlobAuthoringUtility.BakeToBlob(this, authoring.WorkerData),
                });
                AddComponent(entity, new ExecutePlanetCapture());
                SetComponentEnabled<ExecutePlanetCapture>(entity, false);
                AddComponent(entity, new ExecuteBuild());
                SetComponentEnabled<ExecuteBuild>(entity, false);
            }
        }
    }
}