using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Galaxy
{
    /// <summary>
    /// System to handle planet selection
    /// </summary>
    public partial struct PlanetSelectionSystem : ISystem
    {
        private Entity m_SelectedPlanet;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MainCamera>();
            state.RequireForUpdate<Planet>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // TODO: Mobile support
            if (Cursor.visible && Input.GetMouseButton(0))
            {
                Entity tempEntity = Entity.Null;
                
                Camera mainCamera = Camera.main;
                if (mainCamera == null)
                    return;

                float3 cameraPosition = mainCamera.transform.position;
                float3 mousePosition = Input.mousePosition;
                Ray rayToMouse = mainCamera.ScreenPointToRay(mousePosition);
                float minDistance = float.PositiveInfinity;

                foreach (var (planetTransform, entity) in
                         SystemAPI.Query<RefRO<LocalTransform>>()
                             .WithAll<Planet>()
                             .WithEntityAccess())
                {
                    Bounds bounds = new Bounds(planetTransform.ValueRO.Position, new float3(planetTransform.ValueRO.Scale));
                    if (bounds.IntersectRay(rayToMouse))
                    {
                        float distance = math.distance(cameraPosition, planetTransform.ValueRO.Position);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            tempEntity = entity;
                        }
                    }
                }
                
                m_SelectedPlanet = tempEntity;
            }

            // Update selection
            if (m_SelectedPlanet == Entity.Null)
            {
                UIEvents.UpdatePlanetSelection?.Invoke(new StatsData {Visible = false});
            }
            else
            {
                var planetPosition = SystemAPI.GetComponent<LocalTransform>(m_SelectedPlanet).Position;
                var planetData = SystemAPI.GetComponent<Planet>(m_SelectedPlanet);
                var team = SystemAPI.GetComponent<Team>(m_SelectedPlanet);
                
                UIEvents.UpdatePlanetSelection?.Invoke(new StatsData
                {
                    Visible = true,
                    TargetPosition = planetPosition,
                    PlanetData = new PlanetData
                    {
                        TeamIndex = team.Index,
                        ResourceCurrentStorage = planetData.ResourceCurrentStorage,
                        ResourceMaxStorage = planetData.ResourceMaxStorage,
                        ResourceGenerationRate = planetData.ResourceGenerationRate,
                        ConversionTime = planetData.CaptureTime,
                        ConversionProgress = planetData.CaptureProgress
                    }
                });
            }
        }
    }
}