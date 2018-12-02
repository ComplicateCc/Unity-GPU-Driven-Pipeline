using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering;
namespace MPipeline
{
    [PipelineEvent(false, true)]
    public unsafe class GeometryEvent : PipelineEvent
    {
        HizDepth hizDepth;
        Material linearMat;
        public Material proceduralMaterial;
        public enum OcclusionCullingMode
        {
            None, SingleCheck, DoubleCheck
        }
        public OcclusionCullingMode occCullingMod = OcclusionCullingMode.None;
        protected override void Init(PipelineResources resources)
        {
            hizDepth = new HizDepth();
            hizDepth.InitHiZ(resources);
            linearMat = new Material(resources.linearDepthShader);
            Application.targetFrameRate = int.MaxValue;
        }

        protected override void Dispose()
        {
            hizDepth.DisposeHiZ();
        }
        public System.Func<IPerCameraData> getOcclusionData = () => new HizOcclusionData();
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {

            //   Material proceduralMaterial = data.baseBuffer.combinedMaterial;
            CommandBuffer buffer = data.buffer;
            buffer.SetRenderTarget(cam.targets.gbufferIdentifier, cam.targets.depthIdentifier);
            buffer.ClearRenderTarget(true, true, Color.black);
            PipelineBaseBuffer baseBuffer;
            if (!SceneController.current.GetBaseBufferAndCheck(out baseBuffer)) return;
            HizOcclusionData hizData = IPerCameraData.GetProperty<HizOcclusionData>(cam, getOcclusionData);
            RenderClusterOptions options = new RenderClusterOptions
            {
                command = buffer,
                frustumPlanes = data.arrayCollection.frustumPlanes,
                proceduralMaterial = proceduralMaterial,
                isOrtho = cam.cam.orthographic,
                cullingShader = data.resources.gpuFrustumCulling
            };
            HizOptions hizOptions;
            switch (occCullingMod)
            {
                case OcclusionCullingMode.None:
                    SceneController.current.DrawCluster(ref options);
                    break;
                case OcclusionCullingMode.SingleCheck:
                    hizOptions = new HizOptions
                    {
                        currentCameraUpVec = cam.cam.transform.up,
                        hizData = hizData,
                        hizDepth = hizDepth,
                        linearLODMaterial = linearMat
                    };
                    SceneController.current.DrawClusterOccSingleCheck(ref options, ref hizOptions);
                    break;
                case OcclusionCullingMode.DoubleCheck:
                    hizOptions = new HizOptions
                    {
                        currentCameraUpVec = cam.cam.transform.up,
                        hizData = hizData,
                        hizDepth = hizDepth,
                        linearLODMaterial = linearMat
                    };
                    SceneController.current.DrawClusterOccDoubleCheck(ref options, ref hizOptions, ref cam.targets);
                    break;
            }
            data.ExecuteCommandBuffer();
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