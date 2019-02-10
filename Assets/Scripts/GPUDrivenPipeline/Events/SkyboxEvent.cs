using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Skybox")]
    [RequireEvent(typeof(PropertySetEvent))]
    public class SkyboxEvent : PipelineEvent
    {
        public Material skyboxMaterial;
        public RenderTargetIdentifier[] skyboxIdentifier = new RenderTargetIdentifier[2];
        protected override void Dispose()
        {
        }
        protected override void Init(PipelineResources resources)
        {
        }

        public override bool CheckProperty()
        {
            return true;
        }
        public override void FrameUpdate(PipelineCamera camera, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            skyboxIdentifier[0] = camera.targets.renderTargetIdentifier;
            skyboxIdentifier[1] = camera.targets.motionVectorTexture;
            buffer.SetRenderTarget(skyboxIdentifier, camera.targets.depthIdentifier);
            buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, skyboxMaterial, 0, 0);
        }
    }
}
