using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(BuildingAuthoring))]
public class TurretAuthoring : MonoBehaviour
{
    public TurretDataObject TurretData;
    
    class Baker : Baker<TurretAuthoring>
    {
        public override void Bake(TurretAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, new Turret
            {
                TurretData = BlobAuthoringUtility.BakeToBlob(this, authoring.TurretData),
            });
        }
    }
}
