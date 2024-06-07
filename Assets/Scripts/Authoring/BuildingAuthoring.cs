using System.Collections;
using System.Collections.Generic;
using Galaxy;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[RequireComponent(typeof(TeamAuthoring))]
[RequireComponent(typeof(HealthAuthoring))]
[RequireComponent(typeof(InitializableAuthoring))]
[RequireComponent(typeof(ActorTypeAuthoring))]
[RequireComponent(typeof(EntityNameAuthoring))]
public class BuildingAuthoring : MonoBehaviour
{
    public BuildingDataObject BuildingData;
    
    class Baker : Baker<BuildingAuthoring>
    {
        public override void Bake(BuildingAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, new Building
            {
                BuildingData = BlobAuthoringUtility.BakeToBlob(this, authoring.BuildingData),
            });
        }
    }
}
