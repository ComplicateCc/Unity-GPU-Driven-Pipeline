using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Uber")]
    [RequireEvent(typeof(PropertySetEvent))]
    public unsafe sealed class UberEvents : PipelineEvent
    {
        private Material muberMaterial;
        protected override void Init(PipelineResources resources)
        {
            muberMaterial = new Material(resources.shaders.uberShader);
        }

        protected override void Dispose()
        {
            DestroyImmediate(muberMaterial);
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            data.buffer.GetTemporaryRT(ShaderIDs._CopyedDepthTexture, cam.cam.pixelWidth, cam.cam.pixelHeight, 32, FilterMode.Point, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear, 1, false);
            data.buffer.CopyTexture(cam.targets.depthIdentifier, ShaderIDs._CopyedDepthTexture);
            data.buffer.SetRenderTarget(color: cam.targets.renderTargetIdentifier, depth: cam.targets.depthIdentifier);
            data.buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, muberMaterial, 0, 0);
            data.buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, muberMaterial, 0, 1);
            data.buffer.ReleaseTemporaryRT(ShaderIDs._CopyedDepthTexture);
        }

        public override bool CheckProperty()
        {
            return muberMaterial != null;
        }
    }
}
