using System.Collections;
using System.Collections.Generic;
using Galaxy;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

[RequireComponent(typeof(TeamAuthoring))]
[RequireComponent(typeof(HealthAuthoring))]
[RequireComponent(typeof(InitializableAuthoring))]
[RequireComponent(typeof(ActorTypeAuthoring))]
[RequireComponent(typeof(EntityNameAuthoring))]
public class ShipAuthoring : MonoBehaviour
{
    public ShipDataObject ShipData;
    
    class Baker : Baker<ShipAuthoring>
    {
        public override void Bake(ShipAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, new Ship
            {
                ShipData = BlobAuthoringUtility.BakeToBlob(this, authoring.ShipData),
                
                AccelerationMultiplier = 1f,
                MaxSpeedMultiplier = 1f,
            });
        }
    }
}
