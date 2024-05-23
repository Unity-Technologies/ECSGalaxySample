using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct PlanetNavigationGrid : IComponentData
{
    public UniformOriginGrid Grid;
}

public struct PlanetNavigationCell : IBufferElementData
{
    public Entity Entity;
    public float3 Position;
    public float Radius;
}

public struct PlanetNavigationBuildData
{
    public Entity Entity;
    public float3 Position;
    public float Radius;
}

public static class PlanetNavigationGridUtility
{
    public static Entity CreatePlanetNavigationGrid(EntityCommandBuffer ecb, NativeList<PlanetNavigationBuildData> planetDatas, float halfExtents, int subdivisions)
    {
        Entity gridEntity = ecb.CreateEntity();

        // Grid
        PlanetNavigationGrid planetNavigationGrid = new PlanetNavigationGrid
        {
            Grid = new UniformOriginGrid(halfExtents, subdivisions),
        };
        ecb.AddComponent(gridEntity, planetNavigationGrid);
        
        // Cells
        DynamicBuffer<PlanetNavigationCell> cellsBuffer = ecb.AddBuffer<PlanetNavigationCell>(gridEntity);
        cellsBuffer.Resize(planetNavigationGrid.Grid.CellCount, NativeArrayOptions.ClearMemory);
        for (int c = 0; c < cellsBuffer.Length; c++)
        {
            int3 cellCoords = UniformOriginGrid.GetCellCoordsFromIndex(in planetNavigationGrid.Grid, c);
            float3 cellCenter = UniformOriginGrid.GetCellCenter(planetNavigationGrid.Grid.BoundsMin, planetNavigationGrid.Grid.CellSize, cellCoords);

            PlanetNavigationCell closestPlanetData = new PlanetNavigationCell
            {
                Position = float3.zero,
                Radius = 0f,
                Entity = Entity.Null,
            };
            
            // Find the closest planet
            float closestDistance = float.MaxValue;
            for (int p = 0; p < planetDatas.Length; p++)
            {
                PlanetNavigationBuildData planetData = planetDatas[p];
                float3 cellToPlanet = planetData.Position - cellCenter;
                float cellToPlanetSurfaceDistance = math.length(cellToPlanet) - planetData.Radius;
                if (cellToPlanetSurfaceDistance < closestDistance)
                {
                    closestPlanetData.Position = planetData.Position;
                    closestPlanetData.Radius = planetData.Radius;
                    closestPlanetData.Entity = planetData.Entity;

                    closestDistance = cellToPlanetSurfaceDistance;
                }
            }
            
            // Write info to cell
            cellsBuffer[c] = closestPlanetData;
        }
        
        return gridEntity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetCellDataAtPosition(in PlanetNavigationGrid navigationGrid, in DynamicBuffer<PlanetNavigationCell> cellsBuffer, float3 position, out PlanetNavigationCell cellNavigation)
    {
        if (UniformOriginGrid.IsInBounds(in navigationGrid.Grid, position))
        {
            int3 cellCoords = UniformOriginGrid.GetCellCoordsFromPosition(in navigationGrid.Grid, position);
            int cellIndex = UniformOriginGrid.GetCellIndexFromCoords(in navigationGrid.Grid, cellCoords);
            cellNavigation = cellsBuffer[cellIndex];
            return true;
        }

        cellNavigation = default;
        return false;
    }
}