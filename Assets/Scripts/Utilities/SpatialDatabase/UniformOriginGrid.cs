using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;


public struct UniformOriginGrid
{
    public int Subdivisions;
    public float HalfExtents;
    
    public int CellCount;
    public int CellCountPerDimension;
    public int CellCountPerPlane;
    public float CellSize;
    public float3 BoundsMin;
    public float3 BoundsMax;

    public UniformOriginGrid(float halfExtents, int subdivisions)
    {
        Subdivisions = subdivisions;
        HalfExtents = halfExtents;
        
        CellCount = (int)math.pow(8f, subdivisions);
        CellCountPerDimension = (int)math.pow(2f, subdivisions);
        CellCountPerPlane = CellCountPerDimension * CellCountPerDimension;
        CellSize = (halfExtents * 2f) / (float)CellCountPerDimension;
        BoundsMin = new float3(-halfExtents);
        BoundsMax = new float3(halfExtents);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSubdivisionLevelForMaxCellSize(float halfExtents, float maxCellSize, int maxSubdivisionLevel = 5)
    {
        for (int s = 1; s <= maxSubdivisionLevel; s++)
        {
            int cellCountPerDimension = (int)math.pow(2f, s);
            float cellSize = (halfExtents * 2f) / (float)cellCountPerDimension;
            if (cellSize < maxCellSize)
            {
                return s;
            }
        }
        return maxSubdivisionLevel;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInBounds(in UniformOriginGrid grid, float3 position)
    {
        return position.x > grid.BoundsMin.x &&
               position.x < grid.BoundsMax.x &&
               position.y > grid.BoundsMin.y &&
               position.y < grid.BoundsMax.y &&
               position.z > grid.BoundsMin.z &&
               position.z < grid.BoundsMax.z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int3 GetCellCoordsFromPosition(in UniformOriginGrid grid, float3 position)
    {
        float3 localPos = position - grid.BoundsMin;
        int3 cellCoords = new int3
        {
            x = (int)math.floor(localPos.x / grid.CellSize),
            y = (int)math.floor(localPos.y / grid.CellSize),
            z = (int)math.floor(localPos.z / grid.CellSize),
        };
        cellCoords = math.clamp(cellCoords, int3.zero, new int3(grid.CellCountPerDimension - 1));
        return cellCoords;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int3 GetCellCoordsFromIndex(in UniformOriginGrid grid, int index)
    {
        return new int3
        {
            x = index % grid.CellCountPerDimension,
            y = index / grid.CellCountPerPlane,
            z = (index % grid.CellCountPerPlane) / grid.CellCountPerDimension,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetCellIndex(in UniformOriginGrid grid, float3 position)
    {
        if (IsInBounds(in grid, position))
        {
            int3 cellCoords = GetCellCoordsFromPosition(in grid, position);
            return GetCellIndexFromCoords(in grid, cellCoords);
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetCellIndexFromCoords(in UniformOriginGrid grid, int3 coords)
    {
        return (coords.x) +
                (coords.z * grid.CellCountPerDimension) +
                (coords.y * grid.CellCountPerPlane);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AABBIntersectAABB(float3 aabb1Min, float3 aabb1Max, float3 aabb2Min, float3 aabb2Max)
    {
        return (aabb1Min.x <= aabb2Max.x && aabb1Max.x >= aabb2Min.x) &&
               (aabb1Min.y <= aabb2Max.y && aabb1Max.y >= aabb2Min.y) &&
               (aabb1Min.z <= aabb2Max.z && aabb1Max.z >= aabb2Min.z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 GetCellCenter(float3 spatialDatabaseBoundsMin, float cellSize, int3 cellCoords)
    {
        float3 minCenter = spatialDatabaseBoundsMin + new float3(cellSize * 0.5f);
        return minCenter + ((float3)cellCoords * cellSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetDistanceSqAABBToPoint(float3 point, float3 aabbMin, float3 aabbMax)
    {
        float3 pointOnBounds = math.clamp(point, aabbMin, aabbMax);
        return math.lengthsq(pointOnBounds - point);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetAABBMinMaxCoords(in UniformOriginGrid grid, float3 aabbMin, float3 aabbMax, out int3 minCoords, out int3 maxCoords)
    {
        if (AABBIntersectAABB(aabbMin, aabbMax, grid.BoundsMin, grid.BoundsMax))
        {
            // Clamp to bounds
            aabbMin = math.clamp(aabbMin, grid.BoundsMin, grid.BoundsMax);
            aabbMax = math.clamp(aabbMax, grid.BoundsMin, grid.BoundsMax);

            // Get min max coords
            minCoords = GetCellCoordsFromPosition(in grid, aabbMin);
            maxCoords = GetCellCoordsFromPosition(in grid, aabbMax);

            return true;
        }

        minCoords = new int3(-1);
        maxCoords = new int3(-1);
        return false;
    }
}