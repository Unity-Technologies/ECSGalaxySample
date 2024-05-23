using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class SpatialDatabaseAuthoring : MonoBehaviour
{
    public float HalfExtents = 100f;
    public int Subdivisions = 3;
    public int InitialCellCapacity = 256;

    public bool DebugCells = false;

    class Baker : Baker<SpatialDatabaseAuthoring>
    {
        public override void Bake(SpatialDatabaseAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.None);

            SpatialDatabase spatialDatabase = new SpatialDatabase();
            DynamicBuffer<SpatialDatabaseCell> cellsBuffer = AddBuffer<SpatialDatabaseCell>(entity);
            DynamicBuffer<SpatialDatabaseElement> elementsBuffer = AddBuffer<SpatialDatabaseElement>(entity);

            SpatialDatabase.Initialize(authoring.HalfExtents, authoring.Subdivisions, authoring.InitialCellCapacity,
                ref spatialDatabase, ref cellsBuffer, ref elementsBuffer);
            AddComponent(entity, spatialDatabase);
        }
    }

    private void OnDrawGizmosSelected()
    {
        UniformOriginGrid grid = new UniformOriginGrid(HalfExtents, Subdivisions);

        // Draw grid cells
        if (DebugCells)
        {
            Color col = Color.cyan;
            float colMultiplier = 0.3f;
            col.r *= colMultiplier;
            col.g *= colMultiplier;
            col.b *= colMultiplier;
            Gizmos.color = col;

            int3 maxCoords = new int3
            {
                x = grid.CellCountPerDimension,
                y = grid.CellCountPerDimension,
                z = grid.CellCountPerDimension,
            };
            float3 cellSize3 = new float3(grid.CellSize);
            float3 minCenter = grid.BoundsMin + (grid.CellSize * 0.5f);

            for (int y = 0; y < maxCoords.y; y++)
            {
                for (int z = 0; z < maxCoords.z; z++)
                {
                    for (int x = 0; x < maxCoords.x; x++)
                    {
                        float3 cellCenter = minCenter + new float3
                        {
                            x = x * grid.CellSize,
                            y = y * grid.CellSize,
                            z = z * grid.CellSize,
                        };
                        Gizmos.DrawWireCube(cellCenter, cellSize3);
                    }
                }
            }
        }

        // Draw bounds
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(default, new float3(grid.HalfExtents) * 2f);
        }
    }
}
