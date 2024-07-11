using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

[BurstCompile]
[UpdateAfter(typeof(BeginSimulationMainThreadGroup))]
[UpdateAfter(typeof(BuildSpatialDatabaseGroup))]
[UpdateAfter(typeof(PlanetSystem))]
[UpdateBefore(typeof(DeathSystem))]
[UpdateBefore(typeof(FinishInitializeSystem))]
public partial struct BuildingSystem : ISystem
{
    private EntityQuery _shipsQuery;
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Config>();
        state.RequireForUpdate<SpatialDatabaseSingleton>();
        state.RequireForUpdate<ShipCollection>();
        state.RequireForUpdate<BuildingCollection>();
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<TeamManagerReference>();
        state.RequireForUpdate<VFXHitSparksSingleton>();
        _shipsQuery = SystemAPI.QueryBuilder().WithAll<Ship>().Build();
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityQuery aliveTeamsQuery = SystemAPI.QueryBuilder().WithAll<TeamManager>().Build();
        
        Config config = SystemAPI.GetSingleton<Config>();
        SpatialDatabaseSingleton spatialDatabaseSingleton = SystemAPI.GetSingleton<SpatialDatabaseSingleton>();
        VFXHitSparksSingleton vfxSparksSingleton = SystemAPI.GetSingletonRW<VFXHitSparksSingleton>().ValueRW;
        
        TurretInitializeJob turretInitializeJob = new TurretInitializeJob
        { };
        state.Dependency = turretInitializeJob.ScheduleParallel(state.Dependency);
        
        BuildingInitializeJob buildingInitializeJob = new BuildingInitializeJob
        { };
        state.Dependency = buildingInitializeJob.ScheduleParallel(state.Dependency);
        
        BuildingConstructionJob buildingConstructionJob = new BuildingConstructionJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
            BuildingLookup = SystemAPI.GetComponentLookup<Building>(true),
            TeamLookup = SystemAPI.GetComponentLookup<Team>(true),
        };
        state.Dependency = buildingConstructionJob.Schedule(state.Dependency);
       
        TurretUpdateAttackJob turretUpdateAttackJob = new TurretUpdateAttackJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            CachedSpatialDatabase = new CachedSpatialDatabaseRO
            {
                SpatialDatabaseEntity = spatialDatabaseSingleton.TargetablesSpatialDatabase, 
                SpatialDatabaseLookup = SystemAPI.GetComponentLookup<SpatialDatabase>(true),
                CellsBufferLookup = SystemAPI.GetBufferLookup<SpatialDatabaseCell>(true),
                ElementsBufferLookup = SystemAPI.GetBufferLookup<SpatialDatabaseElement>(true),
            },
            LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
            ParentLookup = SystemAPI.GetComponentLookup<Parent>(true),
        };
        state.Dependency = turretUpdateAttackJob.ScheduleParallel(state.Dependency);
       
        TurretExecuteAttack turretExecuteAttack = new TurretExecuteAttack
        {
            LaserPrefab = config.LaserPrefab,
            ECB = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged),
            HealthLookup = SystemAPI.GetComponentLookup<Health>(false),
            TeamManagerLookup = SystemAPI.GetComponentLookup<TeamManager>(true),
            HitSparksManager = vfxSparksSingleton.Manager,
            PlanetLookup = SystemAPI.GetComponentLookup<Planet>(false),
            BuildingLookup = SystemAPI.GetComponentLookup<Building>(true),
        };
        state.Dependency = turretExecuteAttack.Schedule(state.Dependency);
        
        ResearchApplyToPlanetJob researchApplyToPlanetJob = new ResearchApplyToPlanetJob
        {
            PlanetLookup = SystemAPI.GetComponentLookup<Planet>(false),
        };
        state.Dependency = researchApplyToPlanetJob.Schedule(state.Dependency);

        if (aliveTeamsQuery.CalculateEntityCount() > 1)
        {
            FactoryJob factoryJob = new FactoryJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                IsOverGlobalShipsLimit = _shipsQuery.CalculateEntityCount() < config.MaxTotalShips,
                MaxShipsPerTeam = config.MaxShipsPerTeam,
                ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged),
                FactoryActionsLookup = SystemAPI.GetBufferLookup<FactoryAction>(true),
                LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                HealthLookup = SystemAPI.GetComponentLookup<Health>(true),
                ShipLookup = SystemAPI.GetComponentLookup<Ship>(true),
                FighterLookup = SystemAPI.GetComponentLookup<Fighter>(true),
                PlanetLookup = SystemAPI.GetComponentLookup<Planet>(false),
                TeamManagerAILookup = SystemAPI.GetComponentLookup<TeamManagerAI>(true),
            };
            state.Dependency = factoryJob.Schedule(state.Dependency);
        }
    }

    [BurstCompile]
    [WithAll(typeof(Initialize))]
    public partial struct TurretInitializeJob : IJobEntity
    {
        private void Execute(Entity entity, ref Turret turret)
        {
            Random random = GameUtilities.GetDeterministicRandom(entity.Index);
            TurretData turretData = turret.TurretData.Value;
            turret.DetectionTimer = random.NextFloat(0f, turretData.ShipDetectionInterval);
        }
    }

    [BurstCompile]
    [WithAll(typeof(Initialize))]
    public partial struct BuildingInitializeJob : IJobEntity
    {
        private void Execute(Entity entity, ref Building building)
        {
            building.Random = GameUtilities.GetDeterministicRandom(entity.Index);
        }
    }

    [BurstCompile]
    [WithAll(typeof(BuildingReference))]
    public partial struct BuildingConstructionJob : IJobEntity
    {
        public float DeltaTime;
        public EntityCommandBuffer ECB;
        [ReadOnly] public ComponentLookup<Building> BuildingLookup;
        [ReadOnly] public ComponentLookup<Team> TeamLookup;
        
        void Execute(Entity entity, ref Moon moon, in BuildingReference buildingReference)
        {
            Team team = TeamLookup[moon.PlanetEntity];

            // Clear build if team change
            if (team.Index != moon.PreviousTeam.Index)
            {
                ClearBuild(ref moon);
            }
            
            // Handle construction
            if (moon.BuiltPrefab != Entity.Null)
            {
                // Clear build if there's already a building there
                if (buildingReference.BuildingEntity != Entity.Null)
                {
                    ClearBuild(ref moon);
                }

                if (BuildingLookup.TryGetComponent(moon.BuiltPrefab, out Building prefabBuilding))
                {
                    moon.BuildProgress += moon.CummulativeBuildSpeed * DeltaTime;
                    if (moon.BuildProgress >= prefabBuilding.BuildingData.Value.BuildTime)
                    {
                        GameUtilities.CreateBuilding(ECB, moon.BuiltPrefab, entity, moon.PlanetEntity, team.Index,
                            in BuildingLookup);

                        ClearBuild(ref moon);
                    }
                }
                else
                {
                    ClearBuild(ref moon);
                }
            }
            else
            {
                ClearBuild(ref moon);
            }

            // Clear workers speed
            moon.CummulativeBuildSpeed = 0f;

            moon.PreviousTeam = team;
        }

        private void ClearBuild(ref Moon moon)
        {
            moon.BuiltPrefab = Entity.Null;
            moon.BuildProgress = 0f;
            moon.CummulativeBuildSpeed = 0f;
        }
    }

    [BurstCompile]
    [WithAll(typeof(LocalToWorld))]
    public partial struct TurretUpdateAttackJob : IJobEntity, IJobEntityChunkBeginEnd
    {
        public float DeltaTime;
        public CachedSpatialDatabaseRO CachedSpatialDatabase;
        [ReadOnly]
        public ComponentLookup<LocalToWorld> LocalToWorldLookup;
        [ReadOnly]
        public ComponentLookup<Parent> ParentLookup;
        
        private void Execute(Entity entity, ref Turret turret, ref LocalTransform localTransform, in Team team, in LocalToWorld turretLTW)
        {
            TurretData turretData = turret.TurretData.Value;
            
            // Check if target still exists
            if (turret.ActiveTarget != Entity.Null)
            {
                if (LocalToWorldLookup.TryGetComponent(turret.ActiveTarget, out LocalToWorld activeTargetLTW))
                {
                    // Check if target in range
                    float targetDistance = math.distance(turretLTW.Position, activeTargetLTW.Position);
                    if (targetDistance > turretData.AttackRange)
                    {
                        // Disengage
                        turret.ActiveTarget = Entity.Null;
                    }
                    else
                    {
                        // Store new position
                        turret.ActiveTargetPosition = activeTargetLTW.Position;
                    }
                }
                else
                {
                    // Disengage
                    turret.ActiveTarget = Entity.Null;
                }
            }
            
            // Detect enemies to attack
            if (team.IsNonNeutral())
            {
                turret.DetectionTimer -= DeltaTime;
                if (turret.DetectionTimer <= 0f)
                {
                    if (turret.ActiveTarget == Entity.Null)
                    {
                        ShipQueryCollector collector = new ShipQueryCollector(entity, turretLTW.Position, team.Index);
                        SpatialDatabase.QueryAABBCellProximityOrder(in CachedSpatialDatabase._SpatialDatabase,
                            in CachedSpatialDatabase._SpatialDatabaseCells,
                            in CachedSpatialDatabase._SpatialDatabaseElements, turretLTW.Position,
                            turretData.AttackRange, ref collector);

                        turret.ActiveTarget = collector.ClosestEnemy.Entity;
                        turret.ActiveTargetPosition = collector.ClosestEnemy.Position;
                    }

                    turret.DetectionTimer += turretData.ShipDetectionInterval;
                }
            }

            // Attack timer
            if (turret.AttackTimer > 0f)
            {
                turret.AttackTimer -= DeltaTime;
            }
            
            if (turret.ActiveTarget != Entity.Null)
            {
                // Rotate towards target
                quaternion turretWorldRotation = turretLTW.Rotation;
                float3 directionToTarget = math.normalizesafe(turret.ActiveTargetPosition - turretLTW.Position);
                quaternion rotationToTarget = quaternion.LookRotationSafe(directionToTarget, math.up());
                turretWorldRotation = math.slerp(turretWorldRotation, rotationToTarget,
                    MathUtilities.GetSharpnessInterpolant(turretData.RotationSharpness, DeltaTime));
                MathUtilities.GetLocalRotationForWorldRotation(entity, out quaternion selfLocalRotation,
                    turretWorldRotation, ref LocalToWorldLookup, ref ParentLookup);
                localTransform.Rotation = selfLocalRotation;
                
                // Update attack
                if (turret.AttackTimer <= 0f)
                {
                    turret.MustAttack = 1;
                }
            }
        }

        public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            CachedSpatialDatabase.CacheData();
            return true;
        }

        public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask, bool chunkWasExecuted)
        {
        }
    }

    [BurstCompile]
    public partial struct TurretExecuteAttack : IJobEntity
    {
        public Entity LaserPrefab;
        public EntityCommandBuffer ECB;
        public ComponentLookup<Health> HealthLookup;
        public ComponentLookup<Planet> PlanetLookup;
        [ReadOnly]
        public ComponentLookup<Building> BuildingLookup;
        [ReadOnly] public ComponentLookup<TeamManager> TeamManagerLookup;
        public VFXManager<VFXHitSparksRequest> HitSparksManager;

        private void Execute(Entity entity, ref Turret turret, in Team team, in LocalToWorld ltw)
        {
            if (turret.MustAttack == 1)
            {
                turret.MustAttack = 0;

                bool canFire = false;
                if (BuildingLookup.TryGetComponent(entity, out Building building) &&
                    PlanetLookup.TryGetComponent(building.PlanetEntity, out Planet planet))
                {
                    if (GameUtilities.TryConsumeResources(turret.TurretData.Value.ResourceCost, ref planet))
                    {
                        canFire = true;
                        PlanetLookup[building.PlanetEntity] = planet;
                    }
                }
                else
                {
                    canFire = true; // true by default for turrets on ships
                }

                if (canFire)
                {
                    TurretData turretData = turret.TurretData.Value;

                    if (turret.ActiveTarget != Entity.Null &&
                        HealthLookup.TryGetComponent(turret.ActiveTarget, out Health enemyHealth))
                    {
                        GameUtilities.ApplyDamage(ref enemyHealth, turretData.AttackDamage);
                        HealthLookup[turret.ActiveTarget] = enemyHealth;
                    }

                    float3 selfToTarget = turret.ActiveTargetPosition - ltw.Position;
                    float3 selfToTargetDir = math.normalizesafe(selfToTarget);

                    if (TeamManagerLookup.TryGetComponent(team.ManagerEntity, out TeamManager teamManager))
                    {
                        // Spawn laser
                        GameUtilities.SpawnLaser(ECB, LaserPrefab, teamManager.LaserColor, ltw.Position,
                            selfToTargetDir,
                            math.length(selfToTarget));

                        // Hit sparks
                        HitSparksManager.AddRequest(new VFXHitSparksRequest
                        {
                            Position = turret.ActiveTargetPosition,
                            Color = teamManager.LaserSparksColor,
                        });
                    }

                    turret.AttackTimer = turretData.AttackDelay;
                }
            }
        }
    }
    
    [BurstCompile]
    public partial struct ResearchApplyToPlanetJob : IJobEntity
    {
        public float DeltaTime;
        public ComponentLookup<Planet> PlanetLookup;
        
        private void Execute(ref Research research, in Building building)
        {
            if (PlanetLookup.HasComponent(building.PlanetEntity))
            {
                ResearchData researchData = research.ResearchData.Value;
                ref Planet planet = ref PlanetLookup.GetRefRW(building.PlanetEntity).ValueRW;

                float3 consumedResources = researchData.ResourcesConsumptionRate * DeltaTime;
                if (GameUtilities.TryConsumeResources(consumedResources, ref planet))
                {
                    // Apply bonuses
                    planet.ResearchBonuses.Add(researchData.ResearchBonuses);
                }
            }
        }
    }

    [BurstCompile]
    public partial struct FactoryJob : IJobEntity, IJobEntityChunkBeginEnd
    {
        public float DeltaTime;
        public bool IsOverGlobalShipsLimit;
        public int MaxShipsPerTeam;
        public EntityCommandBuffer ECB;
        [ReadOnly] 
        public BufferLookup<FactoryAction> FactoryActionsLookup;
        [ReadOnly]
        public ComponentLookup<LocalToWorld> LocalToWorldLookup;
        [ReadOnly]
        public ComponentLookup<Health> HealthLookup;
        [ReadOnly]
        public ComponentLookup<Ship> ShipLookup;
        [ReadOnly]
        public ComponentLookup<Fighter> FighterLookup;
        [ReadOnly] 
        public ComponentLookup<TeamManagerAI> TeamManagerAILookup;
        public ComponentLookup<Planet> PlanetLookup;
        
        [NativeDisableContainerSafetyRestriction]
        private NativeList<float> _tmpShipImportances;
        
        void Execute(ref Factory factory, ref Building building, in Team team)
        {
            if (PlanetLookup.TryGetComponent(building.PlanetEntity,
                    out Planet planet))
            {
                // Try start next production
                if (factory.CurrentProducedPrefab == Entity.Null)
                {
                    TryStartNewProduction(ref building, ref factory, ref planet, in team);
                }

                if (factory.ProductionTimer > 0f)
                {
                    // Update production time
                    factory.ProductionTimer -= DeltaTime;
                }

                // Update production
                while (factory.ProductionTimer <= 0f && factory.CurrentProducedPrefab != Entity.Null)
                {
                    float3 worldPosition = LocalToWorldLookup[building.MoonEntity].Position;

                    // Create produced entity
                    Entity producedEntity = ECB.Instantiate(factory.CurrentProducedPrefab);
                    ECB.SetComponent(producedEntity, LocalTransform.FromPosition(worldPosition));
                    GameUtilities.SetTeam(ECB, producedEntity, team.Index);

                    // Apply research bonuses
                    if (HealthLookup.TryGetComponent(factory.CurrentProducedPrefab, out Health producedHealth))
                    {
                        producedHealth.MaxHealth *= factory.ProductionResearchBonuses.ShipMaxHealthMultiplier;
                        producedHealth.CurrentHealth = producedHealth.MaxHealth;
                        ECB.SetComponent(producedEntity, producedHealth);
                    }

                    if (ShipLookup.TryGetComponent(factory.CurrentProducedPrefab, out Ship producedShip))
                    {
                        producedShip.AccelerationMultiplier =
                            factory.ProductionResearchBonuses.ShipAccelerationMultiplier;
                        producedShip.MaxSpeedMultiplier =
                            factory.ProductionResearchBonuses.ShipSpeedMultiplier;
                        ECB.SetComponent(producedEntity, producedShip);
                    }

                    if (FighterLookup.TryGetComponent(factory.CurrentProducedPrefab, out Fighter producedFighter))
                    {
                        producedFighter.DamageMultiplier =
                            factory.ProductionResearchBonuses.ShipDamageMultiplier;
                        ECB.SetComponent(producedEntity, producedFighter);
                    }

                    factory.CurrentProducedPrefab = Entity.Null;
                    factory.ProductionResearchBonuses.Reset();

                    TryStartNewProduction(ref building, ref factory, ref planet, in team);
                }

                // Write back to planet for resources
                PlanetLookup[building.PlanetEntity] = planet;
            }
        }

        private void TryStartNewProduction(ref Building building, ref Factory factory, ref Planet planet, in Team team)
        {
            FactoryAction chosenAction = default;

            if (TeamManagerAILookup.TryGetComponent(team.ManagerEntity, out TeamManagerAI teamManagerAI))
            {
                bool hasEnoughSlots = teamManagerAI.EmpireStatistics.TotalShipsCount < MaxShipsPerTeam;
                if (!hasEnoughSlots || !IsOverGlobalShipsLimit)
                {
                    return;
                }
            }

            // Pick a ship based on a weighted random
            if(team.IsNonNeutral())
            {
                if (FactoryActionsLookup.TryGetBuffer(team.ManagerEntity,
                        out DynamicBuffer<FactoryAction> factoryActionsBuffer))
                {
                    _tmpShipImportances.Clear();
                    float cummulativeWeights = 0f;

                    for (int i = 0; i < factoryActionsBuffer.Length; i++)
                    {
                        FactoryAction action = factoryActionsBuffer[i];

                        // Only consider ships that we could build
                        float finalImportance = action.Importance;
                        if (!GameUtilities.HasEnoughResources(action.ResourceCost, in planet))
                        {
                            finalImportance = 0f;
                        }
                        cummulativeWeights += finalImportance;
                        _tmpShipImportances.Add(finalImportance);
                    }

                    int randomIndex = GameUtilities.GetWeightedRandomIndex(cummulativeWeights, in _tmpShipImportances,
                        ref building.Random);
                    if (randomIndex >= 0)
                    {
                        chosenAction = factoryActionsBuffer[randomIndex];
                    }
                }
            }

            // Start building chosen ship
            if (chosenAction.PrefabEntity != Entity.Null)
            {
                if (GameUtilities.TryConsumeResources(chosenAction.ResourceCost, ref planet))
                {
                    factory.ProductionResearchBonuses = planet.ResearchBonuses;
                    factory.CurrentProducedPrefab = chosenAction.PrefabEntity;
                    factory.ProductionTimer += (chosenAction.BuildTime /
                                                factory.ProductionResearchBonuses.FactoryBuildSpeedMultiplier);

                    PlanetLookup[building.PlanetEntity] = planet;
                }
            }
        }

        public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (!_tmpShipImportances.IsCreated)
            {
                _tmpShipImportances = new NativeList<float>(32, Allocator.Temp);
            }
            return true;
        }

        public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask,
            bool chunkWasExecuted)
        {
        }
    }
}
