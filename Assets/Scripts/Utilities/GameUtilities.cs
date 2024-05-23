using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Logging;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

public struct IdentifiedImportance
{
    public int Id;
    public float Importance;
}

public struct IdentifiedImportanceVector
{
    public int Id;
    public float3 Importance;
}

public static class GameUtilities
{
    public static bool TryGetSingletonEntity<T>(EntityManager entityManager, out Entity singletonEntity) where T : unmanaged, IComponentData
    {
        EntityQuery singletonQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<T>().Build(entityManager);
        if (singletonQuery.HasSingleton<T>())
        {
            singletonEntity = singletonQuery.GetSingletonEntity();
            return true;
        }

        singletonEntity = default;
        return false;
    }
    
    public static bool TryGetSingleton<T>(EntityManager entityManager, out T singleton) where T : unmanaged, IComponentData
    {
        EntityQuery singletonQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<T>().Build(entityManager);
        if (singletonQuery.HasSingleton<T>())
        {
            singleton = singletonQuery.GetSingleton<T>();
            return true;
        }

        singleton = default;
        return false;
    }
    
    public static bool TryGetSingletonRW<T>(EntityManager entityManager, out RefRW<T> singletonRW) where T : unmanaged, IComponentData
    {
        EntityQuery singletonQuery = new EntityQueryBuilder(Allocator.Temp).WithAllRW<T>().Build(entityManager);
        if (singletonQuery.HasSingleton<T>())
        {
            singletonRW = singletonQuery.GetSingletonRW<T>();
            return true;
        }

        singletonRW = default;
        return false;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ApplyDamage(ref Health health, float damage)
    {
        health.CurrentHealth = math.clamp(health.CurrentHealth - damage, 0f, health.MaxHealth);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Heal(ref Health health, float heal)
    {
        if (!health.IsDead)
        {
            ApplyDamage(ref health, -heal);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetTeam(EntityManager entityManager, Entity entity, int team)
    {
        if (team > byte.MaxValue)
        {
            Log.Error("Error: surpassed max teams count");
        }
        
        entityManager.SetComponentData(entity, new Team { Index = team });
        entityManager.SetComponentEnabled<ApplyTeam>(entity, true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetTeam(EntityCommandBuffer ecb, Entity entity, int team)
    {
        if (team > byte.MaxValue)
        {
            Log.Error("Error: surpassed max teams count");
        }

        ecb.SetComponent(entity, new Team { Index = team });
        ecb.SetComponentEnabled<ApplyTeam>(entity, true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetTeam(EntityCommandBuffer.ParallelWriter ecb, int sortKey, Entity entity, int team)
    {
        if (team > byte.MaxValue)
        {
            Log.Error("Error: surpassed max teams count");
        }

        ecb.SetComponent(sortKey, entity, new Team { Index = team });
        ecb.SetComponentEnabled<ApplyTeam>(sortKey, entity, true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Random GetDeterministicRandom(int v)
    {
        return Random.CreateFromIndex(GetUniqueUIntFromInt(v));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Random GetDeterministicRandom(int vA, int vB)
    {
        return Random.CreateFromIndex(GetUniqueUIntFromInt(vA) + GetUniqueUIntFromInt(vB));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Random GetDeterministicRandom(int vA, uint vB)
    {
        return Random.CreateFromIndex(GetUniqueUIntFromInt(vA) + vB);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Random GetDeterministicRandom(int vA, int vB, int vC)
    {
        return Random.CreateFromIndex(GetUniqueUIntFromInt(vA) + GetUniqueUIntFromInt(vB) + GetUniqueUIntFromInt(vC));
    }

    public static bool HasEnoughResources(float3 resourceNeeds, in Planet planet)
    {
        return math.all(planet.ResourceCurrentStorage >= resourceNeeds);
    }

    public static bool TryConsumeResources(float3 resourceConsumption, ref Planet planet)
    {
        if (math.all(planet.ResourceCurrentStorage >= resourceConsumption))
        {
            planet.ResourceCurrentStorage -= resourceConsumption;
            return true;
        }

        return false;
    }
    
    public static bool TryConsumeResources(float resourceConsumption, ref Planet planet)
    {
        float3 resourceCurrentStorage = planet.ResourceCurrentStorage;   
        int maxResource = 0;
        if (resourceCurrentStorage.y > resourceCurrentStorage.x) 
        {
            maxResource = 1;
        }
        if (resourceCurrentStorage.z > resourceCurrentStorage[maxResource]) 
        {
            maxResource = 2;
        }
        
        if (resourceCurrentStorage[maxResource] >= resourceConsumption)
        {
            planet.ResourceCurrentStorage[maxResource] -= resourceConsumption;
            return true;
        }

        return false;
    }

    public static Entity CreateBuilding(EntityManager entityManager, Entity buildingPrefab, Entity moonEntity, Entity planetEntity, int team)
    {
        Entity buildingEntity = entityManager.Instantiate(buildingPrefab);
        entityManager.AddComponentData(buildingEntity, new Parent { Value = moonEntity });

        Building building = entityManager.GetComponentData<Building>(buildingPrefab);
        building.PlanetEntity = planetEntity;
        building.MoonEntity = moonEntity;
        entityManager.SetComponentData(buildingEntity, building);

        GameUtilities.SetTeam(entityManager, buildingEntity, team);

        entityManager.SetComponentData(moonEntity, new BuildingReference
        {
            BuildingEntity = buildingEntity,
        });

        return buildingEntity;
    }

    public static Entity CreateBuilding(EntityCommandBuffer ecb, Entity buildingPrefab, Entity moonEntity, Entity planetEntity, int team, in ComponentLookup<Building> buildingLookup)
    {
        Entity buildingEntity = ecb.Instantiate(buildingPrefab);
        ecb.AddComponent(buildingEntity, new Parent { Value = moonEntity });

        Building building = buildingLookup[buildingPrefab];
        building.PlanetEntity = planetEntity;
        building.MoonEntity = moonEntity;
        ecb.SetComponent(buildingEntity, building);

        GameUtilities.SetTeam(ecb, buildingEntity, team);

        ecb.SetComponent(moonEntity, new BuildingReference
        {
            BuildingEntity = buildingEntity,
        });

        return buildingEntity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SpawnLaser(EntityCommandBuffer ecb, Entity laserPrefab, float4 laserColor, float3 startPos, float3 direction, float length)
    {
        Entity laserEntity = ecb.Instantiate(laserPrefab);
        ecb.SetComponent(laserEntity, new LocalTransform
        {
            Position = startPos,
            Rotation = quaternion.LookRotationSafe(direction, math.up()),
            Scale = 1,
        });
        ecb.SetComponent(laserEntity, new PostTransformMatrix
        {
            Value = float4x4.Scale(new float3(1f, 1f, length)),
        });
        ecb.SetComponent(laserEntity, new URPMaterialPropertyEmissionColor
        {
            Value = laserColor,
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddAction<T>(ref DynamicBuffer<T> considerations, ref AIProcessor aIProcessor, T consideration,
        AIAction action)
        where T : unmanaged, IBufferElementData
    {
        considerations.Add(consideration);
        aIProcessor.AddAction(action);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CalculateProximityImportance(float3 selfPosition, float3 otherPosition, float maxDistanceSqForPlanetProximityImportanceScaling, float2 planetProximityImportanceRemap)
    {
        float maxDistanceSq = math.distancesq(selfPosition, otherPosition);
        float proximityImportance = 1f - math.saturate(maxDistanceSq / maxDistanceSqForPlanetProximityImportanceScaling);
        return math.remap(0f, 1f, planetProximityImportanceRemap.x,
            planetProximityImportanceRemap.y, proximityImportance);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetWeightedRandomIndex(float totalWeights, in NativeList<float> importances, ref Random random)
    {
        float decision = random.NextFloat(0f, totalWeights);
        totalWeights = 0f;
        for (int i = 0; i < importances.Length; i++)
        {
            totalWeights += importances[i];
            if (decision < totalWeights)
            {
                return i;
            }
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetWeightedRandomIndex(float3 totalWeights, in NativeList<float3> importances, ref Random random, out int subIndex)
    {
        subIndex = 0;
        float decision = random.NextFloat(0f, math.csum(totalWeights));
        float newTotal = 0f;
        for (int i = 0; i < importances.Length; i++)
        {
            float3 values = importances[i];
            float valuesSum = math.csum(values);
            newTotal += valuesSum;
            if (decision < newTotal)
            {
                float subDecision = random.NextFloat(0f, valuesSum);
                if (subDecision < values.x)
                {
                    subIndex = 0;
                }
                else if (subDecision < values.x + values.y)
                {
                    subIndex = 1;
                }
                else
                {
                    subIndex = 2;
                }
                
                return i;
            }
        }

        subIndex = -1;
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetTraderResourcesMask(float3 diff, bool forReceiver, out bool3 mask, out float3 maskMultiplier)
    {
        mask = default;
        bool3 diffSignIsPositive = diff > float3.zero;
        if (forReceiver)
        {
            mask = diffSignIsPositive == new bool3(false);
        }
        else
        {
            mask = diffSignIsPositive == new bool3(true);
        }
        maskMultiplier = math.select(new float3(0f), new float3(1f), mask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetUniqueUIntFromInt(int val)
    {
        return math.select((uint)val, (uint)(int.MaxValue) + (uint)(val), val <= 0);
    }

    public static float GetTimeScale(WorldUnmanaged world)
    {
        EntityQuery simRateQuery =
            new EntityQueryBuilder(Allocator.Temp).WithAll<SimulationRate>().Build(world.EntityManager);
        if (simRateQuery.HasSingleton<SimulationRate>())
        {
            SimulationRate simRate = simRateQuery.GetSingleton<SimulationRate>();
            return simRate.TimeScale;
        }

        return 1f;
    }

    public static void SetTimeScale(WorldUnmanaged world, float timeScale)
    {
        EntityQuery simRateQuery =
            new EntityQueryBuilder(Allocator.Temp).WithAllRW<SimulationRate>().Build(world.EntityManager);
        if (simRateQuery.HasSingleton<SimulationRate>())
        {
            ref SimulationRate simRate = ref simRateQuery.GetSingletonRW<SimulationRate>().ValueRW;
            simRate.TimeScale = timeScale;
        }
    }

    public static bool GetUseFixedRate(WorldUnmanaged world)
    {
        EntityQuery simRateQuery =
            new EntityQueryBuilder(Allocator.Temp).WithAll<SimulationRate>().Build(world.EntityManager);
        if (simRateQuery.HasSingleton<SimulationRate>())
        {
            SimulationRate simRate = simRateQuery.GetSingleton<SimulationRate>();
            return simRate.UseFixedRate;
        }

        return false;
    }

    public static void SetUseFixedRate(WorldUnmanaged world, bool useFixedRate)
    {
        EntityQuery simRateQuery =
            new EntityQueryBuilder(Allocator.Temp).WithAllRW<SimulationRate>().Build(world.EntityManager);
        if (simRateQuery.HasSingleton<SimulationRate>())
        {
            ref SimulationRate simRate = ref simRateQuery.GetSingletonRW<SimulationRate>().ValueRW;
            simRate.UseFixedRate = useFixedRate;
            simRate.Update = true;
        }
    }

    public static float GetFixedTimeStep(WorldUnmanaged world)
    {
        EntityQuery simRateQuery =
            new EntityQueryBuilder(Allocator.Temp).WithAll<SimulationRate>().Build(world.EntityManager);
        if (simRateQuery.HasSingleton<SimulationRate>())
        {
            SimulationRate simRate = simRateQuery.GetSingleton<SimulationRate>();
            return simRate.FixedTimeStep;
        }

        return 1f;
    }

    public static void SetFixedTimeStep(WorldUnmanaged world, float fixedTimeStep)
    {
        EntityQuery simRateQuery =
            new EntityQueryBuilder(Allocator.Temp).WithAllRW<SimulationRate>().Build(world.EntityManager);
        if (simRateQuery.HasSingleton<SimulationRate>())
        {
            ref SimulationRate simRate = ref simRateQuery.GetSingletonRW<SimulationRate>().ValueRW;
            simRate.FixedTimeStep = fixedTimeStep;
            simRate.Update = true;
        }
    }
}
