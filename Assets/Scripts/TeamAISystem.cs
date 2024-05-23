using System;
using System.Collections;
using System.Collections.Generic;
using Galaxy;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Logging;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

[Serializable]
public struct FleetAssessment
{
    public int Count;
    public float Value;
}

public struct EmpireStatistics
{
    public int OwnedPlanetsCount;
    public int TotalPlanetsCount;

    public int MaxAlliedFightersCountForPlanet;
    public int MaxEnemyFightersCountForPlanet;
    public float FarthestDistance;
    public float3 MaxResourceStorageRatio;
    public float3 MaxResourceGenerationRate;
    
    public FleetAssessment FightersAssessment;
    public FleetAssessment WorkersAssessment;
    public FleetAssessment TradersAssessment;
    public int TotalNonFighterShipsCount;
    public int TotalShipsCount;
}

public struct PlanetStatistics
{
    public float ThreatLevel;
    public float SafetyLevel;
    public float DistanceFromOwnedPlanetsScore;
    public float3 ResourceStorageRatioPercentile;
    public float ResourceGenerationScore;
}

[BurstCompile]
[UpdateAfter(typeof(BeginSimulationMainThreadGroup))]
public partial struct TeamAISystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Config>();
        state.RequireForUpdate<TeamManagerReference>();
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        Config config = SystemAPI.GetSingleton<Config>();
        DynamicBuffer<TeamManagerReference> teamManagerReferences =
            SystemAPI.GetSingletonBuffer<TeamManagerReference>();
        EntityQuery teamQuery = SystemAPI.QueryBuilder().WithAll<TeamManager>().Build();
        EntityQuery planetsQuery = SystemAPI.QueryBuilder().WithAll<Planet, LocalTransform, Team>().Build();
        EntityQuery unitsQuery = SystemAPI.QueryBuilder().WithAll<Team, Health>().WithAny<Ship, Building>().Build();
        int teamsCount = teamManagerReferences.Length;
        int aliveTeamsCount = teamQuery.CalculateEntityCount();
        NativeArray<FleetAssessment> fightersFleet = new NativeArray<FleetAssessment>(teamsCount, Allocator.TempJob);
        NativeArray<FleetAssessment> workersFleet = new NativeArray<FleetAssessment>(teamsCount, Allocator.TempJob);
        NativeArray<FleetAssessment> tradersFleet = new NativeArray<FleetAssessment>(teamsCount, Allocator.TempJob);

        // Compute fleet compositions - one job for each ship type, each one in parallel
        {
            JobHandle initialFleetCompositionsDep = state.Dependency;
            
            FighterFleetAssessmentJob fighterFleetJob = new FighterFleetAssessmentJob
            {
                FleetCompositions = fightersFleet,
            };
            state.Dependency = JobHandle.CombineDependencies(state.Dependency, fighterFleetJob.Schedule(initialFleetCompositionsDep)); 
            
            WorkerFleetAssessmentJob workerFleetJob = new WorkerFleetAssessmentJob
            {
                FleetCompositions = workersFleet,
            };
            state.Dependency = JobHandle.CombineDependencies(state.Dependency, workerFleetJob.Schedule(initialFleetCompositionsDep)); 
            
            TraderFleetAssessmentJob traderFleetJob = new TraderFleetAssessmentJob
            {
                FleetCompositions = tradersFleet,
            };
            state.Dependency = JobHandle.CombineDependencies(state.Dependency, traderFleetJob.Schedule(initialFleetCompositionsDep)); 
        }
        
        // Each team's AI is solved in parallel
        {
            NativeArray<Entity> teamEntities = teamQuery.ToEntityArray(state.WorldUpdateAllocator);
            NativeArray<Entity> planetEntities = planetsQuery.ToEntityArray(state.WorldUpdateAllocator);
            NativeArray<Planet> planets = planetsQuery.ToComponentDataArray<Planet>(state.WorldUpdateAllocator);
            NativeArray<Team> planetTeams = planetsQuery.ToComponentDataArray<Team>(state.WorldUpdateAllocator);
            NativeArray<LocalTransform> planetTransforms = planetsQuery.ToComponentDataArray<LocalTransform>(state.WorldUpdateAllocator);
            
            TeamAIJob teamAIJob = new TeamAIJob
            {
                ShipsCollectionEntity = SystemAPI.GetSingletonEntity<ShipCollection>(),
                BuildingsCollectionEntity = SystemAPI.GetSingletonEntity<BuildingCollection>(),
                
                TeamManagerLookup = SystemAPI.GetComponentLookup<TeamManager>(false),
                TeamManagerAILookup = SystemAPI.GetComponentLookup<TeamManagerAI>(false),
                FighterActionsLookup = SystemAPI.GetBufferLookup<FighterAction>(false),
                WorkerActionsLookup = SystemAPI.GetBufferLookup<WorkerAction>(false),
                TraderActionsLookup = SystemAPI.GetBufferLookup<TraderAction>(false),
                FactoryActionsLookup = SystemAPI.GetBufferLookup<FactoryAction>(false),
                PlanetIntelLookup = SystemAPI.GetBufferLookup<PlanetIntel>(false),
                
                MoonReferenceLookup = SystemAPI.GetBufferLookup<MoonReference>(true),
                ShipCollectionBufferLookup = SystemAPI.GetBufferLookup<ShipCollection>(true),
                BuildingCollectionBufferLookup = SystemAPI.GetBufferLookup<BuildingCollection>(true),
                BuildingReferenceLookup = SystemAPI.GetComponentLookup<BuildingReference>(true),
                PlanetLookup = SystemAPI.GetComponentLookup<Planet>(true),
                TeamLookup = SystemAPI.GetComponentLookup<Team>(true),
                ActorTypeLookup = SystemAPI.GetComponentLookup<ActorType>(true),
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                PlanetNetworkLookup = SystemAPI.GetBufferLookup<PlanetNetwork>(true),
                PlanetShipsAssessmentLookup = SystemAPI.GetBufferLookup<PlanetShipsAssessment>(true),
                
                TeamEntities = teamEntities,
                PlanetEntities = planetEntities,
                Planets = planets,
                PlanetTeams = planetTeams,
                PlanetTransforms = planetTransforms,
                FighterFleetAssessments = fightersFleet,
                WorkerFleetAssessments = workersFleet,
                TraderFleetAssessments = tradersFleet,
            };
            state.Dependency = teamAIJob.Schedule(aliveTeamsCount, 1, state.Dependency);
            
            teamEntities.Dispose(state.Dependency);
            planetEntities.Dispose(state.Dependency);
            planets.Dispose(state.Dependency);
            planetTeams.Dispose(state.Dependency);
            planetTransforms.Dispose(state.Dependency);
        }

        {
            NativeArray<Entity> unitEntities = unitsQuery.ToEntityArray(state.WorldUpdateAllocator);
            NativeArray<Team> unitTeams = unitsQuery.ToComponentDataArray<Team>(state.WorldUpdateAllocator);
            
            TeamDefeatedJob teamDefeatedJob = new TeamDefeatedJob
            {
                ECB = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged),
                
                UnitEntities = unitEntities,
                UnitTeams = unitTeams,
                
                HealthLookup = SystemAPI.GetComponentLookup<Health>(false),
            };
            state.Dependency = teamDefeatedJob.Schedule(state.Dependency);

            unitEntities.Dispose(state.Dependency);
            unitTeams.Dispose(state.Dependency);
        }

        fightersFleet.Dispose(state.Dependency);
        workersFleet.Dispose(state.Dependency);
        tradersFleet.Dispose(state.Dependency);
    }

    [BurstCompile]
    [WithAll(typeof(Fighter))]
    public partial struct FighterFleetAssessmentJob : IJobEntity
    {
        public NativeArray<FleetAssessment> FleetCompositions;

        public void Execute(in Ship ship, in Team team)
        {
            FleetAssessment assessment = FleetCompositions[team.Index];
            assessment.Count++;
            assessment.Value += ship.ShipData.Value.Value;
            FleetCompositions[team.Index] = assessment;
        }
    }

    [BurstCompile]
    [WithAll(typeof(Worker))]
    public partial struct WorkerFleetAssessmentJob : IJobEntity
    {
        public NativeArray<FleetAssessment> FleetCompositions;

        public void Execute(in Ship ship, in Team team)
        {
            FleetAssessment assessment = FleetCompositions[team.Index];
            assessment.Count++;
            assessment.Value += ship.ShipData.Value.Value;
            FleetCompositions[team.Index] = assessment;
        }
    }

    [BurstCompile]
    [WithAll(typeof(Trader))]
    public partial struct TraderFleetAssessmentJob : IJobEntity
    {
        public NativeArray<FleetAssessment> FleetCompositions;

        public void Execute(in Ship ship, in Team team)
        {
            FleetAssessment assessment = FleetCompositions[team.Index];
            assessment.Count++;
            assessment.Value += ship.ShipData.Value.Value;
            FleetCompositions[team.Index] = assessment;
        }
    }

    [BurstCompile]
    public struct TeamAIJob : IJobParallelFor
    {
        public Entity ShipsCollectionEntity;
        public Entity BuildingsCollectionEntity;

        // ok to disable safeties because each of these jobs writes to a different team entity
        [NativeDisableContainerSafetyRestriction]
        public ComponentLookup<TeamManager> TeamManagerLookup;

        [NativeDisableContainerSafetyRestriction]
        public ComponentLookup<TeamManagerAI> TeamManagerAILookup;

        [NativeDisableContainerSafetyRestriction]
        public BufferLookup<FighterAction> FighterActionsLookup;

        [NativeDisableContainerSafetyRestriction]
        public BufferLookup<WorkerAction> WorkerActionsLookup;

        [NativeDisableContainerSafetyRestriction]
        public BufferLookup<TraderAction> TraderActionsLookup;

        [NativeDisableContainerSafetyRestriction]
        public BufferLookup<FactoryAction> FactoryActionsLookup;

        [NativeDisableContainerSafetyRestriction]
        public BufferLookup<PlanetIntel> PlanetIntelLookup;

        [ReadOnly] public BufferLookup<MoonReference> MoonReferenceLookup;
        [ReadOnly] public BufferLookup<ShipCollection> ShipCollectionBufferLookup;
        [ReadOnly] public BufferLookup<BuildingCollection> BuildingCollectionBufferLookup;
        [ReadOnly] public ComponentLookup<BuildingReference> BuildingReferenceLookup;
        [ReadOnly] public ComponentLookup<Planet> PlanetLookup;
        [ReadOnly] public ComponentLookup<Team> TeamLookup;
        [ReadOnly] public ComponentLookup<ActorType> ActorTypeLookup;
        [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;

        [ReadOnly] public BufferLookup<PlanetNetwork> PlanetNetworkLookup;
        [ReadOnly] public BufferLookup<PlanetShipsAssessment> PlanetShipsAssessmentLookup;

        [ReadOnly] public NativeArray<Entity> TeamEntities;
        [ReadOnly] public NativeArray<Entity> PlanetEntities;
        [ReadOnly] public NativeArray<Planet> Planets;
        [ReadOnly] public NativeArray<Team> PlanetTeams;
        [ReadOnly] public NativeArray<LocalTransform> PlanetTransforms;
        [ReadOnly] public NativeArray<FleetAssessment> FighterFleetAssessments;
        [ReadOnly] public NativeArray<FleetAssessment> WorkerFleetAssessments;
        [ReadOnly] public NativeArray<FleetAssessment> TraderFleetAssessments;

        public void Execute(int indexOfTeamInTeamReferences)
        {
            Entity teamEntity = TeamEntities[indexOfTeamInTeamReferences];

            int teamIndex = TeamLookup[teamEntity].Index;
            TeamManagerAI teamManagerAI = TeamManagerAILookup[teamEntity];
            DynamicBuffer<FighterAction> fighterActions = FighterActionsLookup[teamEntity];
            DynamicBuffer<WorkerAction> workerActions = WorkerActionsLookup[teamEntity];
            DynamicBuffer<TraderAction> traderActions = TraderActionsLookup[teamEntity];
            DynamicBuffer<FactoryAction> factoryActions = FactoryActionsLookup[teamEntity];
            DynamicBuffer<PlanetIntel> planetIntels = PlanetIntelLookup[teamEntity];
            NativeList<Entity> teamPlanetEntities = new NativeList<Entity>(32, Allocator.Temp);
            
            // Initialize random
            if (teamManagerAI.Random.state == 0)
            {
                teamManagerAI.Random = GameUtilities.GetDeterministicRandom(teamEntity.Index);
            }

            // Clear buffers
            fighterActions.Clear();
            workerActions.Clear();
            traderActions.Clear();
            factoryActions.Clear();
            planetIntels.Clear();

            // Compute statistics about our empire and surroundings
            ComputeEmpireStatistics(teamIndex, teamPlanetEntities, ref teamManagerAI, ref planetIntels);
            
            // Check for team death
            if (teamManagerAI.EmpireStatistics.OwnedPlanetsCount <= 0)
            {
                teamManagerAI.IsDefeated = true;
            }
            else
            {
                DynamicBuffer<ShipCollection> shipsCollection = ShipCollectionBufferLookup[ShipsCollectionEntity];
                DynamicBuffer<BuildingCollection> buildingsCollection =
                    BuildingCollectionBufferLookup[BuildingsCollectionEntity];
                NativeList<float> tmpImportances = new NativeList<float>(32, Allocator.Temp);
                
                // AI Processors
                AIProcessor fighterAIProcessor = new AIProcessor(128, Allocator.Temp);
                AIProcessor workerAIProcessor = new AIProcessor(128, Allocator.Temp);
                AIProcessor traderAIProcessor = new AIProcessor(128, Allocator.Temp);
                
                // Handle creating the list of possible actions that factory AI can choose from
                // (which ships to build)
                HandleFactoryActions(ref teamManagerAI, in shipsCollection, ref factoryActions);

                // Compute per-planet actions for ships
                for (int i = 0; i < planetIntels.Length; i++)
                {
                    PlanetIntel planetIntel = planetIntels[i];

                    // Calculate values used by AI for this planet
                    CalculatePlanetStatistics(
                        in planetIntel,
                        in teamManagerAI,
                        out PlanetStatistics planetStatistics);

                    // Actions related to owned planets
                    if (planetIntel.IsOwned == 1)
                    {
                        HandleFighterDefendAction(
                            ref fighterAIProcessor,
                            ref fighterActions,
                            in planetIntel,
                            in planetStatistics,
                            in teamManagerAI);

                        HandleWorkerBuildAction(
                            ref workerAIProcessor,
                            ref workerActions,
                            ref buildingsCollection,
                            in planetIntel,
                            in planetStatistics,
                            ref teamManagerAI,
                            ref tmpImportances);

                        HandleTraderTradeAction(
                            ref traderAIProcessor,
                            ref traderActions,
                            in planetIntel,
                            in planetStatistics,
                            in teamManagerAI);
                    }
                    // Actions related to non-owned planets
                    else
                    {
                        HandleFighterAttackAction(
                            ref fighterAIProcessor,
                            ref fighterActions,
                            in planetIntel,
                            in planetStatistics,
                            in teamManagerAI);

                        HandleWorkerCaptureAction(
                            ref workerAIProcessor,
                            ref workerActions,
                            in planetIntel,
                            in planetStatistics,
                            in teamManagerAI);
                    }
                }

                // Compute corrected importances after all actions have been registered
                fighterAIProcessor.ComputeFinalImportances();
                workerAIProcessor.ComputeFinalImportances();
                traderAIProcessor.ComputeFinalImportances();

                // Assign corrected importances to actions
                {
                    for (int i = 0; i < fighterActions.Length; i++)
                    {
                        FighterAction c = fighterActions[i];
                        c.Importance = fighterAIProcessor.GetActionImportance(i);
                        fighterActions[i] = c;
                    }

                    for (int i = 0; i < workerActions.Length; i++)
                    {
                        WorkerAction c = workerActions[i];
                        c.Importance = workerAIProcessor.GetActionImportance(i);
                        workerActions[i] = c;
                    }

                    for (int i = 0; i < traderActions.Length; i++)
                    {
                        TraderAction c = traderActions[i];
                        c.ImportanceBias = traderAIProcessor.GetActionImportance(i);
                        traderActions[i] = c;
                    }
                }
            }

            // Write back team AI values
            TeamManagerAILookup[teamEntity] = teamManagerAI;
        }

        private PlanetIntel GetPlanetIntel(
            Entity entity, 
            int teamIndex, 
            int planetTeamIndex, 
            float3 position,
            float radius, 
            float distance, 
            in Planet planet)
        {
            if (PlanetShipsAssessmentLookup.TryGetBuffer(entity,
                    out DynamicBuffer<PlanetShipsAssessment> shipsAssessmentBuffer) &&
                MoonReferenceLookup.TryGetBuffer(entity, out DynamicBuffer<MoonReference> moonReferencesBuffer))
            {
                int alliedShips = 0;
                int alliedFighters = 0;
                int alliedWorkers = 0;
                int alliedTraders = 0;

                int enemyShips = 0;
                int enemyFighters = 0;
                int enemyWorkers = 0;
                int enemyTraders = 0;

                for (int i = 0; i < shipsAssessmentBuffer.Length; i++)
                {
                    if (i == teamIndex)
                    {
                        alliedShips += shipsAssessmentBuffer[i].TotalCount;
                        alliedFighters += shipsAssessmentBuffer[i].FighterCount;
                        alliedWorkers += shipsAssessmentBuffer[i].WorkerCount;
                        alliedTraders += shipsAssessmentBuffer[i].TraderCount;
                    }
                    else
                    {
                        enemyShips += shipsAssessmentBuffer[i].TotalCount;
                        enemyFighters += shipsAssessmentBuffer[i].FighterCount;
                        enemyWorkers += shipsAssessmentBuffer[i].WorkerCount;
                        enemyTraders += shipsAssessmentBuffer[i].TraderCount;
                    }
                }

                Entity freeMoonEntity = Entity.Null;
                int totalMoons = 0;
                int freeMoons = 0;
                int factoriesCount = 0;
                int turretsCount = 0;
                int researchesCount = 0;

                for (int i = 0; i < moonReferencesBuffer.Length; i++)
                {
                    Entity moonEntity = moonReferencesBuffer[i].Entity;
                    if (BuildingReferenceLookup.TryGetComponent(moonEntity, out BuildingReference buildingReference))
                    {
                        totalMoons++;

                        if (ActorTypeLookup.TryGetComponent(buildingReference.BuildingEntity, out ActorType actorType))
                        {
                            switch (actorType.Type)
                            {
                                case ActorType.FactoryType:
                                    factoriesCount++;
                                    break;
                                case ActorType.TurretType:
                                    turretsCount++;
                                    break;
                                case ActorType.ResearchType:
                                    researchesCount++;
                                    break;
                            }
                        }
                        else
                        {
                            if (freeMoonEntity == Entity.Null)
                            {
                                freeMoonEntity = moonEntity;
                            }

                            freeMoons++;
                        }
                    }
                }

                return new PlanetIntel
                {
                    Entity = entity,
                    Position = position,
                    PlanetRadius = radius,
                    Distance = distance,
                    IsOwned = (planetTeamIndex == teamIndex) ? (byte)1 : (byte)0,

                    ResourceGenerationRate = planet.ResourceGenerationRate,
                    CurrentResourceStorage = planet.ResourceCurrentStorage,
                    MaxResourceStorage = planet.ResourceMaxStorage,

                    AlliedShips = alliedShips,
                    AlliedFighters = alliedFighters,
                    AlliedWorkers = alliedWorkers,
                    AlliedTraders = alliedTraders,

                    EnemyShips = enemyShips,
                    EnemyFighters = enemyFighters,
                    EnemyWorkers = enemyWorkers,
                    EnemyTraders = enemyTraders,

                    FreeMoonEntity = freeMoonEntity,
                    TotalMoonsCount = totalMoons,
                    FreeMoonsCount = freeMoons,

                    FactoriesCount = factoriesCount,
                    TurretsCount = turretsCount,
                    ResearchesCount = researchesCount,
                };
            }

            return default;
        }

        private void ComputeEmpireStatistics(
            int teamIndex,
            NativeList<Entity> teamPlanetEntities,
            ref TeamManagerAI teamManagerAI,
            ref DynamicBuffer<PlanetIntel> planetIntels)
        {
            teamManagerAI.EmpireStatistics = default;
            
            // Fleet
            teamManagerAI.EmpireStatistics.FightersAssessment = FighterFleetAssessments[teamIndex];
            teamManagerAI.EmpireStatistics.WorkersAssessment = WorkerFleetAssessments[teamIndex];
            teamManagerAI.EmpireStatistics.TradersAssessment = TraderFleetAssessments[teamIndex];
            
            teamManagerAI.EmpireStatistics.TotalNonFighterShipsCount = 
                teamManagerAI.EmpireStatistics.WorkersAssessment.Count + 
                teamManagerAI.EmpireStatistics.TradersAssessment.Count;
            teamManagerAI.EmpireStatistics.TotalShipsCount =
                teamManagerAI.EmpireStatistics.TotalNonFighterShipsCount + 
                teamManagerAI.EmpireStatistics.FightersAssessment.Count;
            
            // Gather intel about our team's planets
            for (int p = 0; p < PlanetEntities.Length; p++)
            {
                if (PlanetTeams[p].Index == teamIndex)
                {
                    LocalTransform planetLocalTransform = PlanetTransforms[p];
                    Entity planetEntity = PlanetEntities[p];
                    Planet planet = Planets[p];

                    PlanetIntel intel = GetPlanetIntel(planetEntity, teamIndex, teamIndex,
                        planetLocalTransform.Position, planetLocalTransform.Scale * 0.5f, 0f, in planet);
                    planetIntels.Add(intel);

                    teamPlanetEntities.Add(planetEntity);
                }
            }

            teamManagerAI.EmpireStatistics.OwnedPlanetsCount = teamPlanetEntities.Length;

            // Gather intel about our team's neighbor planets
            for (int e = 0; e < teamPlanetEntities.Length; e++)
            {
                DynamicBuffer<PlanetNetwork> planetNetworkBuffer = PlanetNetworkLookup[teamPlanetEntities[e]];

                for (int i = 0; i < planetNetworkBuffer.Length; i++)
                {
                    PlanetNetwork connectedPlanet = planetNetworkBuffer[i];
                    Entity planetEntity = connectedPlanet.Entity;

                    // Check if planet is already added
                    bool isAlreadyAdded = false;
                    for (int j = 0; j < planetIntels.Length; j++)
                    {
                        if (planetIntels[j].Entity == connectedPlanet.Entity)
                        {
                            isAlreadyAdded = true;
                            break;
                        }
                    }

                    // Note: since we've already added all of our own team's planets, all new non-added planets will be non-owned planets.
                    //      we do this in two steps because we have faster access to our own team's planets data.
                    if (!isAlreadyAdded)
                    {
                        float3 planetPosition = connectedPlanet.Position;
                        float planetDistance = connectedPlanet.Distance;
                        Planet planet = PlanetLookup[planetEntity];
                        Team planetTeam = TeamLookup[planetEntity];

                        PlanetIntel intel = GetPlanetIntel(planetEntity, teamIndex, planetTeam.Index, planetPosition,
                            connectedPlanet.Radius, planetDistance,
                            in planet);
                        planetIntels.Add(intel);
                    }
                }
            }

            teamManagerAI.EmpireStatistics.TotalPlanetsCount = planetIntels.Length;
            
            for (int i = 0; i < planetIntels.Length; i++)
            {
                PlanetIntel planetIntel = planetIntels[i];
                teamManagerAI.EmpireStatistics.MaxAlliedFightersCountForPlanet = math.max(teamManagerAI.EmpireStatistics.MaxAlliedFightersCountForPlanet, planetIntel.AlliedFighters);
                teamManagerAI.EmpireStatistics.MaxEnemyFightersCountForPlanet = math.max(teamManagerAI.EmpireStatistics.MaxEnemyFightersCountForPlanet, planetIntel.EnemyFighters);
                teamManagerAI.EmpireStatistics.FarthestDistance = math.max(teamManagerAI.EmpireStatistics.FarthestDistance, planetIntel.Distance);
                teamManagerAI.EmpireStatistics.MaxResourceStorageRatio = math.max(teamManagerAI.EmpireStatistics.MaxResourceStorageRatio,
                    planetIntel.CurrentResourceStorage / planetIntel.MaxResourceStorage);
                teamManagerAI.EmpireStatistics.MaxResourceGenerationRate = math.max(teamManagerAI.EmpireStatistics.MaxResourceGenerationRate, planetIntel.ResourceGenerationRate);
            }
        }

        private void HandleFactoryActions(
            ref TeamManagerAI teamManagerAI,
            in DynamicBuffer<ShipCollection> shipsCollection, 
            ref DynamicBuffer<FactoryAction> factoryActions)
        {
            // Calculate total probabilities per ship type
            float totalFighterProbabilities = 0f;
            float totalWorkerProbabilities = 0f;
            float totalTraderProbabilities = 0f;
            for (int i = 0; i < shipsCollection.Length; i++)
            {
                ShipCollection shipInfo = shipsCollection[i];
                ActorType shipActorType = ActorTypeLookup[shipInfo.PrefabEntity];
                float shipBuildProbability = shipInfo.ShipData.Value.BuildProbabilityForShipType;

                switch (shipActorType.Type)
                {
                    case ActorType.FighterType:
                        totalFighterProbabilities += shipBuildProbability;
                        break;
                    case ActorType.WorkerType:
                        totalWorkerProbabilities += shipBuildProbability;
                        break;
                    case ActorType.TraderType:
                        totalTraderProbabilities += shipBuildProbability;
                        break;
                }
            }

            // For each ship type we could produce, compute an importance
            for (int i = 0; i < shipsCollection.Length; i++)
            {
                ShipCollection shipInfo = shipsCollection[i];
                ActorType shipActorType = ActorTypeLookup[shipInfo.PrefabEntity];
                ref ShipData shipData = ref shipInfo.ShipData.Value;
                float shipBuildProbability = shipData.BuildProbabilityForShipType;

                // Calculate biases
                teamManagerAI.FighterBias = teamManagerAI.MaxShipProductionBias;
                if (teamManagerAI.EmpireStatistics.FightersAssessment.Value > 0f)
                {
                    teamManagerAI.FighterBias =
                        (teamManagerAI.DesiredFightersPerOtherShip * (float)teamManagerAI.EmpireStatistics.TotalNonFighterShipsCount) /
                        (float)teamManagerAI.EmpireStatistics.FightersAssessment.Count;
                }

                teamManagerAI.WorkerBias = teamManagerAI.MaxShipProductionBias;
                if (teamManagerAI.EmpireStatistics.WorkersAssessment.Value > 0f)
                {
                    teamManagerAI.WorkerBias = (teamManagerAI.DesiredWorkerValuePerPlanet * teamManagerAI.EmpireStatistics.TotalPlanetsCount) /
                                               teamManagerAI.EmpireStatistics.WorkersAssessment.Value;
                }

                teamManagerAI.TraderBias = teamManagerAI.MaxShipProductionBias;
                if (teamManagerAI.EmpireStatistics.TradersAssessment.Value > 0f)
                {
                    int planetCount = teamManagerAI.EmpireStatistics.OwnedPlanetsCount;
                    // traders need at least 2 planets to avoid staying in idle.
                    if (planetCount >= 2)
                    {
                        teamManagerAI.TraderBias = (teamManagerAI.DesiredTraderValuePerOwnedPlanet * planetCount) /
                                                   teamManagerAI.EmpireStatistics.TradersAssessment.Value;
                    }
                    else
                    {
                        teamManagerAI.TraderBias = 0;
                    }
                }

                // Calculate final probability
                float finalProbability = 0f;
                switch (shipActorType.Type)
                {
                    case ActorType.FighterType:
                    {
                        float probabilityInType = shipBuildProbability / totalFighterProbabilities;
                        finalProbability = teamManagerAI.FighterBias * probabilityInType;
                        break;
                    }
                    case ActorType.WorkerType:
                    {
                        float probabilityInType = shipBuildProbability / totalWorkerProbabilities;
                        finalProbability = teamManagerAI.WorkerBias * probabilityInType;
                        break;
                    }
                    case ActorType.TraderType:
                    {
                        float probabilityInType = shipBuildProbability / totalTraderProbabilities;
                        finalProbability = teamManagerAI.TraderBias * probabilityInType;
                        break;
                    }
                }

                factoryActions.Add(new FactoryAction
                {
                    PrefabEntity = shipInfo.PrefabEntity,
                    Importance = finalProbability,
                    ResourceCost = shipData.ResourcesCost,
                    BuildTime = shipData.BuildTime,
                });
            }
        }

        private Entity PickRandomBuildingToBuild(
            Entity planetEntity, 
            ref NativeList<float> tmpImportances,
            in DynamicBuffer<BuildingCollection> buildingsCollectionBuffer, 
            ref Random random)
        {
            // Prioritize factories
            bool planetHasAFactory = false;
            if (MoonReferenceLookup.TryGetBuffer(planetEntity, out DynamicBuffer<MoonReference> moonReferences))
            {
                for (int i = 0; i < moonReferences.Length; i++)
                {
                    Entity planetMoonEntity = moonReferences[i].Entity;
                    if (BuildingReferenceLookup.TryGetComponent(planetMoonEntity,
                            out BuildingReference planetMoonBuildingReference))
                    {
                        if (ActorTypeLookup.TryGetComponent(planetMoonBuildingReference.BuildingEntity,
                                out ActorType actorType))
                        {
                            if (actorType.Type == ActorType.FactoryType)
                            {
                                planetHasAFactory = true;
                                break;
                            }
                        }
                    }
                }
            }

            // First select a random building
            Entity buildingPrefab = default;
            {
                tmpImportances.Clear();
                float cummulativeImportances = 0f;

                for (int i = 0; i < buildingsCollectionBuffer.Length; i++)
                {
                    BuildingCollection buildingInfo = buildingsCollectionBuffer[i];
                    ref BuildingData buildingData = ref buildingInfo.BuildingData.Value;
                    cummulativeImportances += buildingData.BuildProbability;
                    tmpImportances.Add(buildingData.BuildProbability);
                }

                int randomIndex =
                    GameUtilities.GetWeightedRandomIndex(cummulativeImportances, in tmpImportances, ref random);
                if (randomIndex >= 0)
                {
                    buildingPrefab = buildingsCollectionBuffer[randomIndex].PrefabEntity;
                }
            }

            // But if planet doesn't have a factory, search for a factory
            if (!planetHasAFactory)
            {
                for (int i = 0; i < buildingsCollectionBuffer.Length; i++)
                {
                    Entity tmpPrefab = buildingsCollectionBuffer[i].PrefabEntity;
                    if (ActorTypeLookup.TryGetComponent(tmpPrefab, out ActorType actorType))
                    {
                        if (actorType.Type == ActorType.FactoryType)
                        {
                            buildingPrefab = tmpPrefab;
                            break;
                        }
                    }
                }
            }

            return buildingPrefab;
        }

        private void CalculatePlanetStatistics(
            in PlanetIntel planetIntel,
            in TeamManagerAI teamManagerAI,
            out PlanetStatistics planetStatistics)
        {
            planetStatistics = default;
            
            if (teamManagerAI.EmpireStatistics.MaxEnemyFightersCountForPlanet > 0)
            {
                planetStatistics.ThreatLevel = math.saturate(planetIntel.EnemyFighters / (float)teamManagerAI.EmpireStatistics.MaxEnemyFightersCountForPlanet);
            }
            
            if (teamManagerAI.EmpireStatistics.MaxAlliedFightersCountForPlanet > 0)
            {
                planetStatistics.SafetyLevel = math.saturate(planetIntel.AlliedFighters / (float)teamManagerAI.EmpireStatistics.MaxAlliedFightersCountForPlanet);
            }

            if (teamManagerAI.EmpireStatistics.FarthestDistance > 0f)
            {
                planetStatistics.DistanceFromOwnedPlanetsScore =
                    math.saturate(1f - math.saturate(planetIntel.Distance / teamManagerAI.EmpireStatistics.FarthestDistance));
            }

            if (math.lengthsq(teamManagerAI.EmpireStatistics.MaxResourceStorageRatio) > 0f)
            {
                planetStatistics.ResourceStorageRatioPercentile =
                    math.saturate((planetIntel.CurrentResourceStorage / planetIntel.MaxResourceStorage) /
                                  teamManagerAI.EmpireStatistics.MaxResourceStorageRatio);
            }

            float maxResourcesGenerationScore = math.csum(teamManagerAI.EmpireStatistics.MaxResourceGenerationRate);
            if (maxResourcesGenerationScore > 0f)
            {
                planetStatistics.ResourceGenerationScore =
                    math.saturate(1f - math.saturate(math.csum(planetIntel.ResourceGenerationRate) / maxResourcesGenerationScore));
            }
        }

        private void HandleFighterDefendAction(
            ref AIProcessor fighterAIProcessor,
            ref DynamicBuffer<FighterAction> fighterActions, 
            in PlanetIntel planetIntel, 
            in PlanetStatistics planetStatistics,
            in TeamManagerAI teamManagerAI)
        {
            // Fighters want to defend valuable planets
            AIAction fighterAIAction = AIAction.New();

            fighterAIAction.ApplyConsideration(MathUtilities.Clamp(planetStatistics.ThreatLevel,
                teamManagerAI.FighterDefendThreatLevelConsiderationClamp));
            fighterAIAction.ApplyConsideration(MathUtilities.Clamp(planetStatistics.ResourceGenerationScore,
                teamManagerAI.FighterDefendResourceScoreConsiderationClamp));
            fighterAIAction.ApplyConsideration(teamManagerAI.FighterDefendPlanetConsideration);

            if (fighterAIAction.HasConsiderationsAndImportance())
            {
                GameUtilities.AddAction(ref fighterActions, ref fighterAIProcessor, new FighterAction
                {
                    Entity = planetIntel.Entity,
                    Position = planetIntel.Position,
                    Radius = planetIntel.PlanetRadius,
                    IsOwned = planetIntel.IsOwned,
                }, fighterAIAction);
            }
        }

        private void HandleWorkerBuildAction(
            ref AIProcessor workerAIProcessor,
            ref DynamicBuffer<WorkerAction> workerActions, 
            ref DynamicBuffer<BuildingCollection> buildingsCollection,
            in PlanetIntel planetIntel, 
            in PlanetStatistics planetStatistics,
            ref TeamManagerAI teamManagerAI, 
            ref NativeList<float> tmpImportances)
        {
            // Workers want to build buildings on owned planets that have available moons (moons are building slots)
            AIAction workerAIAction = AIAction.New();

            if (planetIntel.FreeMoonsCount > 0 && planetIntel.FreeMoonEntity != Entity.Null)
            {
                Entity buildingPrefab = PickRandomBuildingToBuild(planetIntel.Entity,
                    ref tmpImportances, in buildingsCollection,
                    ref teamManagerAI.Random);

                if (buildingPrefab != Entity.Null)
                {
                    LocalTransform moonTransform = LocalTransformLookup[planetIntel.FreeMoonEntity];
                    float3 freeMoonPosition = moonTransform.Position;
                    float freeMoonRadius = moonTransform.Scale * 0.5f;
                    float buildingOccupancyBias =
                        math.saturate((float)planetIntel.FreeMoonsCount / (float)planetIntel.TotalMoonsCount);

                    // Workers are more interested in building building on high-resource planets
                    workerAIAction.ApplyConsideration(MathUtilities.Clamp(planetStatistics.SafetyLevel,
                        teamManagerAI.WorkerBuildSafetyLevelConsiderationClamp));
                    workerAIAction.ApplyConsideration(MathUtilities.Clamp(planetStatistics.ResourceGenerationScore,
                        teamManagerAI.WorkerBuildResourceScoreConsiderationClamp));
                    workerAIAction.ApplyConsideration(buildingOccupancyBias);
                    workerAIAction.ApplyConsideration(teamManagerAI.WorkerBuildConsideration);

                    if (workerAIAction.HasConsiderationsAndImportance())
                    {
                        GameUtilities.AddAction(ref workerActions, ref workerAIProcessor, new WorkerAction
                        {
                            Type = (byte)1, // Build
                            Entity = planetIntel.FreeMoonEntity,
                            Position = freeMoonPosition,
                            PlanetRadius = freeMoonRadius,
                            BuildingPrefab = buildingPrefab,
                        }, workerAIAction);
                    }
                }
            }
        }

        private void HandleTraderTradeAction(
            ref AIProcessor traderAIProcessor,
            ref DynamicBuffer<TraderAction> traderActions, 
            in PlanetIntel planetIntel, 
            in PlanetStatistics planetStatistics,
            in TeamManagerAI teamManagerAI)
        {
            // Traders trade between owned planets and try to even out resource storages across all planets
            AIAction traderAIAction = AIAction.New();

            traderAIAction.ApplyConsideration(MathUtilities.Clamp(planetStatistics.SafetyLevel,
                teamManagerAI.TraderSafetyLevelConsiderationClamp));

            if (traderAIAction.HasConsiderationsAndImportance())
            {
                GameUtilities.AddAction(ref traderActions, ref traderAIProcessor, new TraderAction
                {
                    Entity = planetIntel.Entity,
                    Position = planetIntel.Position,
                    ResourceStorageRatioPercentile = planetStatistics.ResourceStorageRatioPercentile,
                    Radius = planetIntel.PlanetRadius,
                }, traderAIAction);
            }
        }

        private void HandleFighterAttackAction(
            ref AIProcessor fighterAIProcessor,
            ref DynamicBuffer<FighterAction> fighterActions, 
            in PlanetIntel planetIntel, 
            in PlanetStatistics planetStatistics,
            in TeamManagerAI teamManagerAI)
        {
            // Fighters want to go fight for non-owned planets
            AIAction fighterAIAction = AIAction.New();

            fighterAIAction.ApplyConsideration(MathUtilities.Clamp(planetStatistics.ThreatLevel,
                teamManagerAI.FighterAttackThreatLevelConsiderationClamp));
            fighterAIAction.ApplyConsideration(MathUtilities.Clamp(planetStatistics.ResourceGenerationScore,
                teamManagerAI.FighterAttackResourceScoreConsiderationClamp));
            fighterAIAction.ApplyConsideration(MathUtilities.Clamp(planetStatistics.DistanceFromOwnedPlanetsScore,
                teamManagerAI.FighterAttackDistanceFromOwnedPlanetsConsiderationClamp));
            fighterAIAction.ApplyConsideration(teamManagerAI.FighterAttackPlanetConsideration);

            if (fighterAIAction.HasConsiderationsAndImportance())
            {
                GameUtilities.AddAction(ref fighterActions, ref fighterAIProcessor, new FighterAction
                {
                    Entity = planetIntel.Entity,
                    Position = planetIntel.Position,
                    Radius = planetIntel.PlanetRadius,
                    IsOwned = planetIntel.IsOwned,
                }, fighterAIAction);
            }
        }
        
        private void HandleWorkerCaptureAction(
            ref AIProcessor workerAIProcessor,
            ref DynamicBuffer<WorkerAction> workerActions, 
            in PlanetIntel planetIntel, 
            in PlanetStatistics planetStatistics,
            in TeamManagerAI teamManagerAI)
        {
            // Workers want to capture non-owned planets, but with a bias for safety
            AIAction workerAIAction = AIAction.New();

            workerAIAction.ApplyConsideration(MathUtilities.Clamp(planetStatistics.SafetyLevel,
                teamManagerAI.WorkerCaptureSafetyLevelConsiderationClamp));
            workerAIAction.ApplyConsideration(MathUtilities.Clamp(planetStatistics.ResourceGenerationScore,
                teamManagerAI.WorkerCaptureResourceScoreConsiderationClamp));
            workerAIAction.ApplyConsideration(MathUtilities.Clamp(planetStatistics.DistanceFromOwnedPlanetsScore,
                teamManagerAI.WorkerCaptureDistanceFromOwnedPlanetsConsiderationClamp));
            workerAIAction.ApplyConsideration(teamManagerAI.WorkerCapturePlanetConsideration);

            if (workerAIAction.HasConsiderationsAndImportance())
            {
                GameUtilities.AddAction(ref workerActions, ref workerAIProcessor, new WorkerAction
                {
                    Type = (byte)0, // Capture
                    Entity = planetIntel.Entity,
                    Position = planetIntel.Position,
                    PlanetRadius = planetIntel.PlanetRadius,
                }, workerAIAction);
            }
        }
    }

    public partial struct TeamDefeatedJob : IJobEntity
    {
        public EntityCommandBuffer ECB;
        [ReadOnly]
        public NativeArray<Entity> UnitEntities;
        [ReadOnly]
        public NativeArray<Team> UnitTeams;

        public ComponentLookup<Health> HealthLookup;
        
        void Execute(Entity entity, in Team team, in TeamManagerAI teamManagerAI)
        {
            if (teamManagerAI.IsDefeated)
            {
                ECB.DestroyEntity(entity);

                // Destroy all units belonging to this team
                for (int i = 0; i < UnitTeams.Length; i++)
                {
                    if (UnitTeams[i].Index == team.Index)
                    {
                        Entity unitEntity = UnitEntities[i];
                        if (HealthLookup.TryGetComponent(unitEntity, out Health health))
                        {
                            health.CurrentHealth = -1000;
                            HealthLookup[unitEntity] = health;
                        }
                    }
                }
            }
        }
    }
}