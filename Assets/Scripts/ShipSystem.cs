using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Logging;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Galaxy
{
    [BurstCompile]
    [UpdateAfter(typeof(BuildSpatialDatabaseGroup))]
    [UpdateAfter(typeof(TeamAISystem))]
    [UpdateAfter(typeof(ApplyTeamSystem))]
    [UpdateBefore(typeof(DeathSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [UpdateBefore(typeof(FinishInitializeSystem))]
    public partial struct ShipSystem : ISystem
    {
        private EntityQuery _spatialDatabasesQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _spatialDatabasesQuery = SystemAPI.QueryBuilder()
                .WithAll<SpatialDatabase, SpatialDatabaseCell, SpatialDatabaseElement>().Build();
            state.RequireForUpdate<Config>();
            state.RequireForUpdate<SpatialDatabaseSingleton>();
            state.RequireForUpdate(_spatialDatabasesQuery);
            state.RequireForUpdate<PlanetNavigationGrid>();
            state.RequireForUpdate<TeamManagerReference>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<VFXHitSparksSingleton>();
            state.RequireForUpdate<VFXThrustersSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            Config config = SystemAPI.GetSingleton<Config>();
            SpatialDatabaseSingleton spatialDatabaseSingleton = SystemAPI.GetSingleton<SpatialDatabaseSingleton>();
            VFXHitSparksSingleton vfxSparksSingleton = SystemAPI.GetSingletonRW<VFXHitSparksSingleton>().ValueRW;
            VFXThrustersSingleton vfxThrustersSingleton = SystemAPI.GetSingletonRW<VFXThrustersSingleton>().ValueRW;

            ShipInitializeJob shipInitializeJob = new ShipInitializeJob
            {
                TeamManagerLookup = SystemAPI.GetComponentLookup<TeamManager>(true),
                ThrustersManager = vfxThrustersSingleton.Manager,
            };
            state.Dependency = shipInitializeJob.Schedule(state.Dependency);
            
            FighterInitializeJob fighterInitializeJob = new FighterInitializeJob
            { };
            state.Dependency = fighterInitializeJob.ScheduleParallel(state.Dependency);

            ShipNavigationJob navigationJob = new ShipNavigationJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                PlanetGridEntity = SystemAPI.GetSingletonEntity<PlanetNavigationGrid>(),
                PlanetNavigationGridLookup = SystemAPI.GetComponentLookup<PlanetNavigationGrid>(true),
                PlanetNavigationCellLookup = SystemAPI.GetBufferLookup<PlanetNavigationCell>(true),
            };
            state.Dependency = navigationJob.ScheduleParallel(state.Dependency);

            FighterAIJob fighterAIJob = new FighterAIJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                FighterActionsLookup = SystemAPI.GetBufferLookup<FighterAction>(true),
                LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                CachedSpatialDatabase = new CachedSpatialDatabaseRO
                {
                    SpatialDatabaseEntity = spatialDatabaseSingleton.TargetablesSpatialDatabase, 
                    SpatialDatabaseLookup = SystemAPI.GetComponentLookup<SpatialDatabase>(true),
                    CellsBufferLookup = SystemAPI.GetBufferLookup<SpatialDatabaseCell>(true),
                    ElementsBufferLookup = SystemAPI.GetBufferLookup<SpatialDatabaseElement>(true),
                },
            };
            state.Dependency = fighterAIJob.ScheduleParallel(state.Dependency);
            
            WorkerAIJob workerAIJob = new WorkerAIJob
            {
                WorkerActionLookup = SystemAPI.GetBufferLookup<WorkerAction>(true),
                TeamLookup = SystemAPI.GetComponentLookup<Team>(true),
            };
            state.Dependency = workerAIJob.ScheduleParallel(state.Dependency);

            TraderAIJob traderAIJob = new TraderAIJob
            {
                TraderActionLookup = SystemAPI.GetBufferLookup<TraderAction>(true),
                TeamLookup = SystemAPI.GetComponentLookup<Team>(true),
                PlanetLookup = SystemAPI.GetComponentLookup<Planet>(true),
            };
            state.Dependency = traderAIJob.ScheduleParallel(state.Dependency);
            
            // We schedule all the single-thread jobs last
            
            FighterExecuteAttackJob executeAttackJob = new FighterExecuteAttackJob
            {
                LaserPrefab = config.LaserPrefab,
                ECB = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                HealthLookup = SystemAPI.GetComponentLookup<Health>(false),
                TeamManagerLookup = SystemAPI.GetComponentLookup<TeamManager>(true),
                HitSparksManager = vfxSparksSingleton.Manager,
            };
            state.Dependency = executeAttackJob.Schedule(state.Dependency);

            WorkerExecutePlanetCaptureJob workerExecutePlanetCaptureJob = new WorkerExecutePlanetCaptureJob
            {
                CapturingWorkerLookup = SystemAPI.GetBufferLookup<CapturingWorker>(false),
            };
            state.Dependency = workerExecutePlanetCaptureJob.Schedule(state.Dependency);

            WorkerExecuteBuildJob workerExecuteBuildJob = new WorkerExecuteBuildJob
            {
                MoonLookup = SystemAPI.GetComponentLookup<Moon>(false),
            };
            state.Dependency = workerExecuteBuildJob.Schedule(state.Dependency);

            TraderExecuteTradeJob traderExecuteTradeJob = new TraderExecuteTradeJob
            {
                PlanetLookup = SystemAPI.GetComponentLookup<Planet>(false),
            };
            state.Dependency = traderExecuteTradeJob.Schedule( state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(Initialize))]
        public partial struct ShipInitializeJob : IJobEntity
        {
            [ReadOnly]
            public ComponentLookup<TeamManager> TeamManagerLookup;
            public VFXManagerParented<VFXThrusterData> ThrustersManager;
            
            private void Execute(in Team team, ref Ship ship)
            {
                ShipData shipData = ship.ShipData.Value;

                ship.ThrusterVFXIndex = ThrustersManager.Create();
                
                if (ship.ThrusterVFXIndex >= 0 && TeamManagerLookup.TryGetComponent(team.ManagerEntity, out TeamManager teamManager))
                {
                    ThrustersManager.Datas[ship.ThrusterVFXIndex] = new VFXThrusterData
                    {
                        Color = teamManager.ThrusterColor,
                        Size = shipData.ThrusterSize,
                        Length = shipData.ThrusterLength,
                    };
                }
            }
        }

        [BurstCompile]
        [WithAll(typeof(Initialize))]
        public partial struct FighterInitializeJob : IJobEntity
        {
            private void Execute(Entity entity, ref Fighter fighter)
            {
                Random random = GameUtilities.GetDeterministicRandom(entity.Index);
                FighterData fighterData = fighter.FighterData.Value;
                fighter.DetectionTimer = random.NextFloat(0f, fighterData.ShipDetectionInterval);
            }
        }

        [BurstCompile]
        public partial struct ShipNavigationJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public float DeltaTime;
            public Entity PlanetGridEntity;
            [ReadOnly] public ComponentLookup<PlanetNavigationGrid> PlanetNavigationGridLookup;
            [ReadOnly] public BufferLookup<PlanetNavigationCell> PlanetNavigationCellLookup;

            // Cached data for the lifetime of the job
            private PlanetNavigationGrid _cachedGrid;
            [ReadOnly] private DynamicBuffer<PlanetNavigationCell> _cachedCellsBuffer;

            public void Execute(ref LocalTransform transform, ref Ship ship)
            {
                ShipData shipData = ship.ShipData.Value;

                // Handle movement/velocity towards target
                if(ship.BlockNavigation == 0 && ship.NavigationTargetEntity != Entity.Null)
                {
                    float3 vectorToTarget = ship.NavigationTargetPosition - transform.Position;

                    // Steering / Rotation
                    quaternion targetRot = quaternion.LookRotationSafe(vectorToTarget, math.up());
                    transform.Rotation = math.slerp(transform.Rotation, targetRot,
                        MathUtilities.GetSharpnessInterpolant(shipData.SteeringSharpness, DeltaTime));

                    // Acceleration
                    float trueAcceleration = shipData.Acceleration * ship.AccelerationMultiplier;
                    float trueMaxSpeed = shipData.MaxSpeed * ship.MaxSpeedMultiplier;
                    float3 forward = math.mul(transform.Rotation, math.forward());
                    ship.Velocity += forward * trueAcceleration * DeltaTime;

                    ship.Velocity = MathUtilities.ClampToMaxLength(ship.Velocity, trueMaxSpeed);
                }

                // Planet avoidance
                if (PlanetNavigationGridUtility.GetCellDataAtPosition(in _cachedGrid, in _cachedCellsBuffer,
                        transform.Position, out PlanetNavigationCell closestPlanetData))
                {
                    float distanceSqToPlanet = math.lengthsq(closestPlanetData.Position - transform.Position);
                    
                    // If closest planet is roughly within some distance threshold, start handling avoidance
                    if (distanceSqToPlanet < shipData.PlanetAvoidanceDistance * shipData.PlanetAvoidanceDistance)
                    {
                        float3 shipToPlanet = closestPlanetData.Position - transform.Position;
                        
                        // Hard collision
                        if (math.lengthsq(shipToPlanet) <=
                            closestPlanetData.Radius * closestPlanetData.Radius)
                        {
                            float3 shipToPlanetDir = math.normalizesafe(shipToPlanet);
                            ship.Velocity = MathUtilities.ProjectOnPlane(ship.Velocity, shipToPlanetDir) * math.length(ship.Velocity);
                            transform.Position = closestPlanetData.Position +
                                                 (-shipToPlanetDir * closestPlanetData.Radius);
                        }
                        // Avoidance
                        else if (ship.IgnoreAvoidance == 0)
                        {
                            if (closestPlanetData.Entity != ship.NavigationTargetEntity ||
                                shipData.ShouldAvoidTargetPlanet)
                            {
                                float3 velocityDir = math.normalizesafe(ship.Velocity);
                                float3 normalizedShipToPlanetProjectedOnVelocityDir =
                                    -math.normalizesafe(MathUtilities.ProjectOnPlane(shipToPlanet, velocityDir));
                                float3 pointOfTangencyTarget = closestPlanetData.Position +
                                                               (normalizedShipToPlanetProjectedOnVelocityDir *
                                                                closestPlanetData.Radius *
                                                                shipData.PlanetAvoidanceRelativeOffset);

                                bool wouldCollidePlanet = MathUtilities.SegmentIntersectsSphere(
                                    transform.Position,
                                    transform.Position + (velocityDir * shipData.PlanetAvoidanceDistance),
                                    closestPlanetData.Position,
                                    closestPlanetData.Radius);
                                if (wouldCollidePlanet)
                                {
                                    float3 directionToPointOfTangencyTarget =
                                        math.normalizesafe(pointOfTangencyTarget - transform.Position);
                                    quaternion targetRotation =
                                        quaternion.LookRotationSafe(directionToPointOfTangencyTarget, math.up());

                                    transform.Rotation = math.slerp(transform.Rotation, targetRotation,
                                        MathUtilities.GetSharpnessInterpolant(shipData.SteeringSharpness, DeltaTime));
                                    ship.Velocity = directionToPointOfTangencyTarget * math.length(ship.Velocity);
                                }
                            }
                        }
                    }
                }

                // Apply velocity as movement
                transform.Position += ship.Velocity * DeltaTime;
            }

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                // Get the planet navigation data only once, and cache it
                if (!_cachedCellsBuffer.IsCreated)
                {
                    _cachedGrid = PlanetNavigationGridLookup[PlanetGridEntity];
                    _cachedCellsBuffer = PlanetNavigationCellLookup[PlanetGridEntity];
                }

                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask, bool chunkWasExecuted)
            {
            }
        }

        [BurstCompile]
        [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
        public partial struct FighterAIJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public float DeltaTime;
            public CachedSpatialDatabaseRO CachedSpatialDatabase;
            [ReadOnly] public BufferLookup<FighterAction> FighterActionsLookup;
            [ReadOnly] public ComponentLookup<LocalToWorld> LocalToWorldLookup;

            // Cached data for the lifetime of the job
            [NativeDisableContainerSafetyRestriction]
            private NativeList<float> _tmpFinalImportances;

            public void Execute(Entity entity, in LocalTransform transform, ref Ship ship, in Team team, ref Fighter fighter, EnabledRefRW<ExecuteAttack> executeAttack)
            {
                if (team.IsNonNeutral())
                {
                    FighterData fighterData = fighter.FighterData.Value;

                    ship.IgnoreAvoidance = 0;
                    
                    // Update attack timer
                    if (fighter.AttackTimer > 0f)
                    {
                        fighter.AttackTimer -= DeltaTime;
                    }
                    
                    // Handle chasing and attacking target
                    if (fighter.TargetIsEnemyShip == 1)
                    {
                        if (LocalToWorldLookup.TryGetComponent(ship.NavigationTargetEntity, out LocalToWorld targetLtW))
                        {
                            // Target too far
                            float targetDistanceSq = math.distancesq(targetLtW.Position, transform.Position);
                            if (targetDistanceSq > fighterData.AttackRange * fighterData.AttackRange)
                            {
                                fighter.TargetIsEnemyShip = 0;
                                ship.NavigationTargetEntity = Entity.Null;
                            }
                            // Target in range
                            else
                            {

                                ship.IgnoreAvoidance = 1;
                                
                                // Update navigation target
                                ship.NavigationTargetPosition = targetLtW.Position;
                                
                                // Update when to fire
                                if (fighter.AttackTimer <= 0f)
                                {
                                    float3 shipToTarget = targetLtW.Position - transform.Position;
                                    float3 shipToTargetDir = math.normalizesafe(shipToTarget);
                                    float3 shipForward = math.mul(transform.Rotation, math.forward());
                    
                                    bool activettackTargetInSights =
                                        math.dot(shipForward, shipToTargetDir) > fighterData.DotProdThresholdForTargetInSights;
                                    if (activettackTargetInSights)
                                    {
                                        // Remember to execute the attack this frame
                                        executeAttack.ValueRW = true;
                                    }
                                }
                            }
                        }
                        // Target doesn't exist anymore
                        else
                        {
                            fighter.TargetIsEnemyShip = 0;
                            ship.NavigationTargetEntity = Entity.Null;
                        }
                    }
                    else
                    {
                        // Detect enemies to attack
                        fighter.DetectionTimer -= DeltaTime;
                        if (fighterData.DetectionRange > 0f && fighter.DetectionTimer <= 0f)
                        {
                            ShipQueryCollector collector =
                                new ShipQueryCollector(entity, transform.Position, team.Index);
                            SpatialDatabase.QueryAABBCellProximityOrder(in CachedSpatialDatabase._SpatialDatabase,
                                in CachedSpatialDatabase._SpatialDatabaseCells,
                                in CachedSpatialDatabase._SpatialDatabaseElements, transform.Position,
                                fighterData.DetectionRange, ref collector);

                            ship.NavigationTargetEntity = collector.ClosestEnemy.Entity;
                            fighter.TargetIsEnemyShip = 1;

                            fighter.DetectionTimer += fighterData.ShipDetectionInterval;
                        }
                        
                        // Choose a target planet to go to
                        if (fighter.TargetIsEnemyShip == 0)
                        {
                            if (FighterActionsLookup.TryGetBuffer(team.ManagerEntity,
                                    out DynamicBuffer<FighterAction> fighterActionsBuffer))
                            {
                                ShipData shipData = ship.ShipData.Value;

                                _tmpFinalImportances.Clear();
                                float importancesTotal = 0f;
                                float currentActionImportance = -1f;

                                for (int i = 0; i < fighterActionsBuffer.Length; i++)
                                {
                                    FighterAction fitghterAction = fighterActionsBuffer[i];

                                    float proximityImportance = GameUtilities.CalculateProximityImportance(
                                        transform.Position, fitghterAction.Position,
                                        shipData.MaxDistanceSqForPlanetProximityImportanceScaling,
                                        shipData.PlanetProximityImportanceRemap);
                                    float finalImportance = fitghterAction.Importance * proximityImportance;

                                    _tmpFinalImportances.Add(finalImportance);
                                    importancesTotal += finalImportance;
                                    
                                    // Try find the current action's new importance
                                    if (currentActionImportance < 0f && fitghterAction.Entity == ship.NavigationTargetEntity)
                                    {
                                        currentActionImportance = fitghterAction.Importance;
                                    }
                                }

                                // If couldn't find current action, clear data
                                if (currentActionImportance < 0f)
                                {
                                    ship.NavigationTargetEntity = Entity.Null;
                                }
                                
                                // Get a random that is unique to this combo of entities, so AI decisions have some
                                // stability from frame to frame
                                Random persistentRandom = GameUtilities.GetDeterministicRandom(entity.Index);

                                // Pick planet based on weighted random
                                int weightedRandomIndex = GameUtilities.GetWeightedRandomIndex(importancesTotal,
                                    in _tmpFinalImportances, ref persistentRandom);
                                if (weightedRandomIndex >= 0)
                                {
                                    FighterAction fighterAction = fighterActionsBuffer[weightedRandomIndex];

                                    // Only pick a different action if the new action is significantly more important
                                    if (_tmpFinalImportances[weightedRandomIndex] > currentActionImportance * 2f)
                                    {
                                        ship.NavigationTargetEntity = fighterAction.Entity;
                                        ship.NavigationTargetPosition = fighterAction.Position;
                                        ship.NavigationTargetRadius = fighterAction.Radius;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                CachedSpatialDatabase.CacheData();
                if (!_tmpFinalImportances.IsCreated)
                {
                    _tmpFinalImportances = new NativeList<float>(64, Allocator.Temp);
                }

                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask, bool chunkWasExecuted)
            {
            }
        }

        [BurstCompile]
        [WithAll(typeof(Team))]
        [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
        public partial struct WorkerAIJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            [ReadOnly] public BufferLookup<WorkerAction> WorkerActionLookup;
            [ReadOnly] public ComponentLookup<Team> TeamLookup;
            
            // Cached data for the lifetime of the job
            [NativeDisableContainerSafetyRestriction]
            private NativeList<float> _tmpFinalImportances;

            public void Execute(Entity entity, ref Worker worker, in LocalTransform transform, ref Ship ship,
                EnabledRefRW<ExecutePlanetCapture> executePlanetCapture, EnabledRefRW<ExecuteBuild> executeBuild)
            {
                ship.BlockNavigation = 0;
                
                Team team = TeamLookup[entity];
                if (team.IsNonNeutral())
                {
                    WorkerData workerData = worker.WorkerData.Value;
                    
                    // Handle reaching targets
                    if (ship.NavigationTargetEntity != Entity.Null)
                    {
                        // Construct building
                        if (worker.DesiredBuildingPrefab != Entity.Null)
                        {
                            float requiredRange = workerData.BuildRange + ship.NavigationTargetRadius;
                            if (math.distancesq(transform.Position, ship.NavigationTargetPosition) < requiredRange * requiredRange)
                            {
                                executeBuild.ValueRW = true;
                            }
                        }
                        // Capture planet
                        else
                        {
                            float requiredRange = workerData.CaptureRange + ship.NavigationTargetRadius;
                            if (math.distancesq(transform.Position, ship.NavigationTargetPosition) <
                                requiredRange * requiredRange)
                            {
                                // Only capture if it's not already captured
                                if (TeamLookup[ship.NavigationTargetEntity].Index != team.Index)
                                {
                                    executePlanetCapture.ValueRW = true;
                                }
                            }
                        }
                    }

                    // Choose an action (capture planet, construct building)
                    if (WorkerActionLookup.TryGetBuffer(team.ManagerEntity,
                            out DynamicBuffer<WorkerAction> workerActionsBuffer))
                    {
                        ShipData shipData = ship.ShipData.Value;
                        Random persistentRandom = GameUtilities.GetDeterministicRandom(entity.Index);
                        
                        // Clear data
                        _tmpFinalImportances.Clear();
                        float importancesTotal = 0f;
                        float currentActionImportance = -1f;
                        
                        for (int i = 0; i < workerActionsBuffer.Length; i++)
                        {
                            WorkerAction workerAction = workerActionsBuffer[i];

                            float proximityImportance = GameUtilities.CalculateProximityImportance(transform.Position,
                                workerAction.Position, shipData.MaxDistanceSqForPlanetProximityImportanceScaling,
                                shipData.PlanetProximityImportanceRemap);
                            float finalImportance = workerAction.Importance * proximityImportance;

                            _tmpFinalImportances.Add(finalImportance);
                            importancesTotal += finalImportance;
                                    
                            // Try find the current action's new importance
                            if (currentActionImportance < 0f && workerAction.Entity == ship.NavigationTargetEntity)
                            {
                                currentActionImportance = workerAction.Importance;
                            }
                        }

                        // If couldn't find current action, clear data
                        if (currentActionImportance < 0f)
                        {
                            worker.DesiredBuildingPrefab = Entity.Null;
                            ship.NavigationTargetEntity = Entity.Null;
                        }

                        int weightedRandomIndex = GameUtilities.GetWeightedRandomIndex(importancesTotal,
                            in _tmpFinalImportances, ref persistentRandom);
                        if (weightedRandomIndex >= 0)
                        {
                            WorkerAction workerAction = workerActionsBuffer[weightedRandomIndex];

                            // Only pick a different action if the new action is significantly more important
                            if (_tmpFinalImportances[weightedRandomIndex] > currentActionImportance * 2f)
                            {
                                ship.NavigationTargetEntity = workerAction.Entity;
                                ship.NavigationTargetPosition = workerAction.Position;
                                ship.NavigationTargetRadius = workerAction.PlanetRadius;

                                // Build action
                                if (workerAction.Type == 1)
                                {
                                    worker.DesiredBuildingPrefab = workerAction.BuildingPrefab;
                                }
                            }
                        }
                    }
                }
            }

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (!_tmpFinalImportances.IsCreated)
                {
                    _tmpFinalImportances = new NativeList<float>(64, Allocator.Temp);
                }

                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask, bool chunkWasExecuted)
            {
            }
        }

        [BurstCompile]
        [WithAll(typeof(ExecutePlanetCapture))]
        public partial struct WorkerExecutePlanetCaptureJob : IJobEntity
        {
            public BufferLookup<CapturingWorker> CapturingWorkerLookup;

            public void Execute(ref Worker worker, in Team team, ref Ship ship,
                EnabledRefRW<ExecutePlanetCapture> executePlanetCapture)
            {
                executePlanetCapture.ValueRW = false;

                WorkerData workerData = worker.WorkerData.Value;

                ship.BlockNavigation = 1;
                ship.Velocity = 0;
                
                if (CapturingWorkerLookup.TryGetBuffer(ship.NavigationTargetEntity,
                        out DynamicBuffer<CapturingWorker> capturingWorkers))
                {
                    CapturingWorker currentWorker = capturingWorkers[team.Index];
                    currentWorker.CaptureSpeed += workerData.CaptureSpeed;
                    capturingWorkers[team.Index] = currentWorker;
                }
            }
        }

        [BurstCompile]
        [WithAll(typeof(ExecuteBuild))]
        public partial struct WorkerExecuteBuildJob : IJobEntity
        {
            public ComponentLookup<Moon> MoonLookup;

            public void Execute(ref Worker worker, ref Ship ship, EnabledRefRW<ExecuteBuild> executeBuild)
            {
                executeBuild.ValueRW = false;

                WorkerData workerData = worker.WorkerData.Value;

                ship.BlockNavigation = 1;
                ship.Velocity = 0;
                
                if (MoonLookup.TryGetComponent(ship.NavigationTargetEntity, out Moon moon))
                {
                    // Detect starting a new construction
                    if (moon.BuiltPrefab == Entity.Null)
                    {
                        moon.BuiltPrefab = worker.DesiredBuildingPrefab;
                    }

                    moon.CummulativeBuildSpeed += workerData.BuildSpeed;
                    MoonLookup[ship.NavigationTargetEntity] = moon;
                }
            }
        }

        [BurstCompile]
        [WithAll(typeof(Team))]
        [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
        public partial struct TraderAIJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            [ReadOnly] 
            public BufferLookup<TraderAction> TraderActionLookup;
            [ReadOnly] 
            public ComponentLookup<Team> TeamLookup;
            [ReadOnly] 
            public ComponentLookup<Planet> PlanetLookup;

            // Cached data for the lifetime of the job
            [NativeDisableContainerSafetyRestriction]
            private NativeList<float3> _tmpFinalImportancesVector;
            [NativeDisableContainerSafetyRestriction]
            private NativeList<float> _tmpFinalImportances;

            public void Execute(Entity entity, in LocalTransform transform, ref Ship ship, ref Trader trader, EnabledRefRW<ExecuteTrade> executeTrade)
            {
                Team team = TeamLookup[entity];
                if (team.IsNonNeutral())
                {
                    TraderData traderData = trader.TraderData.Value;

                    // Idle
                    if (ship.NavigationTargetEntity == Entity.Null)
                    {
                        ship.Velocity = float3.zero; // TODO;
                        ship.BlockNavigation = 1;

                        FindNewTradeRoute(entity, team, ref trader, ref ship);
                    }
                    else
                    {
                        ship.BlockNavigation = 0;
                        Team activePlanetTeam = TeamLookup[ship.NavigationTargetEntity];

                        // Handle team changes mid-trade
                        if (activePlanetTeam.Index != team.Index)
                        {
                            // If going to receiving planet and its team changes, just offload resources at nearest
                            // planet
                            if (ship.NavigationTargetEntity == trader.ReceivingPlanetEntity)
                            {
                                SetNearestPlanetAsActiveAndReceiving(team, ref trader, ref ship, in transform);
                            }
                            // If going to giving planet and its team changes, cancel current trade
                            else
                            {
                                ResetTrade(ref ship, ref trader);
                            }
                        }
                        else if (PlanetLookup.TryGetComponent(ship.NavigationTargetEntity, out Planet planet))
                        {
                            // Detect reaching planet
                            float requiredRange = traderData.ResourceExchangeRange + ship.NavigationTargetRadius;
                            if (math.distancesq(transform.Position, ship.NavigationTargetPosition) <=
                                requiredRange * requiredRange)
                            {
                                executeTrade.ValueRW = true;
                            }
                        }
                    }
                }
            }

            private void FindNewTradeRoute(Entity entity, Team team, ref Trader trader, ref Ship ship)
            {
                Random persistentRandom = GameUtilities.GetDeterministicRandom(entity.Index, trader.FindTradeRouteAttempts);

                // Get this ship's team manager data
                if (TraderActionLookup.TryGetBuffer(team.ManagerEntity,
                        out DynamicBuffer<TraderAction> traderActionsBuffer))
                {
                    ship.NavigationTargetEntity = Entity.Null;

                    float3 receivingPlanetStorageRatioPercentile = float3.zero;

                    // Find receiving planet
                    {
                        _tmpFinalImportancesVector.Clear();
                        float3 importancesTotalVector = 0f;

                        for (int i = 0; i < traderActionsBuffer.Length; i++)
                        {
                            TraderAction traderAction = traderActionsBuffer[i];

                            // Determine a resource need importance based on how this planet's resource storage compares
                            // to the best storages among the planets we have
                            float3 resourceNeedImportance =
                                math.saturate(new float3(1f) - traderAction.ResourceStorageRatioPercentile);
                            float3 finalImportance = traderAction.ImportanceBias * resourceNeedImportance;

                            _tmpFinalImportancesVector.Add(finalImportance);
                            importancesTotalVector += finalImportance;
                        }

                        // Pick a receiving planet based on weighted random of needs
                        int weightedRandomIndex = GameUtilities.GetWeightedRandomIndex(importancesTotalVector,
                            in _tmpFinalImportancesVector, ref persistentRandom, out int subIndex);
                        if (weightedRandomIndex >= 0)
                        {
                            TraderAction traderAction = traderActionsBuffer[weightedRandomIndex];

                            trader.ReceivingPlanetEntity = traderAction.Entity;
                            trader.ReceivingPlanetPosition = traderAction.Position;
                            trader.ReceivingPlanetRadius = traderAction.Radius;

                            receivingPlanetStorageRatioPercentile = traderAction.ResourceStorageRatioPercentile;
                            switch (subIndex)
                            {
                                case 0:
                                    trader.ChosenResourceMask = new float3(1f, 0f, 0f);
                                    break;
                                case 1:
                                    trader.ChosenResourceMask = new float3(0f, 1f, 0f);
                                    break;
                                case 2:
                                    trader.ChosenResourceMask = new float3(0f, 0f, 1f);
                                    break;
                            }
                        }
                    }

                    // Find giving planet
                    {
                        _tmpFinalImportances.Clear();
                        float importancesTotal = 0f;

                        float receiverStorageRatio =
                            math.csum(trader.ChosenResourceMask * receivingPlanetStorageRatioPercentile);

                        for (int i = 0; i < traderActionsBuffer.Length; i++)
                        {
                            TraderAction traderAction = traderActionsBuffer[i];

                            // Only consider planets that have a greater storage ratio for the chosen resource than the
                            // receiving planet
                            float finalImportance = 0f;
                            float giverStorageRatio = math.csum(trader.ChosenResourceMask *
                                                           traderAction.ResourceStorageRatioPercentile);
                            if (giverStorageRatio > receiverStorageRatio)
                            {
                                float resourceGivingImportance = giverStorageRatio - receiverStorageRatio;
                                finalImportance = traderAction.ImportanceBias * resourceGivingImportance;
                            }

                            _tmpFinalImportances.Add(finalImportance);
                            importancesTotal += finalImportance;
                        }

                        // Pick random giving planet 
                        int weightedRandomIndex = GameUtilities.GetWeightedRandomIndex(importancesTotal,
                            in _tmpFinalImportances, ref persistentRandom);
                        if (weightedRandomIndex >= 0)
                        {
                            TraderAction traderAction = traderActionsBuffer[weightedRandomIndex];
                            ship.NavigationTargetEntity = traderAction.Entity;
                            ship.NavigationTargetPosition = traderAction.Position;
                            ship.NavigationTargetRadius = traderAction.Radius;
                        }
                        else
                        {
                            trader.FindTradeRouteAttempts++;
                        }
                    }
                }
            }

            private void SetNearestPlanetAsActiveAndReceiving(Team team, ref Trader trader, ref Ship ship, in LocalTransform transform)
            {
                // Get this ship's team manager data
                if (TraderActionLookup.TryGetBuffer(team.ManagerEntity,
                        out DynamicBuffer<TraderAction> traderActionsBuffer))
                {
                    Entity closestPlanetEntity = Entity.Null;
                    float3 closestPlanetPosition = default;
                    float closestPlanetRadius = default;
                    float closestPlanetDistanceSq = float.MaxValue;
                    for (int i = 0; i < traderActionsBuffer.Length; i++)
                    {
                        TraderAction traderAction = traderActionsBuffer[i];
                        float distSq = math.distancesq(traderAction.Position, transform.Position);
                        if (distSq < closestPlanetDistanceSq)
                        {
                            closestPlanetEntity = traderAction.Entity;
                            closestPlanetPosition = traderAction.Position;
                            closestPlanetRadius = traderAction.Radius;

                            closestPlanetDistanceSq = distSq;
                        }
                    }

                    if (closestPlanetEntity != Entity.Null)
                    {
                        ship.NavigationTargetEntity = closestPlanetEntity;
                        ship.NavigationTargetPosition = closestPlanetPosition;
                        ship.NavigationTargetRadius = closestPlanetRadius;

                        trader.ReceivingPlanetEntity = closestPlanetEntity;
                        trader.ReceivingPlanetPosition = closestPlanetPosition;
                        trader.ReceivingPlanetRadius = closestPlanetRadius;
                    }
                    else
                    {
                        ResetTrade(ref ship, ref trader);
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ResetTrade(ref Ship ship, ref Trader trader)
            {
                ship.NavigationTargetEntity = Entity.Null;
                ship.NavigationTargetPosition = default;
                ship.NavigationTargetRadius = default;
                    
                trader.ReceivingPlanetEntity = Entity.Null;
                trader.ReceivingPlanetPosition = default;
                trader.ReceivingPlanetRadius = default;
                trader.FindTradeRouteAttempts = 0;
            }
            
            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (!_tmpFinalImportancesVector.IsCreated)
                {
                    _tmpFinalImportancesVector = new NativeList<float3>(64, Allocator.Temp);
                }
                if (!_tmpFinalImportances.IsCreated)
                {
                    _tmpFinalImportances = new NativeList<float>(64, Allocator.Temp);
                }

                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask, bool chunkWasExecuted)
            {
            }
        }


        [BurstCompile]
        [WithAll(typeof(Team))]
        [WithAll(typeof(ExecuteTrade))]
        public partial struct TraderExecuteTradeJob : IJobEntity
        {
            public ComponentLookup<Planet> PlanetLookup;

            public void Execute(ref Ship ship, ref Trader trader, EnabledRefRW<ExecuteTrade> executeTrade)
            {
                executeTrade.ValueRW = false;

                if (PlanetLookup.TryGetComponent(ship.NavigationTargetEntity, out Planet planet))
                {
                    TraderData traderData = trader.TraderData.Value;

                    // If arrived at receiving planet, offload carried resources and reset
                    if (ship.NavigationTargetEntity == trader.ReceivingPlanetEntity)
                    {
                        ship.Velocity = float3.zero; // TODO;
                        ship.BlockNavigation = 1;

                        planet.ResourceCurrentStorage = math.clamp(
                            planet.ResourceCurrentStorage + trader.CarriedResources, float3.zero,
                            planet.ResourceMaxStorage);
                        trader.CarriedResources = float3.zero;

                        PlanetLookup[ship.NavigationTargetEntity] = planet;
                        ResetTrade(ref ship, ref trader);
                    }
                    // If arrived at giving planet, take resources
                    else
                    {
                        ship.Velocity = float3.zero; // TODO;
                        ship.BlockNavigation = 1;

                        float3 takenResources =
                            trader.ChosenResourceMask * traderData.ResourceCarryCapacity;
                        takenResources = math.min(takenResources, planet.ResourceCurrentStorage);
                        planet.ResourceCurrentStorage = math.clamp(
                            planet.ResourceCurrentStorage - takenResources, float3.zero,
                            planet.ResourceMaxStorage);
                        trader.CarriedResources += takenResources;
                        PlanetLookup[ship.NavigationTargetEntity] = planet;

                        ship.NavigationTargetEntity = trader.ReceivingPlanetEntity;
                        ship.NavigationTargetPosition = trader.ReceivingPlanetPosition;
                        ship.NavigationTargetRadius = trader.ReceivingPlanetRadius;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ResetTrade(ref Ship ship, ref Trader trader)
            {
                ship.NavigationTargetEntity = Entity.Null;
                ship.NavigationTargetPosition = default;
                ship.NavigationTargetRadius = default;
                    
                trader.ReceivingPlanetEntity = Entity.Null;
                trader.ReceivingPlanetPosition = default;
                trader.ReceivingPlanetRadius = default;
                trader.FindTradeRouteAttempts = 0;
            }
        }

        [BurstCompile]
        [WithAll(typeof(ExecuteAttack))]
        public partial struct FighterExecuteAttackJob : IJobEntity
        {
            public Entity LaserPrefab;
            public EntityCommandBuffer ECB;
            public ComponentLookup<Health> HealthLookup;
            [ReadOnly] public ComponentLookup<TeamManager> TeamManagerLookup;

            public VFXManager<VFXHitSparksRequest> HitSparksManager;

            public void Execute(in Ship ship, ref Fighter fighter, in Team team, in LocalTransform transform,
                EnabledRefRW<ExecuteAttack> executeAttack)
            {
                executeAttack.ValueRW = false;

                FighterData fighterData = fighter.FighterData.Value;

                if (ship.NavigationTargetEntity != Entity.Null &&
                    HealthLookup.TryGetComponent(ship.NavigationTargetEntity, out Health enemyHealth))
                {
                    GameUtilities.ApplyDamage(ref enemyHealth, fighterData.AttackDamage * fighter.DamageMultiplier);
                    HealthLookup[ship.NavigationTargetEntity] = enemyHealth;
                }

                float3 shipToTarget = ship.NavigationTargetPosition - transform.Position;
                float3 shipToTargetDir = math.normalizesafe(shipToTarget);

                if (TeamManagerLookup.TryGetComponent(team.ManagerEntity, out TeamManager teamManager))
                {
                    // Spawn laser
                    GameUtilities.SpawnLaser(ECB, LaserPrefab, teamManager.LaserColor, transform.Position, shipToTargetDir,
                        math.length(shipToTarget));

                    // Hit sparks
                    HitSparksManager.AddRequest(new VFXHitSparksRequest
                    {
                        Position = ship.NavigationTargetPosition,
                        Color = teamManager.LaserSparksColor,
                    });
                }

                fighter.AttackTimer = fighterData.AttackDelay;
            }
        }
    }
}

[BurstCompile]
[UpdateAfter(typeof(TransformSystemGroup))]
public partial struct ShipPostTransformsSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<VFXThrustersSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        VFXThrustersSingleton vfxThrustersSingleton = SystemAPI.GetSingletonRW<VFXThrustersSingleton>().ValueRW;

        ShipSetVFXDataJob shipSetVFXDataJob = new ShipSetVFXDataJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            ThrustersData = vfxThrustersSingleton.Manager.Datas,
        };
        state.Dependency = shipSetVFXDataJob.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct ShipSetVFXDataJob : IJobEntity
    {
        public float DeltaTime;
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<VFXThrusterData> ThrustersData;
            
        private void Execute(in LocalTransform transform, in Ship ship)
        {
            if (ship.ThrusterVFXIndex >= 0)
            {
                ref ShipData shipData = ref ship.ShipData.Value;
                    
                VFXThrusterData thrusterData = ThrustersData[ship.ThrusterVFXIndex];
                thrusterData.Position =
                    transform.Position + math.mul(transform.Rotation, shipData.ThrusterLocalPosition);
                thrusterData.Direction = math.mul(transform.Rotation, -math.forward());
                ThrustersData[ship.ThrusterVFXIndex] = thrusterData;
            }
        }
    }
}
