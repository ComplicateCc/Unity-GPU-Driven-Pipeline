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

        public ComputeShader shader;
        private static int[] _Count = new int[2];
        private Matrix4x4[] cascadeShadowMapVP = new Matrix4x4[4];
        private Vector4[] shadowFrustumVP = new Vector4[6];

        protected override void Init(PipelineResources resources)
        {
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
            if (SunLight.current == null) return;
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
                PipelineFunctions.UpdateShadowMapState(ref SunLight.shadMap, ref SunLight.current.settings, buffer);
                SceneController.current.DrawDirectionalShadow(cam.cam, ref opts, ref SunLight.current.settings, ref SunLight.shadMap, cascadeShadowMapVP);
                buffer.SetGlobalMatrixArray(ShaderIDs._ShadowMapVPs, cascadeShadowMapVP);
                buffer.SetGlobalTexture(ShaderIDs._DirShadowMap, SunLight.shadMap.shadowmapTexture);
                pass = 0;
            }
            else
            {
                pass = 1;
            }
            buffer.SetGlobalVector(ShaderIDs._LightFinalColor, SunLight.shadMap.light.color * SunLight.shadMap.light.intensity);
            buffer.SetRenderTarget(cam.targets.renderTargetIdentifier, cam.targets.depthIdentifier);
            buffer.SetGlobalVector(ShaderIDs._LightPos, -SunLight.shadMap.shadCam.forward);
            buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, shadMaskMaterial, 0, pass);
            data.ExecuteCommandBuffer();
        }
    }
}