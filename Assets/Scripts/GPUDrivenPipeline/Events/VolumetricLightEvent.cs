using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace MPipeline
{
    [PipelineEvent(false, true)]
    public class VolumetricLightEvent : PipelineEvent
    {
        private CBDRSharedData cbdr;
        private Material volumeMat;
        private static readonly int _TempMap = Shader.PropertyToID("_TempMap");
        private static readonly int _OriginMap = Shader.PropertyToID("_OriginMap");
        private static readonly int _DownSampledDepth = Shader.PropertyToID("_DownSampledDepth");
        private static readonly int _VolumeTex = Shader.PropertyToID("_VolumeTex");
        protected override void Init(PipelineResources resources)
        {
            cbdr = PipelineSharedData.Get(renderPath, resources, (res) => new CBDRSharedData(res));
            volumeMat = new Material(resources.volumetricShader);
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            if (!cbdr.directLightEnabled && !cbdr.pointLightEnabled) return;
            CommandBuffer buffer = data.buffer;
            buffer.SetKeyword("DIRLIGHT", cbdr.directLightEnabled);
            buffer.SetKeyword("DIRLIGHTSHADOW", cbdr.directLightShadowEnable);
            buffer.SetKeyword("POINTLIGHT", cbdr.pointLightEnabled);
            //Set Random
            buffer.SetGlobalVector(ShaderIDs._RandomNumber, new Vector4(Random.Range(10f, 50f), Random.Range(10f, 50f), Random.Range(10f, 50f), Random.Range(20000f, 40000f)));
            buffer.SetGlobalVector(ShaderIDs._RandomWeight, new Vector4(Random.value, Random.value, Random.value, Random.value));
            buffer.SetGlobalTexture(ShaderIDs._RandomTex, cbdr.randomTex);
            //DownSample
            buffer.GetTemporaryRT(_TempMap, cam.cam.pixelWidth / 2, cam.cam.pixelHeight / 2, 0, FilterMode.Point, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
            buffer.SetGlobalTexture(_OriginMap, cam.targets.depthTexture);
            buffer.BlitSRT(_TempMap, volumeMat, 5);
            buffer.SetGlobalTexture(_OriginMap, _TempMap);
            buffer.GetTemporaryRT(_DownSampledDepth, cam.cam.pixelWidth / 4, cam.cam.pixelHeight / 4, 0, FilterMode.Point, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
            buffer.BlitSRT(_DownSampledDepth, volumeMat, 1);
            buffer.ReleaseTemporaryRT(_TempMap);
            //Volumetric Light
            buffer.GetTemporaryRT(_VolumeTex, cam.cam.pixelWidth / 4, cam.cam.pixelHeight / 4, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            buffer.BlitSRT(_VolumeTex, volumeMat, 0);
            buffer.GetTemporaryRT(_TempMap, cam.cam.pixelWidth / 4, cam.cam.pixelHeight / 4, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            buffer.BlitSRT(_VolumeTex, _TempMap, volumeMat, 3);
            buffer.BlitSRT(_TempMap, _VolumeTex, volumeMat, 4);
            buffer.ReleaseTemporaryRT(_TempMap);

            buffer.BlitSRT(cam.targets.renderTargetIdentifier, volumeMat, 2);
            //Dispose
            buffer.ReleaseTemporaryRT(_DownSampledDepth);
            buffer.ReleaseTemporaryRT(_VolumeTex);
            PipelineFunctions.ExecuteCommandBuffer(ref data);
        }

        protected override void Dispose()
        {
            Destroy(volumeMat);
        }
    }
}
