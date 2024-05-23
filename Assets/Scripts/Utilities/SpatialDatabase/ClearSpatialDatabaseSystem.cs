
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Logging;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(BuildSpatialDatabaseGroup), OrderFirst = true)]
public unsafe partial struct ClearSpatialDatabaseSystem : ISystem
{
    private EntityQuery _spatialDatabasesQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _spatialDatabasesQuery = SystemAPI.QueryBuilder().WithAll<SpatialDatabase, SpatialDatabaseCell, SpatialDatabaseElement>().Build();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (_spatialDatabasesQuery.CalculateEntityCount() > 0)
        {
            BufferLookup<SpatialDatabaseCell> cellsBufferLookup = SystemAPI.GetBufferLookup<SpatialDatabaseCell>(false);
            BufferLookup<SpatialDatabaseElement> elementsBufferLookup = SystemAPI.GetBufferLookup<SpatialDatabaseElement>(false);            
            NativeArray<Entity> spatialDatabaseEntities = _spatialDatabasesQuery.ToEntityArray(Allocator.Temp);

            JobHandle initialDep = state.Dependency;
            
            // Clear each spatial database in a separate thread
            for (int i = 0; i < spatialDatabaseEntities.Length; i++)
            {
                ClearSpatialDatabaseJob clearJob = new ClearSpatialDatabaseJob
                {
                    Entity = spatialDatabaseEntities[i],
                    CellsBufferLookup = cellsBufferLookup,
                    ElementsBufferLookup = elementsBufferLookup,
                };
                state.Dependency = JobHandle.CombineDependencies(state.Dependency, clearJob.Schedule(initialDep));
            }

            spatialDatabaseEntities.Dispose();
        }
    }
    
    [BurstCompile]
    public struct ClearSpatialDatabaseJob : IJob
    {
        public Entity Entity;
        public BufferLookup<SpatialDatabaseCell> CellsBufferLookup;
        public BufferLookup<SpatialDatabaseElement> ElementsBufferLookup;
        
        public void Execute()
        {
            if (CellsBufferLookup.TryGetBuffer(Entity, out DynamicBuffer<SpatialDatabaseCell> cellsBuffer) &&
                ElementsBufferLookup.TryGetBuffer(Entity, out DynamicBuffer<SpatialDatabaseElement> elementsBuffer))
            {
                SpatialDatabase.ClearAndResize(ref cellsBuffer, ref elementsBuffer);
            }
        }
    }
}