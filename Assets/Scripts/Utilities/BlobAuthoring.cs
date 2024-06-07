using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public interface IBlobAuthoring<T> where T : unmanaged
{
    public void BakeToBlobData(ref T data, ref BlobBuilder blobBuilder);
}

public static class BlobAuthoringUtility
{
    public static BlobAssetReference<T> BakeToBlob<T>(IBaker baker, IBlobAuthoring<T> blobAuthoring, UnityEngine.Object dependsOn = null) where T : unmanaged
    {
        BlobBuilder builder = new BlobBuilder(Allocator.Temp);
        ref T definition = ref builder.ConstructRoot<T>();
    
        blobAuthoring.BakeToBlobData(ref definition, ref builder);
        
        BlobAssetReference<T> blobReference = builder.CreateBlobAssetReference<T>(Allocator.Persistent);
        baker.AddBlobAsset(ref blobReference, out var hash);
        builder.Dispose();

        if (dependsOn != null)
        {
            baker.DependsOn(dependsOn);
        }

        return blobReference;
    }
}