using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Geometry")]
    [RequireEvent(typeof(PropertySetEvent))]
    public unsafe class GeometryEvent : PipelineEvent
    {
        Material linearDrawerMat;
        Material linearMat;
        protected override void Init(PipelineResources resources)
        {
            linearMat = new Material(resources.shaders.linearDepthShader);
            linearDrawerMat = new Material(resources.shaders.linearDrawerShader);
        }
        public override bool CheckProperty()
        {
            return linearMat && linearDrawerMat;
        }
        protected override void Dispose()
        {
            DestroyImmediate(linearMat);
            DestroyImmediate(linearDrawerMat);
            linearMat = null;
        }
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            RenderClusterOptions options = new RenderClusterOptions
            {
                command = buffer,
                frustumPlanes = data.frustumPlanes,
                cullingShader = data.resources.shaders.gpuFrustumCulling,
                terrainCompute = data.resources.shaders.terrainCompute
            };

                SceneController.DrawCluster(ref options, ref cam.targets, ref data, cam.cam);
            
         //   buffer.Blit(cam.targets.gbufferIdentifier[2], BuiltinRenderTextureType.CameraTarget);
        }
    }
}