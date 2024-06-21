using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(BuildSpatialDatabaseGroup))]
public partial struct BuildSpatialDatabasesSystem : ISystem
{
    private EntityQuery _spatialDatabasesQuery;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _spatialDatabasesQuery = SystemAPI.QueryBuilder().WithAll<SpatialDatabase, SpatialDatabaseCell, SpatialDatabaseElement>().Build();
        
        state.RequireForUpdate<Config>();
        state.RequireForUpdate<SpatialDatabaseSingleton>();
        state.RequireForUpdate(_spatialDatabasesQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        Config config = SystemAPI.GetSingleton<Config>();
        SpatialDatabaseSingleton spatialDatabaseSingleton = SystemAPI.GetSingleton<SpatialDatabaseSingleton>();

        if (config.BuildSpatialDatabaseParallel)
        {
            CachedSpatialDatabaseUnsafe cachedSpatialDatabase = new CachedSpatialDatabaseUnsafe
            {
                SpatialDatabaseEntity = spatialDatabaseSingleton.TargetablesSpatialDatabase,
                SpatialDatabaseLookup = SystemAPI.GetComponentLookup<SpatialDatabase>(false),
                CellsBufferLookup = SystemAPI.GetBufferLookup<SpatialDatabaseCell>(false),
                ElementsBufferLookup = SystemAPI.GetBufferLookup<SpatialDatabaseElement>(false),
            };

            // Make each ship calculate the octant it belongs to
            SpatialDatabaseParallelComputeCellIndexJob cellIndexJob = new SpatialDatabaseParallelComputeCellIndexJob
            {
                CachedSpatialDatabase = cachedSpatialDatabase,
            };
            state.Dependency = cellIndexJob.ScheduleParallel(state.Dependency);
            
            // Launch X jobs, each responsible for 1/Xth of spatial database cells
            JobHandle initialDep = state.Dependency;
            int parallelCount = math.max(1, JobsUtility.JobWorkerCount - 1);
            for (int s = 0; s < parallelCount; s++)
            {
                BuildSpatialDatabaseParallelJob buildJob = new BuildSpatialDatabaseParallelJob
                {
                    JobSequenceNb = s,
                    JobsTotalCount = parallelCount,
                    CachedSpatialDatabase = cachedSpatialDatabase,
                };
                state.Dependency = JobHandle.CombineDependencies(state.Dependency, buildJob.Schedule(initialDep));
            }
        }
        else
        {
            BuildSpatialDatabaseSingleJob buildJob = new BuildSpatialDatabaseSingleJob
            {
                CachedSpatialDatabase = new CachedSpatialDatabase
                {
                    SpatialDatabaseEntity = spatialDatabaseSingleton.TargetablesSpatialDatabase, 
                    SpatialDatabaseLookup = SystemAPI.GetComponentLookup<SpatialDatabase>(false),
                    CellsBufferLookup = SystemAPI.GetBufferLookup<SpatialDatabaseCell>(false),
                    ElementsBufferLookup = SystemAPI.GetBufferLookup<SpatialDatabaseElement>(false),
                },
            };
            state.Dependency = buildJob.Schedule(state.Dependency);
        }
    }

    [BurstCompile]
    [WithAll(typeof(Targetable))]
    public partial struct BuildSpatialDatabaseSingleJob : IJobEntity, IJobEntityChunkBeginEnd
    {
        public CachedSpatialDatabase CachedSpatialDatabase;
        
        public void Execute(Entity entity, in LocalToWorld ltw, in Team team, in ActorType actorType)
        {
            SpatialDatabaseElement element = new SpatialDatabaseElement
            {
                Entity = entity,
                Position = ltw.Position,
                Team = (byte)team.Index,
                Type = actorType.Type,
            };
            SpatialDatabase.AddToDataBase(in CachedSpatialDatabase._SpatialDatabase,
                ref CachedSpatialDatabase._SpatialDatabaseCells, ref CachedSpatialDatabase._SpatialDatabaseElements,
                element);
        }

        public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            CachedSpatialDatabase.CacheData();
            return true;
        }

        public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask,
            bool chunkWasExecuted)
        {
        }
    }

    [BurstCompile]
    public partial struct SpatialDatabaseParallelComputeCellIndexJob : IJobEntity, IJobEntityChunkBeginEnd
    {
        public CachedSpatialDatabaseUnsafe CachedSpatialDatabase;
        
        // other cached data
        private UniformOriginGrid _grid;
        
        public void Execute(in LocalToWorld ltw, ref SpatialDatabaseCellIndex sdCellIndex)
        {
            sdCellIndex.CellIndex = UniformOriginGrid.GetCellIndex(in _grid, ltw.Position);
        }

        public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            CachedSpatialDatabase.CacheData();
            _grid = CachedSpatialDatabase._SpatialDatabase.Grid;
            return true;
        }

        public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask,
            bool chunkWasExecuted)
        {
        }
    }

    [BurstCompile]
    [WithAll(typeof(Targetable))]
    public partial struct BuildSpatialDatabaseParallelJob : IJobEntity, IJobEntityChunkBeginEnd
    {
        public int JobSequenceNb;
        public int JobsTotalCount;
        public CachedSpatialDatabaseUnsafe CachedSpatialDatabase;
        
        public void Execute(Entity entity, in LocalToWorld ltw, in Team team, in SpatialDatabaseCellIndex sdCellIndex, in ActorType actorType)
        {
            if (sdCellIndex.CellIndex % JobsTotalCount == JobSequenceNb)
            {
                SpatialDatabaseElement element = new SpatialDatabaseElement
                {
                    Entity = entity,
                    Position = ltw.Position,
                    Team = (byte)team.Index,
                    Type = actorType.Type,
                };
                SpatialDatabase.AddToDataBase(in CachedSpatialDatabase._SpatialDatabase,
                    ref CachedSpatialDatabase._SpatialDatabaseCells, ref CachedSpatialDatabase._SpatialDatabaseElements,
                    element, sdCellIndex.CellIndex);
            }
        }

        public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            CachedSpatialDatabase.CacheData();
            return true;
        }

        public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask,
            bool chunkWasExecuted)
        {
        }
    }
}
