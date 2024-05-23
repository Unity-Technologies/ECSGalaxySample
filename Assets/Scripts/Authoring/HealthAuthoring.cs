using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(InitializableAuthoring))]
public class HealthAuthoring : MonoBehaviour
{
    public float MaxHealth;
    
    class Baker : Baker<HealthAuthoring>
    {
        public override void Bake(HealthAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent(entity, new Health
            {
                MaxHealth = authoring.MaxHealth,
                CurrentHealth = authoring.MaxHealth, 
            });
        }
    }
}
