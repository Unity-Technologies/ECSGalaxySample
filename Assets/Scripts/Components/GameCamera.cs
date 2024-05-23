using System;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct GameCamera : IComponentData
{
    public enum Mode
    {
        None,
        Fly,
        OrbitPlanet,
        OrbitShip,
    }

    public float MaxVAngle;
    public float MinVAngle;
    
    // Orbit
    public float OrbitRotationSpeed;
    public float OrbitTargetDistance;
    public float OrbitMinDistance;
    public float OrbitMaxDistance;
    public float OrbitDistanceMovementSpeed;
    public float OrbitDistanceMovementSharpness;

    // Fly
    public float FlyRotationSpeed;
    public float FlyRotationSharpness;
    public float FlyMoveSharpness;
    public float FlyMaxMoveSpeed;
    public float FlySprintSpeedBoost;

    // State
    public Mode CameraMode;
    public Entity FollowedEntity;
    public float3 CurrentMoveVelocity;
    public float CurrentDistanceFromMovement;
    public float PitchAngle;
    public float3 PlanarForward;
    
    public bool IgnoreInput;
}

public struct CameraInputs
{
    public float3 Move;
    public float2 Look;
    public float Zoom;
    public bool Sprint;
    public bool SwitchMode;
}