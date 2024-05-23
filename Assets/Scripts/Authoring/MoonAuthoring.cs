using Unity.Entities;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Galaxy
{
    public class MoonAuthoring : MonoBehaviour
    {
        private class Baker : Baker<MoonAuthoring>
        {
            public override void Bake(MoonAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Moon { });
                AddComponent<BuildingReference>(entity);
            }
        }
    }
}