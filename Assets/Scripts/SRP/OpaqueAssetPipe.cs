using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MPipeline;
using UnityEngine.Rendering;
using RenderPipeline = UnityEngine.Experimental.Rendering.RenderPipeline;
using UnityEngine.Experimental.Rendering;
[ExecuteInEditMode]
public class OpaqueAssetPipe : RenderPipelineAsset
{
#if UNITY_EDITOR
    [UnityEditor.MenuItem("SRP-Demo/Deferred")]
    static void CreateBasicAssetPipeline()
    {
        var instance = CreateInstance<OpaqueAssetPipe>();
        UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/OpaqueDeferred.asset");
    }

#endif
    public GameObject pipelinePrefab;
    public PipelineResources pipelineResources;
    protected override IRenderPipeline InternalCreatePipeline()
    {
        return new MPipeline.RenderPipeline(pipelinePrefab, pipelineResources);
    }
}