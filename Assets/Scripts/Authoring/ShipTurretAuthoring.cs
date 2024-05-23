using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ShipTurretAuthoring : MonoBehaviour
{
    public TurretDataObject TurretData;
    
    class Baker : Baker<ShipTurretAuthoring>
    {
        public override void Bake(ShipTurretAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, new Turret
            {
                TurretData = authoring.TurretData.BakeToBlob(this),
            });
            AddComponent(entity, new Team
            {
                Index = -1,
            });
        }
    }
}
