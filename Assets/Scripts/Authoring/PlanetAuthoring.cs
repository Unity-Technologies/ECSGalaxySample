using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Serialization;

namespace Galaxy
{
    [RequireComponent(typeof(TeamAuthoring))]
    [RequireComponent(typeof(URPMaterialPropertyBaseColorAuthoring))]
    public class PlanetAuthoring : MonoBehaviour
    {
        public float ShipsAssessmentExtents = 18f;
        public float CaptureTime = 3f;
        
        private class PlanetBaker : Baker<PlanetAuthoring>
        {
            public override void Bake(PlanetAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Planet
                {
                    ShipsAssessmentExtents = authoring.ShipsAssessmentExtents,
                    CaptureTime = authoring.CaptureTime,
                });
                AddBuffer<MoonReference>(entity);
                AddBuffer<PlanetNetwork>(entity);
                AddBuffer<CapturingWorker>(entity);
            }
        }
    }
}