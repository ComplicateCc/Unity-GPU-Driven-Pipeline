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
        HizDepth hizDepth;
        Material linearMat;
        public bool enableOcclusionCulling;
        protected override void Init(PipelineResources resources)
        {
            hizDepth = new HizDepth();
            hizDepth.InitHiZ(resources);
            linearMat = new Material(resources.shaders.linearDepthShader);
        }
        public override bool CheckProperty()
        {
            return hizDepth.Check() && linearMat != null;
        }
        protected override void Dispose()
        {
            hizDepth.DisposeHiZ();
            DestroyImmediate(linearMat);
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

            if (enableOcclusionCulling)
            {

            }
            else
            {
                buffer.SetRenderTarget(cam.targets.gbufferIdentifier, cam.targets.depthBuffer);
                buffer.ClearRenderTarget(true, true, Color.black);
                SceneController.DrawCluster(ref options, ref cam.targets, ref data, cam.cam);
            }
        }
    }
}