using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.VFX;

[VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
public struct VFXHitSparksRequest
{
    public Vector3 Position;
    public Vector3 Color;
}

[VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
public struct VFXExplosionRequest
{
    public Vector3 Position;
    public float Scale;
}

[VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
public struct VFXSpawnToDataRequest
{
    public int IndexInData;
}

[VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
public struct VFXThrusterData : IKillableVFX
{
    public Vector3 Position;
    public Vector3 Direction;
    public Vector3 Color;
    public float Size;
    public float Length;
    
    public void Kill()
    {
        Size = -1f;
    }
}

public static class VFXReferences
{
    public static VisualEffect HitSparksGraph;
    public static GraphicsBuffer HitSparksRequestsBuffer;
    
    public static VisualEffect ExplosionsGraph;
    public static GraphicsBuffer ExplosionsRequestsBuffer;
    
    public static VisualEffect ThrustersGraph;
    public static GraphicsBuffer ThrusterRequestsBuffer;
    public static GraphicsBuffer ThrusterDatasBuffer;
}

public interface IKillableVFX
{
    public void Kill();
}

public struct VFXManager<T> where T : unmanaged
{
    public NativeReference<int> RequestsCount;
    public NativeArray<T> Requests;

    public bool GraphIsInitialized { get; private set; }

    public VFXManager(int maxRequests, ref GraphicsBuffer graphicsBuffer)
    {
        RequestsCount = new NativeReference<int>(0, Allocator.Persistent);
        Requests = new NativeArray<T>(maxRequests, Allocator.Persistent);

        graphicsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxRequests,
            Marshal.SizeOf(typeof(T)));

        GraphIsInitialized = false;
    }

    public void Dispose(ref GraphicsBuffer graphicsBuffer)
    {
        graphicsBuffer?.Dispose();
        if (RequestsCount.IsCreated)
        {
            RequestsCount.Dispose();
        }
        if (Requests.IsCreated)
        {
            Requests.Dispose();
        }
    }

    public void Update(
        VisualEffect vfxGraph, 
        ref GraphicsBuffer graphicsBuffer, 
        float deltaTimeMultiplier, 
        int spawnBatchId, 
        int requestsCountId, 
        int requestsBufferId)
    {
        if (vfxGraph != null && graphicsBuffer != null)
        {
            vfxGraph.playRate = deltaTimeMultiplier;
            
            if (!GraphIsInitialized)
            {
                vfxGraph.SetGraphicsBuffer(requestsBufferId, graphicsBuffer);
                GraphIsInitialized = true;
            }

            if (graphicsBuffer.IsValid())
            {
                graphicsBuffer.SetData(Requests, 0, 0, RequestsCount.Value);
                vfxGraph.SetInt(requestsCountId, math.min(RequestsCount.Value, Requests.Length));
                vfxGraph.SendEvent(spawnBatchId);
                RequestsCount.Value = 0;
            }
        }
    }

    public void AddRequest(T request)
    {
        if (RequestsCount.Value < Requests.Length)
        {
            Requests[RequestsCount.Value] = request;
            RequestsCount.Value++;
        }
    }
}

public struct VFXManagerParented<T> where T : unmanaged, IKillableVFX
{
    public NativeReference<int> RequestsCount;
    public NativeArray<VFXSpawnToDataRequest> Requests;
    public NativeArray<T> Datas;
    private NativeQueue<int> FreeIndexes;
    
    public bool GraphIsInitialized { get; private set; }

    public VFXManagerParented(int maxCount, ref GraphicsBuffer requestsGraphicsBuffer, ref GraphicsBuffer datasGraphicsBuffer)
    {
        RequestsCount = new NativeReference<int>(0, Allocator.Persistent);
        Requests = new NativeArray<VFXSpawnToDataRequest>(maxCount, Allocator.Persistent);
        Datas = new NativeArray<T>(maxCount, Allocator.Persistent);
        FreeIndexes = new NativeQueue<int>(Allocator.Persistent);

        requestsGraphicsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxCount,
            Marshal.SizeOf(typeof(VFXSpawnToDataRequest)));
        datasGraphicsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxCount,
            Marshal.SizeOf(typeof(T)));

        for (int i = 0; i < maxCount; i++)
        {
            FreeIndexes.Enqueue(i);
        }
        
        GraphIsInitialized = false;
    }

    public void Dispose(ref GraphicsBuffer requestsGraphicsBuffer, ref GraphicsBuffer datasGraphicsBuffer)
    {
        requestsGraphicsBuffer?.Dispose();
        datasGraphicsBuffer?.Dispose();
        if (RequestsCount.IsCreated)
        {
            RequestsCount.Dispose();
        }
        if (Requests.IsCreated)
        {
            Requests.Dispose();
        }
        if (Datas.IsCreated)
        {
            Datas.Dispose();
        }
        if (FreeIndexes.IsCreated)
        {
            FreeIndexes.Dispose();
        }
    }

    public void Update(
        VisualEffect vfxGraph, 
        ref GraphicsBuffer requestsGraphicsBuffer, 
        ref GraphicsBuffer datasGraphicsBuffer, 
        float deltaTimeMultiplier, 
        int spawnBatchId, 
        int requestsCountId, 
        int requestsBufferId, 
        int datasBufferId)
    {
        if (vfxGraph != null && requestsGraphicsBuffer != null && datasGraphicsBuffer != null)
        {
            vfxGraph.playRate = deltaTimeMultiplier;
            
            if (!GraphIsInitialized)
            {
                vfxGraph.SetGraphicsBuffer(requestsBufferId, requestsGraphicsBuffer);
                vfxGraph.SetGraphicsBuffer(datasBufferId, datasGraphicsBuffer);
                GraphIsInitialized = true;
            }

            if (requestsGraphicsBuffer.IsValid() && datasGraphicsBuffer.IsValid())
            {
                requestsGraphicsBuffer.SetData(Requests, 0, 0, RequestsCount.Value);
                datasGraphicsBuffer.SetData(Datas);
                
                vfxGraph.SetInt(requestsCountId, math.min(RequestsCount.Value, Requests.Length));
                vfxGraph.SendEvent(spawnBatchId);
                
                RequestsCount.Value = 0;
            }
        }
    }
    
    public int Create()
    {
        if (FreeIndexes.TryDequeue(out int index))
        {
            // Request to spawn
            if (RequestsCount.Value < Requests.Length)
            {
                Requests[RequestsCount.Value] = new VFXSpawnToDataRequest
                {
                    IndexInData = index,
                };
                RequestsCount.Value++;
            }
            
            return index;
        }

        return -1;
    }

    public void Kill(int index)
    {
        if (index >= 0 && index < Datas.Length)
        {
            T killdata = default;
            killdata.Kill();
            Datas[index] = killdata;

            FreeIndexes.Enqueue(index);
        }
    }
}

public struct VFXHitSparksSingleton : IComponentData
{
    public VFXManager<VFXHitSparksRequest> Manager;
}

public struct VFXExplosionsSingleton : IComponentData
{
    public VFXManager<VFXExplosionRequest> Manager;
}

public struct VFXThrustersSingleton : IComponentData
{
    public VFXManagerParented<VFXThrusterData> Manager;
}

[UpdateAfter(typeof(ShipPostTransformsSystem))]
public partial struct VFXSystem : ISystem
{
    private int _spawnBatchId;
    private int _requestsCountId;
    private int _requestsBufferId;
    private int _datasBufferId;

    private VFXManager<VFXHitSparksRequest> _hitSparksManager;
    private VFXManager<VFXExplosionRequest> _explosionsManager;
    private VFXManagerParented<VFXThrusterData> _thrustersManager;

    #if UNITY_ANDROID || UNITY_IOS
    public const int HitSparksCapacity = 700;
    public const int ExplosionsCapacity = 700;
    public const int ThrustersCapacity = 50000;
    #else
    public const int HitSparksCapacity = 1000;
    public const int ExplosionsCapacity = 1000;
    public const int ThrustersCapacity = 100000;
    #endif

    public void OnCreate(ref SystemState state)
    {
        // Names to Ids
        _spawnBatchId = Shader.PropertyToID("SpawnBatch");
        _requestsCountId = Shader.PropertyToID("SpawnRequestsCount");
        _requestsBufferId = Shader.PropertyToID("SpawnRequestsBuffer");
        _datasBufferId = Shader.PropertyToID("DatasBuffer");

        // VFX managers
        _hitSparksManager = new VFXManager<VFXHitSparksRequest>(HitSparksCapacity, ref VFXReferences.HitSparksRequestsBuffer);
        _explosionsManager = new VFXManager<VFXExplosionRequest>(ExplosionsCapacity, ref VFXReferences.ExplosionsRequestsBuffer);
        _thrustersManager = new VFXManagerParented<VFXThrusterData>(ThrustersCapacity, ref VFXReferences.ThrusterRequestsBuffer, ref VFXReferences.ThrusterDatasBuffer);

        // Singletons
        state.EntityManager.AddComponentData(state.EntityManager.CreateEntity(), new VFXHitSparksSingleton
        {
            Manager = _hitSparksManager,
        });
        state.EntityManager.AddComponentData(state.EntityManager.CreateEntity(), new VFXExplosionsSingleton
        {
            Manager = _explosionsManager,
        });
        state.EntityManager.AddComponentData(state.EntityManager.CreateEntity(), new VFXThrustersSingleton
        {
            Manager = _thrustersManager,
        });
    }

    public void OnDestroy(ref SystemState state)
    {
        _hitSparksManager.Dispose(ref VFXReferences.HitSparksRequestsBuffer);
        _explosionsManager.Dispose(ref VFXReferences.ExplosionsRequestsBuffer);
        _thrustersManager.Dispose(ref VFXReferences.ThrusterRequestsBuffer, ref VFXReferences.ThrusterDatasBuffer);
    }
    
    public void OnUpdate(ref SystemState state)
    {
        // This is required because we must use data in native collections on the main thread, to send it to VFXGraphs
        SystemAPI.QueryBuilder().WithAll<VFXHitSparksSingleton>().Build().CompleteDependency();
        SystemAPI.QueryBuilder().WithAll<VFXExplosionsSingleton>().Build().CompleteDependency();
        SystemAPI.QueryBuilder().WithAll<VFXThrustersSingleton>().Build().CompleteDependency();
        
        // Update managers
        float rateRatio = SystemAPI.Time.DeltaTime / Time.deltaTime;
        
        _hitSparksManager.Update(
            VFXReferences.HitSparksGraph, 
            ref VFXReferences.HitSparksRequestsBuffer, 
            rateRatio,
            _spawnBatchId, 
            _requestsCountId, 
            _requestsBufferId);
        
        _explosionsManager.Update(
            VFXReferences.ExplosionsGraph, 
            ref VFXReferences.ExplosionsRequestsBuffer, 
            rateRatio,
            _spawnBatchId, 
            _requestsCountId, 
            _requestsBufferId);
        
        _thrustersManager.Update(
            VFXReferences.ThrustersGraph, 
            ref VFXReferences.ThrusterRequestsBuffer, 
            ref VFXReferences.ThrusterDatasBuffer,
            rateRatio,
            _spawnBatchId, 
            _requestsCountId,
            _requestsBufferId,
            _datasBufferId);
    }
}