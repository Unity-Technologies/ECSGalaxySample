using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Galaxy.Authoring
{
    [DisallowMultipleComponent]
    public class GameCameraAuthoring : MonoBehaviour
    {
        [Header("General")]
        public float MaxVAngle = 89f;
        public float MinVAngle = -89f;
        
        [Header("Orbit")]
        public float OrbitRotationSpeed = 150f;
        public float OrbitTargetDistance = 5f;
        public float OrbitMinDistance = 0f;
        public float OrbitMaxDistance = 10f;
        public float OrbitDistanceMovementSpeed = 50f;
        public float OrbitDistanceMovementSharpness = 10f;

        [Header("Free Fly")]
        public float FlyRotationSpeed = 999999f;
        public float FlyRotationSharpness = 999999f;
        public float FlyMoveSharpness = 10f;
        public float FlyMaxMoveSpeed = 10f;
        public float FlySprintSpeedBoost = 5f;

        public class Baker : Baker<GameCameraAuthoring>
        {
            public override void Bake(GameCameraAuthoring authoring)
            {
                Entity entity = GetEntity(authoring, TransformUsageFlags.Dynamic);

                AddComponent(entity, new GameCamera
                {
                    OrbitRotationSpeed = authoring.OrbitRotationSpeed,
                    MaxVAngle = authoring.MaxVAngle,
                    MinVAngle = authoring.MinVAngle,
                    OrbitTargetDistance = authoring.OrbitTargetDistance,
                    OrbitMinDistance = authoring.OrbitMinDistance,
                    OrbitMaxDistance = authoring.OrbitMaxDistance,
                    OrbitDistanceMovementSpeed = authoring.OrbitDistanceMovementSpeed,
                    OrbitDistanceMovementSharpness = authoring.OrbitDistanceMovementSharpness,

                    FlyRotationSpeed = authoring.FlyRotationSpeed,
                    FlyRotationSharpness = authoring.FlyRotationSharpness,
                    FlyMoveSharpness = authoring.FlyMoveSharpness,
                    FlyMaxMoveSpeed = authoring.FlyMaxMoveSpeed,
                    FlySprintSpeedBoost = authoring.FlySprintSpeedBoost,
                    
                    CameraMode = GameCamera.Mode.Fly,
                    CurrentDistanceFromMovement = authoring.OrbitTargetDistance,
                    PlanarForward = math.forward(),
                });
                AddComponent(entity, new MainCamera());
            }
        }
    }
}