using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateAfter(typeof(BuildSpatialDatabaseGroup))]
[UpdateBefore(typeof(BuildingSystem))]
public partial struct PlanetSystem : ISystem
{
    private EntityQuery _spatialDatabasesQuery;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _spatialDatabasesQuery = SystemAPI.QueryBuilder().WithAll<SpatialDatabase, SpatialDatabaseCell, SpatialDatabaseElement>().Build();
        
        state.RequireForUpdate<Config>();
        state.RequireForUpdate<SpatialDatabaseSingleton>();
        state.RequireForUpdate(_spatialDatabasesQuery);
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<TeamManagerReference>();
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        Config config = SystemAPI.GetSingleton<Config>();
        SpatialDatabaseSingleton spatialDatabaseSingleton = SystemAPI.GetSingleton<SpatialDatabaseSingleton>();
        EntityQuery planetsQuery = SystemAPI.QueryBuilder().WithAll<Planet>().Build();
        
        PlanetShipsAssessmentJob shipsAssessmentJob = new PlanetShipsAssessmentJob
        {
            TotalPlanetsCount = planetsQuery.CalculateEntityCount(),
            PlanetShipsAssessmentsPerUpdate = config.PlanetShipAssessmentsPerUpdate,
            CachedSpatialDatabase = new CachedSpatialDatabaseRO
            {
                SpatialDatabaseEntity = spatialDatabaseSingleton.TargetablesSpatialDatabase, 
                SpatialDatabaseLookup = SystemAPI.GetComponentLookup<SpatialDatabase>(true),
                CellsBufferLookup = SystemAPI.GetBufferLookup<SpatialDatabaseCell>(true),
                ElementsBufferLookup = SystemAPI.GetBufferLookup<SpatialDatabaseElement>(true),
            },
        };
        state.Dependency = shipsAssessmentJob.Schedule(state.Dependency);
        
        PlanetConversionJob conversionJob = new PlanetConversionJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
            BuildingReferenceLookup = SystemAPI.GetComponentLookup<BuildingReference>(true),
            BuildingLookup = SystemAPI.GetComponentLookup<Building>(true),
        };
        state.Dependency = conversionJob.Schedule(state.Dependency);

        PlanetResourcesJob resourcesJob = new PlanetResourcesJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
        };
        state.Dependency = resourcesJob.Schedule(state.Dependency);
        
        PlanetClearBuildingsDataJob planetClearBuildingsDataJob = new PlanetClearBuildingsDataJob
        { };
        state.Dependency = planetClearBuildingsDataJob.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct PlanetShipsAssessmentJob : IJobEntity, IJobEntityChunkBeginEnd
    {
        public int TotalPlanetsCount;
        public int PlanetShipsAssessmentsPerUpdate;
        public CachedSpatialDatabaseRO CachedSpatialDatabase;

        void Execute(Entity entity, in LocalTransform transform, ref Planet planet, in Team team,
            ref DynamicBuffer<PlanetShipsAssessment> shipsAssessmentBuffer,
            in DynamicBuffer<PlanetNetwork> planetNetworkBuffer)
        {
            // Ships assessment
            planet.ShipsAssessmentCounter -= math.min(TotalPlanetsCount, PlanetShipsAssessmentsPerUpdate);
            if (planet.ShipsAssessmentCounter < 0)
            {
                planet.ShipsAssessmentCounter += TotalPlanetsCount;

                // Clear assessment buffer
                for (int i = 0; i < shipsAssessmentBuffer.Length; i++)
                {
                    shipsAssessmentBuffer[i] = default;
                }

                // Query allied and enemy ships count
                PlanetAssessmentCollector collector = new PlanetAssessmentCollector(team.Index, shipsAssessmentBuffer);
                SpatialDatabase.QueryAABB(in CachedSpatialDatabase._SpatialDatabase,
                    in CachedSpatialDatabase._SpatialDatabaseCells, in CachedSpatialDatabase._SpatialDatabaseElements,
                    transform.Position,
                    planet.ShipsAssessmentExtents, ref collector);
            }
        }

        public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            CachedSpatialDatabase.CacheData();
            return true;
        }

        public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask,
            bool chunkWasExecuted)
        { }
    }

    [BurstCompile]
    public partial struct PlanetConversionJob : IJobEntity
    {
        public float DeltaTime;
        public EntityCommandBuffer ECB;
        [ReadOnly]
        public ComponentLookup<BuildingReference> BuildingReferenceLookup;
        [ReadOnly]
        public ComponentLookup<Building> BuildingLookup;
        
        void Execute(Entity entity, ref Planet planet, in Team team, ref DynamicBuffer<CapturingWorker> capturingWorkers, in DynamicBuffer<MoonReference> moonReferences)
        {
            // Determine the majority team using CapturingWorker array
            int majorityTeamIndex = -1;
            float majorityTeamSpeed = 0;
            
            // Determine the majority team using capturingWorkers array
            for (int i = 0; i < capturingWorkers.Length; i++)
            {
                CapturingWorker capturingWorker = capturingWorkers[i];
                if (capturingWorker.CaptureSpeed > majorityTeamSpeed)
                {
                    majorityTeamIndex = i;
                    majorityTeamSpeed = capturingWorker.CaptureSpeed;
                }
            }

            // Reset conversion when there's a team change, or no unique team
            if (majorityTeamIndex < 0 || majorityTeamIndex != planet.LastConvertingTeam)
            {
                planet.LastConvertingTeam = -1;
                planet.CaptureProgress = 0;
            }
            
            // Handle conversion if there's a single team holds majority long enough
            if (majorityTeamIndex >= 0)
            {
                planet.LastConvertingTeam = majorityTeamIndex;
                planet.CaptureProgress += DeltaTime * majorityTeamSpeed;
                
                if (planet.CaptureProgress >= planet.CaptureTime)
                {
                    planet.CaptureProgress = 0;
                    GameUtilities.SetTeam(ECB, entity, planet.LastConvertingTeam);
                    
                    // Convert buildings
                    for (int i = 0; i < moonReferences.Length; i++)
                    {
                        Entity moonEntity = moonReferences[i].Entity;
                        Entity buildingEntity = BuildingReferenceLookup[moonEntity].BuildingEntity;
                        if (BuildingLookup.HasComponent(buildingEntity))
                        {
                            GameUtilities.SetTeam(ECB, buildingEntity, planet.LastConvertingTeam);
                        }
                    }
                }
            }

            // Clear capturingWorkers buffer
            for (int i = 0; i < capturingWorkers.Length; i++)
            {
                capturingWorkers[i] = default;
            }
        }
    }

    [BurstCompile]
    public partial struct PlanetResourcesJob : IJobEntity
    {
        public float DeltaTime;
        
        void Execute(ref Planet planet)
        {
            float3 finalGenerationRate =
                planet.ResourceGenerationRate + planet.ResearchBonuses.PlanetResourceGenerationRadeAdd;
            planet.ResourceCurrentStorage =
                math.clamp(planet.ResourceCurrentStorage + (finalGenerationRate * DeltaTime), float3.zero,
                    planet.ResourceMaxStorage);
        }
    }
    
    [BurstCompile]
    public partial struct PlanetClearBuildingsDataJob : IJobEntity
    {
        private void Execute(ref Planet planet)
        {
            planet.ResearchBonuses.Reset();
        }
    }
}
