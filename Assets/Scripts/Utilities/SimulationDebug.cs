using System;
using System.Collections;
using System.Collections.Generic;
using Galaxy;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Serialization;

public class SimulationDebug : MonoBehaviour
{
    [Header("Modes")]
    public bool DebugRuler;
    public bool DebugSpatialDatabase;
    public bool DebugPlanetNavigationGrid;
    public bool DebugPlanetsNetwork;
    public bool DebugPlanetShipsAssessment;
    public bool DebugFighterActions;
    public bool DebugWorkerActions;
    public bool DebugTraderActions;

    [Header("Details - Ruler")] 
    public float RulerLength = 1f;

    [Header("Details - Spatial Database")] 
    public Color SpatialDatabaseBoundsColor = Color.cyan;
    public Color SpatialDatabaseCellsColor = Color.cyan;
    public Color SpatialDatabaseOccupiedCellsColor = Color.cyan;
    public int DebugSpatialDatabaseIndex;
    public bool DebugSpatialDatabaseCells = true;
    
    [Header("Details - Planet Navigation Grid")] 
    public Color PlanetNavGridBoundsColor = Color.cyan;
    public Color PlanetNavGridCellsColor = Color.cyan;
    public Color PlanetNavGridDirectionsColor = Color.yellow;
    public bool DebugPlanetNavGridCells = true;
    public bool DebugPlanetNavGridCellDatas = true;
    
    [Header("Details - Planets Network")] 
    public Color PlanetsNetworkColor = Color.magenta;
    
    [Header("Details - Planet Ships Assessment")] 
    public Color PlanetsShipsAssessmentColor = Color.magenta;
    
    [Header("Details - Fighter Actions")] 
    public Color FighterAttackColor = Color.red;
    public Color FighterDefendColor = Color.green;
    public Color FighterChaseColor = Color.yellow;
    
    [Header("Details - Worker Actions")] 
    public Color WorkerCaptureColor = Color.red;
    public Color WorkerBuildColor = Color.green;
    
    [Header("Details - Trader Routes")] 
    public Color TraderToGiverColor = Color.red;
    public Color TraderToReceiverColor = Color.green;
    public Color TraderCargoResourceXColor = Color.yellow;
    public Color TraderCargoResourceYColor = Color.cyan;
    public Color TraderCargoResourceZColor = Color.magenta;
    public float TraderCargoScale = 1f;

    private void OnDrawGizmos()
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        entityManager.CompleteAllTrackedJobs();

        HandleDebugRuler(entityManager);
        HandleDebugSpatialDatabase(entityManager);
        HandleDebugPlanetNavigationGrid(entityManager);
        HandleDebugPlanetsNetwork(entityManager);
        HandleDebugPlanetShipsAssessment(entityManager);
        HandleDebugFighterActions(entityManager);
        HandleDebugWorkerActions(entityManager);
        HandleDebugTraderRoutes(entityManager);
    }

    private void HandleDebugRuler(EntityManager entityManager)
    {
        if (DebugRuler)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(default, math.forward() * RulerLength);
            Gizmos.DrawWireCube(default, new float3(RulerLength) * 2f);
        }
    }

    private void HandleDebugSpatialDatabase(EntityManager entityManager)
    {
        if (DebugSpatialDatabase)
        {
            EntityQuery spatialDatabaseQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<SpatialDatabase>().Build(entityManager);

            if (spatialDatabaseQuery.CalculateEntityCount() > 0)
            {
                NativeArray<Entity> spatialDatabaseEntities =
                    spatialDatabaseQuery.ToEntityArray(Allocator.Temp);
                NativeArray<SpatialDatabase> spatialDatabases =
                    spatialDatabaseQuery.ToComponentDataArray<SpatialDatabase>(Allocator.Temp);
                
                if (DebugSpatialDatabaseIndex >= 0 && DebugSpatialDatabaseIndex < spatialDatabases.Length)
                {
                    Entity spatialDatabaseEntity = spatialDatabaseEntities[DebugSpatialDatabaseIndex];
                    SpatialDatabase spatialDatabase = spatialDatabases[DebugSpatialDatabaseIndex];
                    DynamicBuffer<SpatialDatabaseCell> cellsBuffer =
                        entityManager.GetBuffer<SpatialDatabaseCell>(spatialDatabaseEntity);
                    DynamicBuffer<SpatialDatabaseElement> elementsBuffer =
                        entityManager.GetBuffer<SpatialDatabaseElement>(spatialDatabaseEntity);

                    // Draw grid cells
                    if (DebugSpatialDatabaseCells)
                    {
                        int3 maxCoords = new int3
                        {
                            x = spatialDatabase.Grid.CellCountPerDimension,
                            y = spatialDatabase.Grid.CellCountPerDimension,
                            z = spatialDatabase.Grid.CellCountPerDimension,
                        };
                        float3 cellSize3 = new float3(spatialDatabase.Grid.CellSize);
                        float3 minCenter = spatialDatabase.Grid.BoundsMin + (spatialDatabase.Grid.CellSize * 0.5f);

                        for (int y = 0; y < maxCoords.y; y++)
                        {
                            for (int z = 0; z < maxCoords.z; z++)
                            {
                                for (int x = 0; x < maxCoords.x; x++)
                                {
                                    float3 cellCenter = minCenter + new float3
                                    {
                                        x = x * spatialDatabase.Grid.CellSize,
                                        y = y * spatialDatabase.Grid.CellSize,
                                        z = z * spatialDatabase.Grid.CellSize,
                                    };
                                    Gizmos.color = SpatialDatabaseCellsColor;
                                    Gizmos.DrawWireCube(cellCenter, cellSize3);

                                    int3 cellCoords = new int3(x, y, z);
                                    int cellIndex =
                                        UniformOriginGrid.GetCellIndexFromCoords(in spatialDatabase.Grid, cellCoords);
                                    if (cellsBuffer[cellIndex].ElementsCount > 0)
                                    {
                                        Gizmos.color = SpatialDatabaseOccupiedCellsColor;
                                        Gizmos.DrawCube(cellCenter, cellSize3);
                                    }
                                }
                            }
                        }
                    }

                    // Draw bounds
                    {
                        Gizmos.color = SpatialDatabaseBoundsColor;
                        Gizmos.DrawWireCube(default, new float3(spatialDatabase.Grid.HalfExtents) * 2f);
                    }
                }

                spatialDatabaseEntities.Dispose();
                spatialDatabases.Dispose();
            }
        }
    }

    private void HandleDebugPlanetNavigationGrid(EntityManager entityManager)
    {
        if(DebugPlanetNavigationGrid)
        {
            EntityQuery planetNavGridQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<PlanetNavigationGrid, PlanetNavigationCell>().Build(entityManager);

            if (planetNavGridQuery.HasSingleton<PlanetNavigationGrid>())
            {
                PlanetNavigationGrid planetNavGrid = planetNavGridQuery.GetSingleton<PlanetNavigationGrid>();
                DynamicBuffer<PlanetNavigationCell> planetNavCellsBuffer = planetNavGridQuery.GetSingletonBuffer<PlanetNavigationCell>();

                // Draw grid cells
                if (DebugPlanetNavGridCells)
                {
                    Gizmos.color = PlanetNavGridCellsColor;

                    int3 maxCoords = new int3
                    {
                        x = planetNavGrid.Grid.CellCountPerDimension,
                        y = planetNavGrid.Grid.CellCountPerDimension,
                        z = planetNavGrid.Grid.CellCountPerDimension,
                    };
                    float3 cellSize3 = new float3(planetNavGrid.Grid.CellSize);
                    float3 minCenter = planetNavGrid.Grid.BoundsMin + (planetNavGrid.Grid.CellSize * 0.5f);

                    for (int y = 0; y < maxCoords.y; y++)
                    {
                        for (int z = 0; z < maxCoords.z; z++)
                        {
                            for (int x = 0; x < maxCoords.x; x++)
                            {
                                float3 cellCenter = minCenter + new float3
                                {
                                    x = x * planetNavGrid.Grid.CellSize,
                                    y = y * planetNavGrid.Grid.CellSize,
                                    z = z * planetNavGrid.Grid.CellSize,
                                };
                                Gizmos.DrawWireCube(cellCenter, cellSize3);
                            }
                        }
                    }
                }
                
                // Draw cell datas
                if (DebugPlanetNavGridCellDatas)
                {
                    for (int i = 0; i < planetNavCellsBuffer.Length; i++)
                    {
                        PlanetNavigationCell cellNavigation = planetNavCellsBuffer[i];
                        int3 cellCoords = UniformOriginGrid.GetCellCoordsFromIndex(in planetNavGrid.Grid, i);
                        float3 cellCenter = UniformOriginGrid.GetCellCenter(planetNavGrid.Grid.BoundsMin, planetNavGrid.Grid.CellSize, cellCoords);

                        Gizmos.color = PlanetNavGridDirectionsColor;
                        Gizmos.DrawLine(cellCenter, cellNavigation.Position);
                    }
                }

                // Draw bounds
                {
                    Gizmos.color = PlanetNavGridBoundsColor;
                    Gizmos.DrawWireCube(default, new float3(planetNavGrid.Grid.HalfExtents) * 2f);
                }
            }
        }
    }


    private void HandleDebugPlanetsNetwork(EntityManager entityManager)
    {
        if (DebugPlanetsNetwork)
        {
            Gizmos.color = PlanetsNetworkColor;
            
            EntityQuery planetsQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Planet, LocalTransform, PlanetNetwork>().Build(entityManager);
            NativeArray<Entity> planetEntities = planetsQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < planetEntities.Length; i++)
            {
                Entity planetEntity = planetEntities[i];
                LocalTransform planetTransform = entityManager.GetComponentData<LocalTransform>(planetEntity);
                DynamicBuffer<PlanetNetwork> planetNetworkBuffer = entityManager.GetBuffer<PlanetNetwork>(planetEntity);

                for (int j = 0; j < planetNetworkBuffer.Length; j++)
                {
                    Gizmos.DrawLine(planetTransform.Position, planetNetworkBuffer[j].Position);   
                }
            }
            planetEntities.Dispose();
        }
    }


    private void HandleDebugPlanetShipsAssessment(EntityManager entityManager)
    {
        if (DebugPlanetShipsAssessment)
        {
            Gizmos.color = PlanetsShipsAssessmentColor;
            
            EntityQuery planetsQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Planet, LocalTransform, PlanetNetwork>().Build(entityManager);
            NativeArray<Entity> planetEntities = planetsQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < planetEntities.Length; i++)
            {
                Entity planetEntity = planetEntities[i];
                LocalTransform planetTransform = entityManager.GetComponentData<LocalTransform>(planetEntity);
                Planet planet = entityManager.GetComponentData<Planet>(planetEntity);
                Gizmos.DrawWireCube(planetTransform.Position, new float3(planet.ShipsAssessmentExtents) * 2f);

            }
            planetEntities.Dispose();
        }
    }

    private void HandleDebugFighterActions(EntityManager entityManager)
    {
        if (DebugFighterActions)
        {
            EntityQuery fightersQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Fighter, Ship, LocalTransform>().Build(entityManager);
            NativeArray<Entity> fighterEntities = fightersQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < fighterEntities.Length; i++)
            {
                Entity fighterEntity = fighterEntities[i];
                Ship ship = entityManager.GetComponentData<Ship>(fighterEntity);
                Fighter fighter = entityManager.GetComponentData<Fighter>(fighterEntity);
                LocalTransform transform = entityManager.GetComponentData<LocalTransform>(fighterEntity);
                
                // Chase
                if (fighter.TargetIsEnemyShip == 1)
                {
                    Gizmos.color = FighterChaseColor;
                    Gizmos.DrawLine(transform.Position, ship.NavigationTargetPosition);
                }

                // Planets
                if (ship.NavigationTargetEntity != Entity.Null)
                {
                    Team selfTeam = entityManager.GetComponentData<Team>(fighterEntity);
                    Team targetTeam = entityManager.GetComponentData<Team>(ship.NavigationTargetEntity);
                    float3 targetPosition = ship.NavigationTargetPosition;

                    // Defend
                    if (selfTeam.Index == targetTeam.Index)
                    {
                        Gizmos.color = FighterDefendColor;
                        Gizmos.DrawLine(transform.Position, targetPosition);
                    }
                    // Attack
                    else
                    {
                        Gizmos.color = FighterAttackColor;
                        Gizmos.DrawLine(transform.Position, targetPosition);
                    }
                }
            }
            fighterEntities.Dispose();
        }
    }

    private void HandleDebugWorkerActions(EntityManager entityManager)
    {
        if (DebugWorkerActions)
        {
            EntityQuery workersQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Worker, Ship, LocalTransform>().Build(entityManager);
            NativeArray<Entity> workerEntities = workersQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < workerEntities.Length; i++)
            {
                Entity workerEntity = workerEntities[i];
                Ship ship = entityManager.GetComponentData<Ship>(workerEntity);

                if (ship.NavigationTargetEntity != Entity.Null)
                {
                    LocalTransform transform = entityManager.GetComponentData<LocalTransform>(workerEntity);
                    Worker worker = entityManager.GetComponentData<Worker>(workerEntity);
                    
                    float3 targetPosition = ship.NavigationTargetPosition;

                    // Build
                    if (worker.DesiredBuildingPrefab != Entity.Null)
                    {
                        Gizmos.color = WorkerBuildColor;
                        Gizmos.DrawLine(transform.Position, targetPosition);
                    }
                    // Capture
                    else
                    {
                        Gizmos.color = WorkerCaptureColor;
                        Gizmos.DrawLine(transform.Position, targetPosition);
                    }
                }
            }
            workerEntities.Dispose();
        }
    }

    private void HandleDebugTraderRoutes(EntityManager entityManager)
    {
        if (DebugTraderActions)
        {
            EntityQuery tradersQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Trader, Ship, LocalTransform>().Build(entityManager);
            NativeArray<Entity> traderEntities = tradersQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < traderEntities.Length; i++)
            {
                Entity traderEntity = traderEntities[i];
                LocalTransform transform = entityManager.GetComponentData<LocalTransform>(traderEntity);
                Trader trader = entityManager.GetComponentData<Trader>(traderEntity);
                Ship ship = entityManager.GetComponentData<Ship>(traderEntity);

                if (ship.NavigationTargetEntity != Entity.Null && trader.ReceivingPlanetEntity != Entity.Null)
                {
                    // Giver
                    if (ship.NavigationTargetEntity != trader.ReceivingPlanetEntity)
                    {
                        LocalTransform giverTransform = entityManager.GetComponentData<LocalTransform>(ship.NavigationTargetEntity);
                        Gizmos.color = TraderToGiverColor;
                        Gizmos.DrawLine(transform.Position, giverTransform.Position);
                    }
                    
                    // Receiver
                    LocalTransform receiverTransform = entityManager.GetComponentData<LocalTransform>(trader.ReceivingPlanetEntity);
                    Gizmos.color = TraderToReceiverColor;
                    Gizmos.DrawLine(transform.Position, receiverTransform.Position);
                    
                    // Cargo
                    float cargoRatio = math.csum(trader.CarriedResources) /
                                        trader.TraderData.Value.ResourceCarryCapacity;
                    bool3 cargoMask = trader.ChosenResourceMask > new float3(0.5f);
                    Gizmos.color = Color.white;
                    if (cargoMask.x)
                    {
                        Gizmos.color = TraderCargoResourceXColor;
                    }
                    else if (cargoMask.y)
                    {
                        Gizmos.color = TraderCargoResourceYColor;
                    }
                    else if (cargoMask.z)
                    {
                        Gizmos.color = TraderCargoResourceZColor;
                    }
                    
                    Gizmos.DrawWireCube(transform.Position, new float3(TraderCargoScale));
                    Gizmos.DrawCube(transform.Position, new float3(TraderCargoScale, cargoRatio * TraderCargoScale, TraderCargoScale));
                    
                }
            }
            traderEntities.Dispose();
        }
    }
}
