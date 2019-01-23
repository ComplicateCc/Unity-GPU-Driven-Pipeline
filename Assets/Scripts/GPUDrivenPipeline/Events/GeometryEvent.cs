using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;
namespace MPipeline
{
    [System.Serializable]
    [PipelineEvent(false, true)]
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
            Application.targetFrameRate = int.MaxValue;
        }

        protected override void Dispose()
        {
            hizDepth.DisposeHiZ();
        }
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            buffer.SetRenderTarget(cam.targets.gbufferIdentifier, cam.targets.depthIdentifier);
            buffer.ClearRenderTarget(true, true, Color.black);
            HizOcclusionData hizData = IPerCameraData.GetProperty(cam, () => new HizOcclusionData());
            RenderClusterOptions options = new RenderClusterOptions
            {
                command = buffer,
                frustumPlanes = data.frustumPlanes,
                cullingShader = data.resources.shaders.gpuFrustumCulling,
                terrainCompute = data.resources.shaders.terrainCompute
            };

            if (enableOcclusionCulling)
            {
                HizOptions hizOptions;
                hizOptions = new HizOptions
                {
                    currentCameraUpVec = cam.cam.transform.up,
                    hizData = hizData,
                    hizDepth = hizDepth,
                    linearLODMaterial = linearMat,
                    currentDepthTex = cam.targets.depthIdentifier
                };
                SceneController.DrawClusterOccDoubleCheck(ref options, ref hizOptions, ref cam.targets, ref data, cam.cam);
            }
            else
            {
                SceneController.DrawCluster(ref options, ref cam.targets, ref data, cam.cam);
            }
        }
    }
    public class HizOcclusionData : IPerCameraData
    {
        public Vector3 lastFrameCameraUp = Vector3.up;
        public RenderTexture historyDepth;
        public HizOcclusionData()
        {
            historyDepth = new RenderTexture(512, 256, 0, RenderTextureFormat.RHalf);
            historyDepth.useMipMap = true;
            historyDepth.autoGenerateMips = false;
            historyDepth.filterMode = FilterMode.Point;
            historyDepth.wrapMode = TextureWrapMode.Clamp;
        }
        public override void DisposeProperty()
        {
            historyDepth.Release();
            Object.Destroy(historyDepth);
        }
    }
}