using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

public static class MathUtilities
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 ProjectOnPlane(float3 vector, float3 onPlaneNormal)
    {
        return vector - math.projectsafe(vector, onPlaneNormal);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 ClampToMaxLength(float3 vector, float maxLength)
    {
        float sqrmag = math.lengthsq(vector);
        if (sqrmag > maxLength * maxLength)
        {
            float mag = math.sqrt(sqrmag);
            float normalized_x = vector.x / mag;
            float normalized_y = vector.y / mag;
            float normalized_z = vector.z / mag;
            return new float3(normalized_x * maxLength,
                normalized_y * maxLength,
                normalized_z * maxLength);
        }

        return vector;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetSharpnessInterpolant(float sharpness, float dt)
    {
        return math.saturate(1f - math.exp(-sharpness * dt));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 RandomInSphere(ref Random random, float radius)
    {
        float3 v = random.NextFloat3Direction();
        v *= math.pow(random.NextFloat(), 1.0f / 3.0f);
        return v * radius;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetLocalRotationForWorldRotation(Entity entity, out quaternion localRotation, quaternion targetWorldRotation, 
        ref ComponentLookup<LocalToWorld> localToWorldLookup, ref ComponentLookup<Parent> parentLookup)
    {
        if (parentLookup.TryGetComponent(entity, out Parent parent) && localToWorldLookup.TryGetComponent(parent.Value, out LocalToWorld patentLTW))
        {
            localRotation = math.mul(math.inverse(patentLTW.Rotation), targetWorldRotation);
            return;
        }

        localRotation = targetWorldRotation;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp(float val, float2 bounds)
    {
        return math.clamp(val, bounds.x, bounds.y);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SegmentIntersectsSphere(float3 p1, float3 p2, float3 sphereCenter, float sphereRadius)
    {
        float distanceSqToSphereCenter = float.MaxValue;
        float segmentLengthSq = math.distancesq(p1, p2); 
        if (segmentLengthSq == 0.0) 
        {
            distanceSqToSphereCenter = math.distancesq(sphereCenter, p1); 
        }
        else
        {
            float t = math.max(0f, math.min(1f, math.dot(sphereCenter - p1, p2 - p1) / segmentLengthSq));
            float3 projection = p1 + t * (p2 - p1); 
            distanceSqToSphereCenter = math.distancesq(sphereCenter, projection);
        }
        
        return distanceSqToSphereCenter <= sphereRadius * sphereRadius;
    }

    public static void GenerateEquidistantPointsOnSphere(ref NativeList<float3> points, int newPointsCount, float radius,
        int repelIterations = 50)
    {
        int initialPointsCount = points.Length;
        int totalPointsCount = initialPointsCount + newPointsCount;
        
        // First pass: generate points around the sphere in a semi-regular distribution
        float goldenRatio = 1 + (math.sqrt(5f) / 4f);
        float angleIncrement = math.PI * 2f * goldenRatio;
        for (int i = initialPointsCount; i < totalPointsCount; i++)
        {
            float distance = (float)i / (float)totalPointsCount;
            float incline = math.acos(1f - (2f * distance));
            float azimuth = angleIncrement * i;

            float3 point = new float3
            {
                x = math.sin(incline) * math.cos(azimuth) * radius,
                y = math.sin(incline) * math.sin(azimuth) * radius,
                z = math.cos(incline) * radius,
            };

            points.Add(point);
        }

        // Second pass: make points repel each other
        if (points.Length > 1)
        {
            float repelAngleIncrements = math.PI * 0.01f;
            for (int r = 0; r < repelIterations; r++)
            {
                for (int a = 0; a < points.Length; a++)
                {
                    float3 dir = math.normalizesafe(points[a]);
                    float closestPointRemappedDot = 0f;
                    float3 closestPointRotationAxis = default;

                    for (int b = 0; b < points.Length; b++)
                    {
                        if (b != a)
                        {
                            float3 otherDir = math.normalizesafe(points[b]);

                            float dot = math.dot(dir, otherDir);
                            float remappedDot = math.remap(-1f, 1f, 0f, 1f, dot);

                            if (remappedDot > closestPointRemappedDot)
                            {
                                closestPointRemappedDot = remappedDot;
                                closestPointRotationAxis = -math.normalizesafe(math.cross(dir, otherDir));
                            }
                        }
                    }

                    quaternion repelRotation = quaternion.AxisAngle(closestPointRotationAxis, repelAngleIncrements);
                    dir = math.rotate(repelRotation, dir);
                    points[a] = dir * radius;
                }
            }
        }
    }
}
