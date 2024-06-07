using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Logging;

public interface ISpatialQueryCollector
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnVisitCell(in SpatialDatabaseCell cell, in UnsafeList<SpatialDatabaseElement> elements, out bool shouldEarlyExit);
}

[InternalBufferCapacity(0)]
public struct SpatialDatabaseCell : IBufferElementData
{
    public int StartIndex;
    public int ElementsCount;
    public int ElementsCapacity;
    public int ExcessElementsCount;
}

[InternalBufferCapacity(0)]
public struct SpatialDatabaseElement : IBufferElementData
{
    public Entity Entity;
    public float3 Position;
    public byte Team;
    public byte Type;
}

public struct SpatialDatabaseCellIndex : IComponentData
{
    public int CellIndex;
}

public unsafe struct CachedSpatialDatabase
{
    public Entity SpatialDatabaseEntity;
    public ComponentLookup<SpatialDatabase> SpatialDatabaseLookup;
    public BufferLookup<SpatialDatabaseCell> CellsBufferLookup;
    public BufferLookup<SpatialDatabaseElement> ElementsBufferLookup;

    public bool _IsInitialized;
    public SpatialDatabase _SpatialDatabase;
    public UnsafeList<SpatialDatabaseCell> _SpatialDatabaseCells;
    public UnsafeList<SpatialDatabaseElement> _SpatialDatabaseElements;

    public void CacheData()
    {
        if (!_IsInitialized)
        {
            _SpatialDatabase = SpatialDatabaseLookup[SpatialDatabaseEntity];
            DynamicBuffer<SpatialDatabaseCell> cellsBuffer = CellsBufferLookup[SpatialDatabaseEntity];
            DynamicBuffer<SpatialDatabaseElement> elementsBuffer = ElementsBufferLookup[SpatialDatabaseEntity];
            _SpatialDatabaseCells = new UnsafeList<SpatialDatabaseCell>((SpatialDatabaseCell*)cellsBuffer.GetUnsafePtr(), cellsBuffer.Length);
            _SpatialDatabaseElements = new UnsafeList<SpatialDatabaseElement>((SpatialDatabaseElement*)elementsBuffer.GetUnsafePtr(), elementsBuffer.Length);
            _IsInitialized = true;
        }
    }
}

public unsafe struct CachedSpatialDatabaseUnsafe
{
    public Entity SpatialDatabaseEntity;
    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public ComponentLookup<SpatialDatabase> SpatialDatabaseLookup;
    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public BufferLookup<SpatialDatabaseCell> CellsBufferLookup;
    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public BufferLookup<SpatialDatabaseElement> ElementsBufferLookup;

    public bool _IsInitialized;
    public SpatialDatabase _SpatialDatabase;
    public UnsafeList<SpatialDatabaseCell> _SpatialDatabaseCells;
    public UnsafeList<SpatialDatabaseElement> _SpatialDatabaseElements;

    public void CacheData()
    {
        if (!_IsInitialized)
        {
            _SpatialDatabase = SpatialDatabaseLookup[SpatialDatabaseEntity];
            DynamicBuffer<SpatialDatabaseCell> cellsBuffer = CellsBufferLookup[SpatialDatabaseEntity];
            DynamicBuffer<SpatialDatabaseElement> elementsBuffer = ElementsBufferLookup[SpatialDatabaseEntity];
            _SpatialDatabaseCells = new UnsafeList<SpatialDatabaseCell>((SpatialDatabaseCell*)cellsBuffer.GetUnsafePtr(), cellsBuffer.Length);
            _SpatialDatabaseElements = new UnsafeList<SpatialDatabaseElement>((SpatialDatabaseElement*)elementsBuffer.GetUnsafePtr(), elementsBuffer.Length);
            _IsInitialized = true;
        }
    }
}

public unsafe struct CachedSpatialDatabaseRO
{
    public Entity SpatialDatabaseEntity;
    [ReadOnly]
    public ComponentLookup<SpatialDatabase> SpatialDatabaseLookup;
    [ReadOnly]
    public BufferLookup<SpatialDatabaseCell> CellsBufferLookup;
    [ReadOnly]
    public BufferLookup<SpatialDatabaseElement> ElementsBufferLookup;

    public bool _IsInitialized;
    public SpatialDatabase _SpatialDatabase;
    public UnsafeList<SpatialDatabaseCell> _SpatialDatabaseCells;
    public UnsafeList<SpatialDatabaseElement> _SpatialDatabaseElements;

    public void CacheData()
    {
        if (!_IsInitialized)
        {
            _SpatialDatabase = SpatialDatabaseLookup[SpatialDatabaseEntity];
            DynamicBuffer<SpatialDatabaseCell> cellsBuffer = CellsBufferLookup[SpatialDatabaseEntity];
            DynamicBuffer<SpatialDatabaseElement> elementsBuffer = ElementsBufferLookup[SpatialDatabaseEntity];
            _SpatialDatabaseCells = new UnsafeList<SpatialDatabaseCell>((SpatialDatabaseCell*)cellsBuffer.GetUnsafeReadOnlyPtr(), cellsBuffer.Length);
            _SpatialDatabaseElements = new UnsafeList<SpatialDatabaseElement>((SpatialDatabaseElement*)elementsBuffer.GetUnsafeReadOnlyPtr(), elementsBuffer.Length);
            _IsInitialized = true;
        }
    }
}

public struct SpatialDatabase : IComponentData
{
    public UniformOriginGrid Grid;

    public const float ElementsCapacityGrowFactor = 2f;

    public static void Initialize(float halfExtents, int subdivisions, int cellEntriesCapacity,
        ref SpatialDatabase spatialDatabase, ref DynamicBuffer<SpatialDatabaseCell> cellsBuffer,
        ref DynamicBuffer<SpatialDatabaseElement> storageBuffer)
    {
        // Clear
        cellsBuffer.Clear();
        storageBuffer.Clear();
        cellsBuffer.Capacity = 16;
        storageBuffer.Capacity = 16;

        // Init grid
        spatialDatabase.Grid = new UniformOriginGrid(halfExtents, subdivisions);

        // Reallocate 
        cellsBuffer.Resize(spatialDatabase.Grid.CellCount, NativeArrayOptions.ClearMemory);
        storageBuffer.Resize(spatialDatabase.Grid.CellCount * cellEntriesCapacity, NativeArrayOptions.ClearMemory);

        // Init cells data
        for (int i = 0; i < cellsBuffer.Length; i++)
        {
            SpatialDatabaseCell cell = cellsBuffer[i];
            cell.StartIndex = i * cellEntriesCapacity;
            cell.ElementsCount = 0;
            cell.ElementsCapacity = cellEntriesCapacity;
            cell.ExcessElementsCount = 0;
            cellsBuffer[i] = cell;
        }
    }

    public static void ClearAndResize(ref DynamicBuffer<SpatialDatabaseCell> cellsBuffer,
        ref DynamicBuffer<SpatialDatabaseElement> storageBuffer)
    {
        int totalDesiredStorage = 0;
        for (int i = 0; i < cellsBuffer.Length; i++)
        {
            SpatialDatabaseCell cell = cellsBuffer[i];
            cell.StartIndex = totalDesiredStorage;

            // Handle calculating an increased max storage for this cell
            cell.ElementsCapacity = math.select(cell.ElementsCapacity,
                (int)math.ceil((cell.ElementsCapacity + cell.ExcessElementsCount) * ElementsCapacityGrowFactor),
                cell.ExcessElementsCount > 0);
            totalDesiredStorage += cell.ElementsCapacity;

            // Reset storage
            cell.ElementsCount = 0;
            cell.ExcessElementsCount = 0;

            cellsBuffer[i] = cell;
        }

        storageBuffer.Resize(totalDesiredStorage, NativeArrayOptions.ClearMemory);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddToDataBase(in SpatialDatabase spatialDatabase,
        ref UnsafeList<SpatialDatabaseCell> cellsBuffer, ref UnsafeList<SpatialDatabaseElement> storageBuffer,
        in SpatialDatabaseElement element)
    {
        int cellIndex = UniformOriginGrid.GetCellIndex(in spatialDatabase.Grid, element.Position);
        if (cellIndex >= 0)
        {
            SpatialDatabaseCell cell = cellsBuffer[cellIndex];

            // Check capacity
            if (cell.ElementsCount + 1 > cell.ElementsCapacity)
            {
                // Remember excess count for resizing next time we clear
                cell.ExcessElementsCount++;
            }
            else
            {
                // Add entry at cell index
                storageBuffer[cell.StartIndex + cell.ElementsCount] = element;
                cell.ElementsCount++;
            }

            cellsBuffer[cellIndex] = cell;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddToDataBase(in SpatialDatabase spatialDatabase,
        ref UnsafeList<SpatialDatabaseCell> cellsBuffer, ref UnsafeList<SpatialDatabaseElement> storageBuffer,
        in SpatialDatabaseElement element, int cellIndex)
    {
        if (cellIndex >= 0)
        {
            SpatialDatabaseCell cell = cellsBuffer[cellIndex];

            // Check capacity
            if (cell.ElementsCount + 1 > cell.ElementsCapacity)
            {
                // Remember excess count for resizing next time we clear
                cell.ExcessElementsCount++;
            }
            else
            {
                // Add entry at cell index
                storageBuffer[cell.StartIndex + cell.ElementsCount] = element;
                cell.ElementsCount++;
            }

            cellsBuffer[cellIndex] = cell;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe static void QueryAABB<T>(in SpatialDatabase spatialDatabase,
        in DynamicBuffer<SpatialDatabaseCell> cellsBuffer, in DynamicBuffer<SpatialDatabaseElement> elementsBuffer,
        float3 center, float3 halfExtents, ref T collector)
        where T : unmanaged, ISpatialQueryCollector
    {
        UnsafeList<SpatialDatabaseCell> cells =
            new UnsafeList<SpatialDatabaseCell>((SpatialDatabaseCell*)cellsBuffer.GetUnsafeReadOnlyPtr(),
                cellsBuffer.Length);
        UnsafeList<SpatialDatabaseElement> elements =
            new UnsafeList<SpatialDatabaseElement>((SpatialDatabaseElement*)elementsBuffer.GetUnsafeReadOnlyPtr(),
                elementsBuffer.Length);
        QueryAABB(in spatialDatabase, in cells, in elements, center, halfExtents, ref collector);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void QueryAABB<T>(in SpatialDatabase spatialDatabase,
        in UnsafeList<SpatialDatabaseCell> cellsBuffer, in UnsafeList<SpatialDatabaseElement> elementsBuffer,
        float3 center, float3 halfExtents, ref T collector)
        where T : unmanaged, ISpatialQueryCollector
    {
        float3 aabbMin = center - halfExtents;
        float3 aabbMax = center + halfExtents;
        UniformOriginGrid grid = spatialDatabase.Grid;
        if (UniformOriginGrid.GetAABBMinMaxCoords(in grid, aabbMin, aabbMax, out int3 minCoords,
                out int3 maxCoords))
        {
            for (int y = minCoords.y; y <= maxCoords.y; y++)
            {
                for (int z = minCoords.z; z <= maxCoords.z; z++)
                {
                    for (int x = minCoords.x; x <= maxCoords.x; x++)
                    {
                        int3 coords = new int3(x, y, z);
                        int cellIndex = UniformOriginGrid.GetCellIndexFromCoords(in grid, coords);
                        SpatialDatabaseCell cell = cellsBuffer[cellIndex];
                        collector.OnVisitCell(in cell, in elementsBuffer,
                            out bool shouldEarlyExit);
                        if (shouldEarlyExit)
                        {
                            return;
                        }
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe static void QueryAABBCellProximityOrder<T>(in SpatialDatabase spatialDatabase,
        in DynamicBuffer<SpatialDatabaseCell> cellsBuffer, in DynamicBuffer<SpatialDatabaseElement> elementsBuffer,
        float3 center, float3 halfExtents, ref T collector)
        where T : unmanaged, ISpatialQueryCollector
    {
        UnsafeList<SpatialDatabaseCell> cells =
            new UnsafeList<SpatialDatabaseCell>((SpatialDatabaseCell*)cellsBuffer.GetUnsafeReadOnlyPtr(),
                cellsBuffer.Length);
        UnsafeList<SpatialDatabaseElement> elements =
            new UnsafeList<SpatialDatabaseElement>((SpatialDatabaseElement*)elementsBuffer.GetUnsafeReadOnlyPtr(),
                elementsBuffer.Length);
        QueryAABBCellProximityOrder(in spatialDatabase, in cells, in elements, center, halfExtents, ref collector);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void QueryAABBCellProximityOrder<T>(in SpatialDatabase spatialDatabase,
        in UnsafeList<SpatialDatabaseCell> cellsBuffer, in UnsafeList<SpatialDatabaseElement> elementsBuffer,
        float3 center, float3 halfExtents, ref T collector)
        where T : unmanaged, ISpatialQueryCollector
    {
        float3 aabbMin = center - halfExtents;
        float3 aabbMax = center + halfExtents;
        UniformOriginGrid grid = spatialDatabase.Grid;
        if (UniformOriginGrid.GetAABBMinMaxCoords(in grid, aabbMin, aabbMax, out int3 minCoords, out int3 maxCoords))
        {
            int3 sourceCoord = UniformOriginGrid.GetCellCoordsFromPosition(in grid, center);
            int3 highestCoordDistances = math.max(maxCoords - sourceCoord, sourceCoord - minCoords);
            int maxLayer = math.max(highestCoordDistances.x,
                math.max(highestCoordDistances.y, highestCoordDistances.z));

            // Iterate layers of cells around the original cell
            for (int l = 0; l <= maxLayer; l++)
            {
                int2 yRange = new int2(sourceCoord.y - l, sourceCoord.y + l);
                int2 zRange = new int2(sourceCoord.z - l, sourceCoord.z + l);
                int2 xRange = new int2(sourceCoord.x - l, sourceCoord.x + l);

                for (int y = yRange.x; y <= yRange.y; y++)
                {
                    int yDistToEdge = math.min(y - minCoords.y, maxCoords.y - y); // positive is inside

                    // Skip coords outside of query coords range
                    if (yDistToEdge < 0)
                    {
                        continue;
                    }

                    for (int z = zRange.x; z <= zRange.y; z++)
                    {
                        int zDistToEdge = math.min(z - minCoords.z, maxCoords.z - z); // positive is inside

                        // Skip coords outside of query coords range
                        if (zDistToEdge < 0)
                        {
                            continue;
                        }

                        for (int x = xRange.x; x <= xRange.y; x++)
                        {
                            int xDistToEdge = math.min(x - minCoords.x, maxCoords.x - x); // positive is inside

                            // Skip coords outside of query coords range
                            if (xDistToEdge < 0)
                            {
                                continue;
                            }

                            int3 coords = new int3(x, y, z);
                            int3 coordDistToCenter = math.abs(coords - sourceCoord);
                            int maxCoordsDist = math.max(coordDistToCenter.x,
                                math.max(coordDistToCenter.y, coordDistToCenter.z));

                            // Skip all inner coords not belonging to the external layer
                            if (maxCoordsDist != l)
                            {
                                x = xRange.y - 1;
                                continue;
                            }

                            int cellIndex = UniformOriginGrid.GetCellIndexFromCoords(in grid, coords);
                            SpatialDatabaseCell cell = cellsBuffer[cellIndex];
                            collector.OnVisitCell(in cell, in elementsBuffer,
                                out bool shouldEarlyExit);
                            if (shouldEarlyExit)
                            {
                                return;
                            }
                        }
                    }
                }
            }
        }
    }
}