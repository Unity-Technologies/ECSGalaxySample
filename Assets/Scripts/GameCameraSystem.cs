using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Galaxy
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TransformSystemGroup))]
    [UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial struct GameCameraSystem : ISystem
    {
        private Unity.Mathematics.Random _random;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _random = Unity.Mathematics.Random.CreateFromIndex(0);
            
            state.RequireForUpdate<SimulationRate>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<GameCamera>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Collect input
            CameraInputs cameraInputs = new CameraInputs
            {
                Move = new float3(
                    (Input.GetKey(KeyCode.D) ? 1f : 0f) + (Input.GetKey(KeyCode.A) ? -1f : 0f),
                    (Input.GetKey(KeyCode.E) ? 1f : 0f) + (Input.GetKey(KeyCode.Q) ? -1f : 0f),
                    (Input.GetKey(KeyCode.W) ? 1f : 0f) + (Input.GetKey(KeyCode.S) ? -1f : 0f)),
                Look = new float2(
                    Input.GetAxis("Mouse X"),
                    Input.GetAxis("Mouse Y")),
                Zoom = -Input.mouseScrollDelta.y,
                Sprint = Input.GetKey(KeyCode.LeftShift),
                SwitchMode = Input.GetKeyDown(KeyCode.Z),
            };
            cameraInputs.Move = math.normalizesafe(cameraInputs.Move) *
                                math.saturate(math.length(cameraInputs.Move)); // Clamp move inputs magnitude to 1

            // Camera target switching
            Entity nextTargetPlanet = Entity.Null;
            Entity nextTargetShip = Entity.Null;
            bool switchShip = Input.GetKeyDown(KeyCode.X);
            if (cameraInputs.SwitchMode || switchShip)
            {
                EntityQuery planetsQuery = SystemAPI.QueryBuilder().WithAll<Planet>().Build();
                NativeArray<Entity> planetEntities = planetsQuery.ToEntityArray(Allocator.Temp);
                if (planetEntities.Length > 0)
                {
                    nextTargetPlanet = planetEntities[_random.NextInt(planetEntities.Length)];
                }

                planetEntities.Dispose();

                EntityQuery shipsQuery = SystemAPI.QueryBuilder().WithAll<Ship>().Build();
                NativeArray<Entity> shipEntities = shipsQuery.ToEntityArray(Allocator.Temp);
                if (shipEntities.Length > 0)
                {
                    nextTargetShip = shipEntities[_random.NextInt(shipEntities.Length)];
                }

                shipEntities.Dispose();
            }

            GameCameraJob job = new GameCameraJob
            {
                DeltaTime = SystemAPI.GetSingleton<SimulationRate>().UnscaledDeltaTime,
                CameraInputs = cameraInputs,
                NextTargetPlanet = nextTargetPlanet,
                NextTargetShip = nextTargetShip,
                LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(false),
            };
            job.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        public partial struct GameCameraJob : IJobEntity
        {
            public float DeltaTime;
            public CameraInputs CameraInputs;
            public Entity NextTargetPlanet;
            public Entity NextTargetShip;

            public ComponentLookup<LocalToWorld> LocalToWorldLookup;

            void Execute(Entity entity, ref LocalTransform transform, ref GameCamera gameCamera)
            {
                if (gameCamera.IgnoreInput)
                    return;

                // Mode switch
                if (CameraInputs.SwitchMode)
                {
                    switch (gameCamera.CameraMode)
                    {
                        case GameCamera.Mode.Fly:
                            gameCamera.CameraMode = GameCamera.Mode.OrbitPlanet;
                            break;
                        case GameCamera.Mode.OrbitPlanet:
                            gameCamera.CameraMode = GameCamera.Mode.OrbitShip;
                            break;
                        case GameCamera.Mode.OrbitShip:
                            gameCamera.CameraMode = GameCamera.Mode.Fly;
                            break;
                    }
                }

                // Target switch
                if (NextTargetPlanet != Entity.Null)
                {
                    switch (gameCamera.CameraMode)
                    {
                        case GameCamera.Mode.OrbitPlanet:
                            gameCamera.FollowedEntity = NextTargetPlanet;
                            break;
                        case GameCamera.Mode.OrbitShip:
                            gameCamera.FollowedEntity = NextTargetShip;
                            break;
                    }
                }

                switch (gameCamera.CameraMode)
                {
                    case GameCamera.Mode.Fly:
                    {
                        // Yaw
                        float yawAngleChange = CameraInputs.Look.x * gameCamera.FlyRotationSpeed;
                        quaternion yawRotation = quaternion.Euler(math.up() * math.radians(yawAngleChange));
                        gameCamera.PlanarForward = math.mul(yawRotation, gameCamera.PlanarForward);

                        // Pitch
                        gameCamera.PitchAngle += -CameraInputs.Look.y * gameCamera.FlyRotationSpeed;
                        gameCamera.PitchAngle = math.clamp(gameCamera.PitchAngle, gameCamera.MinVAngle,
                            gameCamera.MaxVAngle);
                        quaternion pitchRotation = quaternion.Euler(math.right() * math.radians(gameCamera.PitchAngle));

                        // Final rotation
                        quaternion targetRotation =
                            math.mul(quaternion.LookRotationSafe(gameCamera.PlanarForward, math.up()), pitchRotation);
                        transform.Rotation = math.slerp(transform.Rotation, targetRotation,
                            MathUtilities.GetSharpnessInterpolant(gameCamera.FlyRotationSharpness, DeltaTime));

                        // Move
                        float3 worldMoveInputs = math.rotate(transform.Rotation, CameraInputs.Move);
                        float finalMaxSpeed = gameCamera.FlyMaxMoveSpeed;
                        if (CameraInputs.Sprint)
                        {
                            finalMaxSpeed *= gameCamera.FlySprintSpeedBoost;
                        }

                        gameCamera.CurrentMoveVelocity = math.lerp(gameCamera.CurrentMoveVelocity,
                            worldMoveInputs * finalMaxSpeed,
                            MathUtilities.GetSharpnessInterpolant(gameCamera.FlyMoveSharpness, DeltaTime));
                        transform.Position += gameCamera.CurrentMoveVelocity * DeltaTime;

                        break;
                    }
                    case GameCamera.Mode.OrbitPlanet:
                    case GameCamera.Mode.OrbitShip:
                    {
                        // if there is a followed entity, place the camera relatively to it
                        if (LocalToWorldLookup.TryGetComponent(gameCamera.FollowedEntity, out LocalToWorld followedLTW))
                        {
                            // Rotation
                            {
                                transform.Rotation = quaternion.LookRotationSafe(gameCamera.PlanarForward, math.up());

                                // Yaw
                                float yawAngleChange = CameraInputs.Look.x * gameCamera.OrbitRotationSpeed;
                                quaternion yawRotation = quaternion.Euler(math.up() * math.radians(yawAngleChange));
                                gameCamera.PlanarForward = math.rotate(yawRotation, gameCamera.PlanarForward);

                                // Pitch
                                gameCamera.PitchAngle += -CameraInputs.Look.y * gameCamera.OrbitRotationSpeed;
                                gameCamera.PitchAngle = math.clamp(gameCamera.PitchAngle, gameCamera.MinVAngle,
                                    gameCamera.MaxVAngle);
                                quaternion pitchRotation =
                                    quaternion.Euler(math.right() * math.radians(gameCamera.PitchAngle));

                                // Final rotation
                                transform.Rotation = quaternion.LookRotationSafe(gameCamera.PlanarForward, math.up());
                                transform.Rotation = math.mul(transform.Rotation, pitchRotation);
                            }

                            float3 cameraForward = math.mul(transform.Rotation, math.forward());

                            // Distance input
                            float desiredDistanceMovementFromInput =
                                CameraInputs.Zoom * gameCamera.OrbitDistanceMovementSpeed;
                            gameCamera.OrbitTargetDistance =
                                math.clamp(gameCamera.OrbitTargetDistance + desiredDistanceMovementFromInput,
                                    gameCamera.OrbitMinDistance, gameCamera.OrbitMaxDistance);
                            gameCamera.CurrentDistanceFromMovement = math.lerp(gameCamera.CurrentDistanceFromMovement,
                                gameCamera.OrbitTargetDistance,
                                MathUtilities.GetSharpnessInterpolant(gameCamera.OrbitDistanceMovementSharpness,
                                    DeltaTime));

                            // Calculate final camera position from targetposition + rotation + distance
                            transform.Position = followedLTW.Position +
                                                 (-cameraForward * gameCamera.CurrentDistanceFromMovement);
                        }

                        break;
                    }
                    case GameCamera.Mode.None:
                        break;
                }

                // Manually calculate the LocalToWorld since this is updating after the Transform systems, and the LtW is what rendering uses
                LocalToWorld cameraLocalToWorld = new LocalToWorld();
                cameraLocalToWorld.Value = new float4x4(transform.Rotation, transform.Position);
                LocalToWorldLookup[entity] = cameraLocalToWorld;
            }
        }
    }

    public partial struct IgnoreCameraSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<IgnoreCameraInputRequest>();
            state.RequireForUpdate<GameCamera>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            RefRW<GameCamera> gameCamera = SystemAPI.GetSingletonRW<GameCamera>();

            foreach (var (ignoreCameraInputRequest, entity) in SystemAPI.Query<IgnoreCameraInputRequest>()
                         .WithEntityAccess())
            {
                gameCamera.ValueRW.IgnoreInput = ignoreCameraInputRequest.Value;
                ecb.DestroyEntity(entity);
            }
        }
    }
}