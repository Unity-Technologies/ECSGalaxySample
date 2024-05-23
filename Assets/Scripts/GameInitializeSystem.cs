using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Logging;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace Galaxy
{
    [UpdateInGroup(typeof(BeginSimulationMainThreadGroup))]
    public partial struct GameInitializeSystem : ISystem
    {
        private uint _nonDeterministicSeed;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Config>();
            state.RequireForUpdate<ShipCollection>();
            state.RequireForUpdate<BuildingCollection>();
            state.RequireForUpdate<ShipSpawnParams>();
            state.RequireForUpdate<GameCamera>();

            _nonDeterministicSeed = GameUtilities.GetUniqueUIntFromInt(DateTime.Now.Millisecond);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Initialize the game when we find a config singleton with disabled initialized state
            if (SystemAPI.TryGetSingleton(out Config config))
            {
                if (!config.MustInitializeGame)
                    return;

                // Set state to initialized
                config.MustInitializeGame = false;
                SystemAPI.SetSingleton(config);

                // Get and create initialization data
                float simulationCubeHalfExtents = config.HomePlanetSpawnRadius + config.SimulationBoundsPadding;
                int teamsCount = SystemAPI.GetSingletonBuffer<TeamConfig>().Length;
                int spawnedPlanetCounter = 0;

                // Random
                Random random = new Random(config.GameInitializationRandomSeed);
                if (config.UseNonDeterministicRandomSeed)
                {
                    random = new Random(_nonDeterministicSeed);
                }

                // Create some game management entities
                Entity teamManagerReferencesSingletonEntity =
                    state.EntityManager.Instantiate(config.TeamManagerReferencesPrefab);
                Entity runtimeResourcesEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(runtimeResourcesEntity, new SpatialDatabaseSingleton());

                // Allocations
                NativeList<Entity> homePlanetEntities = new NativeList<Entity>(Allocator.Temp);
                NativeArray<ShipCollection> shipsCollection =
                    SystemAPI.GetSingletonBuffer<ShipCollection>().ToNativeArray(Allocator.Temp);
                NativeArray<BuildingCollection> buildingsCollection =
                    SystemAPI.GetSingletonBuffer<BuildingCollection>().ToNativeArray(Allocator.Temp);
                NativeArray<ShipSpawnParams> initialShipSpawns =
                    SystemAPI.GetSingletonBuffer<ShipSpawnParams>().ToNativeArray(Allocator.Temp);

                SetupTeams(ref state, in config, ref random, teamsCount, ref spawnedPlanetCounter,
                    teamManagerReferencesSingletonEntity, ref homePlanetEntities);
                SpawnNeutralPlanets(ref state, in config, ref random, teamsCount, ref spawnedPlanetCounter);

                // Allocations
                EntityQuery planetsQuery = SystemAPI.QueryBuilder().WithAll<Planet, LocalTransform>().Build();
                NativeArray<Entity> planetEntities = planetsQuery.ToEntityArray(AllocatorManager.Temp);
                NativeArray<LocalTransform> planetTransforms =
                    planetsQuery.ToComponentDataArray<LocalTransform>(AllocatorManager.Temp);

                SpawnMoons(ref state, in config, ref random, in homePlanetEntities, in planetEntities,
                    in planetTransforms);
                SpawnInitialShips(ref state, in config, ref random, in initialShipSpawns, shipsCollection,
                    in homePlanetEntities);
                SpawnInitialTeamBuildings(ref state, in config, in buildingsCollection, in homePlanetEntities);
                CreateTargetablesSpatialDatabase(ref state, in config, simulationCubeHalfExtents);
                CreatePlanetNavigationGrid(ref state, in config, simulationCubeHalfExtents, in planetEntities,
                    in planetTransforms);
                ComputePlanetsNetwork(ref state, in config, in planetEntities, in planetTransforms);
                PlaceCamera(ref state, in config);

                shipsCollection.Dispose();
                buildingsCollection.Dispose();
                initialShipSpawns.Dispose();
                planetEntities.Dispose();
                planetTransforms.Dispose();
                homePlanetEntities.Dispose();
            }
        }

        private void SetupTeams(
            ref SystemState state,
            in Config config,
            ref Random random,
            int teamsCount,
            ref int spawnedPlanetCounter,
            Entity teamManagerReferencesSingletonEntity,
            ref NativeList<Entity> homePlanetEntities)
        {
            NativeList<float3> teamSpawnPos = new NativeList<float3>(Allocator.Temp);
            MathUtilities.GenerateEquidistantPointsOnSphere(ref teamSpawnPos, teamsCount,
                config.HomePlanetSpawnRadius, 50);

            teamsCount = math.min(255, teamsCount);
            for (int i = 0; i < teamsCount; i++)
            {
                // Create team manager entity 
                Entity teamManagerEntity = state.EntityManager.Instantiate(config.TeamManagerPrefab);

                // Add team data
                DynamicBuffer<TeamConfig> teamConfigs = SystemAPI.GetSingletonBuffer<TeamConfig>();
                DynamicBuffer<TeamManagerReference> teamManagerReferences =
                    state.EntityManager.GetBuffer<TeamManagerReference>(teamManagerReferencesSingletonEntity);
                teamManagerReferences.Add(new TeamManagerReference
                {
                    Entity = teamManagerEntity,
                });
                TeamManager teamManager = state.EntityManager.GetComponentData<TeamManager>(teamManagerEntity);
                teamManager.Name = teamConfigs[i].Name;
                teamManager.Color = teamConfigs[i].Color;
                teamManager.LaserColor = teamConfigs[i].Color.xyzw * config.LaserEmissionPower;
                teamManager.ThrusterColor = teamConfigs[i].Color.xyz * config.ThrusterEmissionPower;
                teamManager.LaserSparksColor = teamConfigs[i].Color.xyz * config.LaserSparksEmissionPower;
                state.EntityManager.SetComponentData<TeamManager>(teamManagerEntity, teamManager);
                
                // Set team
                GameUtilities.SetTeam(state.EntityManager, teamManagerEntity, i);

                // spawn home planet
                {
                    Entity planetEntity = CreatePlanet(
                        ref state,
                        config,
                        true,
                        teamSpawnPos[i],
                        config.HomePlanetResourceGenerationRate,
                        config.HomePlanetResourceGenerationRate,
                        config.PlanetResourceMaxStorage,
                        i,
                        teamsCount,
                        ref spawnedPlanetCounter,
                        ref random);
                    homePlanetEntities.Add(planetEntity);
                }
            }

            teamSpawnPos.Dispose();
        }

        private void SpawnNeutralPlanets(
            ref SystemState state,
            in Config config,
            ref Random random,
            int teamsCount,
            ref int spawnedPlanetCounter)
        {
            for (int i = 0; i < config.NeutralPlanetsCount; i++)
            {
                CreatePlanet(
                    ref state,
                    config,
                    false,
                    MathUtilities.RandomInSphere(ref random, config.HomePlanetSpawnRadius),
                    config.ResourceGenerationRateMin,
                    config.ResourceGenerationRateMax,
                    config.PlanetResourceMaxStorage,
                    Team.NeutralTeam,
                    teamsCount,
                    ref spawnedPlanetCounter,
                    ref random);
            }
        }

        private void SpawnMoons(
            ref SystemState state,
            in Config config,
            ref Random random,
            in NativeList<Entity> homePlanetEntities,
            in NativeArray<Entity> planetEntities,
            in NativeArray<LocalTransform> planetTransforms)
        {
            for (int i = 0; i < planetEntities.Length; i++)
            {
                Entity planetEntity = planetEntities[i];
                LocalTransform planetTransform = planetTransforms[i];

                bool isHomePlanet = false;
                for (int h = 0; h < homePlanetEntities.Length; h++)
                {
                    if (homePlanetEntities[h] == planetEntity)
                    {
                        isHomePlanet = true;
                        break;
                    }
                }

                float3 planetPosition = planetTransform.Position;
                float planetScale = planetTransform.Scale;

                int numMoons = config.NumMoonsHomePlanet;
                if (!isHomePlanet)
                {
                    numMoons = random.NextInt(config.NumMoonsRange.x, config.NumMoonsRange.y + 1);
                }

                for (int m = 0; m < numMoons; m++)
                {
                    float3 moonPosOffset = random.NextFloat3Direction() *
                                           ((planetScale * 0.5f) + config.MoonDistanceFromSurface);
                    float3 moonWorldPosition = planetPosition + moonPosOffset;
                    float moonSize = random.NextFloat(config.MoonSizeRange.x, config.MoonSizeRange.y);

                    Entity moonEntity = state.EntityManager.Instantiate(config.MoonPrefab);

                    Moon moon = state.EntityManager.GetComponentData<Moon>(moonEntity);
                    moon.PlanetEntity = planetEntity;
                    state.EntityManager.SetComponentData(moonEntity, moon);
                    state.EntityManager.SetComponentData(moonEntity,
                        LocalTransform.FromPositionRotationScale(moonWorldPosition, quaternion.identity,
                            moonSize));

                    DynamicBuffer<MoonReference> moonReferencesBuffer =
                        state.EntityManager.GetBuffer<MoonReference>(planetEntity);
                    moonReferencesBuffer.Add(new MoonReference
                    {
                        Entity = moonEntity,
                    });
                }
            }
        }

        private void SpawnInitialShips(
            ref SystemState state,
            in Config config,
            ref Random random,
            in NativeArray<ShipSpawnParams> initialShipSpawns,
            in NativeArray<ShipCollection> shipsCollectionBuffer,
            in NativeList<Entity> homePlanetEntities)
        {
            for (int i = 0; i < homePlanetEntities.Length; i++)
            {
                Entity planetEntity = homePlanetEntities[i];
                Team planetTeam = state.EntityManager.GetComponentData<Team>(planetEntity);
                LocalTransform planetTransform = state.EntityManager.GetComponentData<LocalTransform>(planetEntity);
                float planetRadius = planetTransform.Scale * 0.5f;

                for (int j = 0; j < initialShipSpawns.Length; j++)
                {
                    ShipSpawnParams spawnParams = initialShipSpawns[j];
                    ShipCollection shipInfo = shipsCollectionBuffer[spawnParams.IndexInCollection];

                    for (int s = 0; s < spawnParams.SpawnCount; s++)
                    {
                        Entity shipEntity = state.EntityManager.Instantiate(shipInfo.PrefabEntity);
                        state.EntityManager.SetComponentData(shipEntity,
                            LocalTransform.FromPosition(planetTransform.Position + (random.NextFloat3Direction() *
                                planetRadius * shipInfo.ShipData.Value.PlanetOrbitOffset)));
                        GameUtilities.SetTeam(state.EntityManager, shipEntity, planetTeam.Index);
                    }
                }
            }
        }

        private void SpawnInitialTeamBuildings(
            ref SystemState state,
            in Config config,
            in NativeArray<BuildingCollection> buildingsCollection,
            in NativeList<Entity> homePlanetEntities)
        {
            if (config.InitialBuildingIndex >= 0 && config.InitialBuildingIndex < buildingsCollection.Length)
            {
                for (int i = 0; i < homePlanetEntities.Length; i++)
                {
                    Entity planetEntity = homePlanetEntities[i];
                    Team planetTeam = state.EntityManager.GetComponentData<Team>(planetEntity);
                    DynamicBuffer<MoonReference> moonReferences =
                        state.EntityManager.GetBuffer<MoonReference>(planetEntity);

                    if (moonReferences.Length > 0)
                    {
                        Entity moonEntity = moonReferences[0].Entity;
                        GameUtilities.CreateBuilding(state.EntityManager,
                            buildingsCollection[config.InitialBuildingIndex].PrefabEntity, moonEntity,
                            planetEntity, planetTeam.Index);
                    }
                }
            }
        }

        private void CreateTargetablesSpatialDatabase(
            ref SystemState state,
            in Config config,
            float simulationCubeHalfExtents)
        {
            ref SpatialDatabaseSingleton spatialDatabaseSingleton = ref SystemAPI.GetSingletonRW<SpatialDatabaseSingleton>().ValueRW;
            spatialDatabaseSingleton.TargetablesSpatialDatabase =
                state.EntityManager.Instantiate(config.SpatialDatabasePrefab);
            SpatialDatabase spatialDatabase =
                state.EntityManager.GetComponentData<SpatialDatabase>(spatialDatabaseSingleton
                    .TargetablesSpatialDatabase);
            DynamicBuffer<SpatialDatabaseCell> cellsBuffer =
                state.EntityManager.GetBuffer<SpatialDatabaseCell>(spatialDatabaseSingleton.TargetablesSpatialDatabase);
            DynamicBuffer<SpatialDatabaseElement> elementsBuffer =
                state.EntityManager.GetBuffer<SpatialDatabaseElement>(spatialDatabaseSingleton
                    .TargetablesSpatialDatabase);

            SpatialDatabase.Initialize(
                simulationCubeHalfExtents,
                config.SpatialDatabaseSubdivisions,
                config.ShipsSpatialDatabaseCellCapacity,
                ref spatialDatabase,
                ref cellsBuffer,
                ref elementsBuffer);

            state.EntityManager.SetComponentData(spatialDatabaseSingleton.TargetablesSpatialDatabase, spatialDatabase);
        }

        private void CreatePlanetNavigationGrid(
            ref SystemState state,
            in Config config,
            float simulationCubeHalfExtents,
            in NativeArray<Entity> planetEntities,
            in NativeArray<LocalTransform> planetTransforms)
        {
            NativeList<PlanetNavigationBuildData> navBuildDatas =
                new NativeList<PlanetNavigationBuildData>(Allocator.Temp);
            for (int i = 0; i < planetEntities.Length; i++)
            {
                navBuildDatas.Add(new PlanetNavigationBuildData
                {
                    Entity = planetEntities[i],
                    Position = planetTransforms[i].Position,
                    Radius = planetTransforms[i].Scale * 0.5f,
                });
            }

            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            PlanetNavigationGridUtility.CreatePlanetNavigationGrid(ecb, navBuildDatas,
                simulationCubeHalfExtents, config.PlanetNavigationGridSubdivisions);
            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            navBuildDatas.Dispose();
        }

        private void ComputePlanetsNetwork(
            ref SystemState state,
            in Config config,
            in NativeArray<Entity> planetEntities,
            in NativeArray<LocalTransform> planetTransforms)
        {
            for (int i = 0; i < planetEntities.Length; i++)
            {
                Entity sourceEntity = planetEntities[i];
                LocalTransform sourceTransform = planetTransforms[i];

                DynamicBuffer<PlanetNetwork> planetNewtorkBuffer =
                    state.EntityManager.GetBuffer<PlanetNetwork>(sourceEntity);
                PlanetNetwork sourceElement = new PlanetNetwork
                {
                    Entity = sourceEntity,
                    Position = sourceTransform.Position,
                    Radius = sourceTransform.Scale * 0.5f
                };

                for (int j = i + 1; j < planetEntities.Length; j++)
                {
                    Entity otherEntity = planetEntities[j];
                    LocalTransform otherTransform = planetTransforms[j];
                    float distance = math.distance(sourceTransform.Position, otherTransform.Position);
                    DynamicBuffer<PlanetNetwork> otherPlanetNewtorkBuffer =
                        state.EntityManager.GetBuffer<PlanetNetwork>(otherEntity);
                    PlanetNetwork otherElement = new PlanetNetwork
                    {
                        Entity = otherEntity,
                        Position = otherTransform.Position,
                        Distance = distance,
                        Radius = otherTransform.Scale * 0.5f
                    };

                    // Add in sorted order for both planets
                    AddPlanetNetworkSorted(ref planetNewtorkBuffer, otherElement,
                        config.PlanetsNetworkCapacity);
                    sourceElement.Distance = otherElement.Distance;
                    AddPlanetNetworkSorted(ref otherPlanetNewtorkBuffer, sourceElement,
                        config.PlanetsNetworkCapacity);
                }
            }
        }

        private void PlaceCamera(ref SystemState state, in Config config)
        {
            Entity cameraEntity = SystemAPI.GetSingletonEntity<GameCamera>();
            LocalTransform cameraTransform = state.EntityManager.GetComponentData<LocalTransform>(cameraEntity);
            cameraTransform.Position.z = -config.HomePlanetSpawnRadius * config.StartCameraDistanceRatio;
            state.EntityManager.SetComponentData<LocalTransform>(cameraEntity, cameraTransform);
        }

        private static Entity CreatePlanet(ref SystemState state, Config config, bool isHomePlanet, float3 position,
            float3 resourceGenerationRateMin, float3 resourceGenerationRateMax, float3 resourceMaxStorage,
            int team, int numTeams, ref int spawnedPlanetsCounter, ref Random random)
        {
            Entity planetEntity = state.EntityManager.Instantiate(config.PlanetPrefab);

            // set size, position, and stats
            state.EntityManager.SetComponentData(planetEntity, new LocalTransform
            {
                Position = position,
                Scale = random.NextFloat(config.MinPlanetSize, config.MaxPlanetSize),
                Rotation = quaternion.identity
            });

            float3 finalResourceGenRate = random.NextFloat3(resourceGenerationRateMin, resourceGenerationRateMax);
            if (!isHomePlanet)
            {
                bool3 hasResources = random.NextFloat3(new float3(1f)) <= config.ResourceGenerationProbabilities;

                // Make sure the planet has at least one resource type
                if (!math.any(hasResources))
                {
                    int randomResourceType = random.NextInt(0, 3);
                    switch (randomResourceType)
                    {
                        case 0:
                            hasResources.x = true;
                            break;
                        case 1:
                            hasResources.y = true;
                            break;
                        case 2:
                            hasResources.z = true;
                            break;
                    }
                }

                finalResourceGenRate *= math.select(0f, 1f, hasResources);
            }

            Planet planet = state.EntityManager.GetComponentData<Planet>(planetEntity);
            planet.ShipsAssessmentCounter = spawnedPlanetsCounter;
            planet.ResourceGenerationRate = finalResourceGenRate;
            planet.ResourceMaxStorage = resourceMaxStorage;
            state.EntityManager.SetComponentData(planetEntity, planet);

            DynamicBuffer<PlanetShipsAssessment> shipsAssessmentBuffer =
                state.EntityManager.AddBuffer<PlanetShipsAssessment>(planetEntity);
            shipsAssessmentBuffer.Resize(numTeams, NativeArrayOptions.ClearMemory);

            DynamicBuffer<CapturingWorker> capturingWorkerBuffer =
                state.EntityManager.GetBuffer<CapturingWorker>(planetEntity);
            capturingWorkerBuffer.Resize(numTeams, NativeArrayOptions.ClearMemory);

            GameUtilities.SetTeam(state.EntityManager, planetEntity, team);

            return planetEntity;
        }

        private static void AddPlanetNetworkSorted(ref DynamicBuffer<PlanetNetwork> planetsBuffer,
            PlanetNetwork element, int maxLength)
        {
            // early exit if entity is already there
            for (int p = 0; p < planetsBuffer.Length; p++)
            {
                if (planetsBuffer[p].Entity == element.Entity)
                {
                    return;
                }
            }

            // Insert in sorted order of closest planet first
            bool insertedElement = false;
            for (int p = 0; p < planetsBuffer.Length; p++)
            {
                if (planetsBuffer[p].Distance > element.Distance)
                {
                    planetsBuffer.Insert(p, element);
                    insertedElement = true;
                    break;
                }
            }

            if (!insertedElement && planetsBuffer.Length < maxLength)
            {
                planetsBuffer.Add(element);
            }

            // Trim length
            if (planetsBuffer.Length > maxLength)
            {
                planetsBuffer.Resize(maxLength, NativeArrayOptions.ClearMemory);
            }
        }
    }
}