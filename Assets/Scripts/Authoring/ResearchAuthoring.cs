using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(BuildingAuthoring))]
public class ResearchAuthoring : MonoBehaviour
{
    public ResearchDataObject ResearchData;
    
    class Baker : Baker<ResearchAuthoring>
    {
        public override void Bake(ResearchAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, new Research
            {
                ResearchData = BlobAuthoringUtility.BakeToBlob(this, authoring.ResearchData),
            });
        }
    }
}
