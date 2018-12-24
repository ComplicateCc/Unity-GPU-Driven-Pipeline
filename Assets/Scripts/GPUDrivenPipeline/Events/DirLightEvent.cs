using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace MPipeline
{
    [PipelineEvent(false, true)]
    public unsafe class DirLightEvent : PipelineEvent
    {    
        private Material shadMaskMaterial;
        private static int[] _Count = new int[2];
        private Matrix4x4[] cascadeShadowMapVP = new Matrix4x4[4];
        private Vector4[] shadowFrustumVP = new Vector4[6];
        private CBDRSharedData cbdr;
        protected override void Init(PipelineResources resources)
        {
            cbdr = PipelineSharedData.Get(renderPath, resources, (a) => new CBDRSharedData(a));
            shadMaskMaterial = new Material(resources.shadowMaskShader);
            for (int i = 0; i < cascadeShadowMapVP.Length; ++i)
            {
                cascadeShadowMapVP[i] = Matrix4x4.identity;
            }
        }


        protected override void Dispose()
        {
            Destroy(shadMaskMaterial);
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            if (SunLight.current == null)
            {
                cbdr.directLightEnabled = false;
                return;
            }
            cbdr.directLightEnabled = true;
            PipelineBaseBuffer baseBuffer;
            if (!SceneController.GetBaseBuffer(out baseBuffer)) return;
            CommandBuffer buffer = data.buffer;
            int pass;
            if (SunLight.current.enableShadow)
            {
                RenderClusterOptions opts = new RenderClusterOptions
                {
                    frustumPlanes = shadowFrustumVP,
                    command = buffer,
                    cullingShader = data.resources.gpuFrustumCulling,
                    isOrtho = true
                };
                ref ShadowmapSettings settings = ref SunLight.current.settings;
                buffer.SetGlobalVector(ShaderIDs._NormalBiases, settings.normalBias);   //Only Depth
                buffer.SetGlobalVector(ShaderIDs._ShadowDisableDistance, new Vector4(settings.firstLevelDistance, settings.secondLevelDistance, settings.thirdLevelDistance, settings.farestDistance));//Only Mask
                buffer.SetGlobalVector(ShaderIDs._SoftParam, settings.cascadeSoftValue / settings.resolution);
                SceneController.current.DrawDirectionalShadow(cam.cam, ref opts, ref SunLight.current.settings, ref SunLight.shadMap, cascadeShadowMapVP);
                buffer.SetGlobalMatrixArray(ShaderIDs._ShadowMapVPs, cascadeShadowMapVP);
                buffer.SetGlobalTexture(ShaderIDs._DirShadowMap, SunLight.shadMap.shadowmapTexture);
                pass = 0;
                cbdr.directLightShadowEnable = true;
            }
            else
            {
                cbdr.directLightShadowEnable = false;
                pass = 1;
            }
            buffer.SetGlobalVector(ShaderIDs._DirLightFinalColor, SunLight.shadMap.light.color * SunLight.shadMap.light.intensity);
            buffer.SetGlobalVector(ShaderIDs._DirLightPos, -SunLight.shadMap.shadCam.forward);
            buffer.SetRenderTarget(cam.targets.renderTargetIdentifier, cam.targets.depthIdentifier);
            buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, shadMaskMaterial, 0, pass);
            data.ExecuteCommandBuffer();
        }
    }
}