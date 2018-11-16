using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace MPipeline
{
    [PipelineEvent(false, true)]
    public class SkyboxEvent : PipelineEvent
    {
        public Material skyboxMaterial;
        public RenderTargetIdentifier[] skyboxIdentifier = new RenderTargetIdentifier[2];
        public override void FrameUpdate(PipelineCamera camera, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            skyboxIdentifier[0] = camera.targets.renderTargetIdentifier;
            skyboxIdentifier[1] = camera.targets.motionVectorTexture;
            buffer.SetRenderTarget(camera.targets.renderTargetIdentifier, camera.targets.depthIdentifier);
            buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, skyboxMaterial, 0, 0);
        }
    }
}
