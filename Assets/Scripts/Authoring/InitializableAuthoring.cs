using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class InitializableAuthoring : MonoBehaviour
{
    class Baker : Baker<InitializableAuthoring>
    {
        public override void Bake(InitializableAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent(entity, new Initialize());
        }
    }
}
