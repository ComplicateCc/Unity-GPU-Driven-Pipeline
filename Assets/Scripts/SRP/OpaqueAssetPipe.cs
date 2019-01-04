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
        var instance = ScriptableObject.CreateInstance<OpaqueAssetPipe>();
        UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/OpaqueDeferred.asset");
    }
#endif
    protected override IRenderPipeline InternalCreatePipeline()
    {
        return new OpaqueAssetPipeInstance();
    }
}
public class OpaqueAssetPipeInstance : RenderPipeline
{
    public override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        if (!MPipeline.RenderPipeline.current) return;
        foreach (var camera in cameras)
        {
            PipelineCamera cam = camera.GetComponent<PipelineCamera>();
            if (!cam) continue;
            CullResults results;
            if (!CullResults.Cull(camera, context, out results)) continue;
            context.SetupCameraProperties(camera);
            cam.RenderSRP(BuiltinRenderTextureType.CameraTarget, ref context, ref results);
            context.Submit();
        }
    }
}