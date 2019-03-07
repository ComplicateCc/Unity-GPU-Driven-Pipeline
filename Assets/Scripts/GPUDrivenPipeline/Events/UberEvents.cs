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
            data.buffer.SetKeyword("GPURP_UBER", RenderPipeline.currentPath == PipelineResources.CameraRenderingPath.GPUDeferred);
            data.buffer.SetRenderTarget(color: cam.targets.renderTargetIdentifier, depth: cam.targets.depthBuffer);
            data.buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, muberMaterial, 0, -1);
        }

        public override bool CheckProperty()
        {
            return muberMaterial != null;
        }
    }
}
