using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace MPipeline
{
    [PipelineEvent(false, true)]
    public class PropertySetEvent : PipelineEvent
    {
        private System.Func<PipelineCamera, LastVPData> getLastVP = (c) => new LastVPData(GL.GetGPUProjectionMatrix(c.cam.projectionMatrix, false) * c.cam.worldToCameraMatrix);
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            LastVPData lastData = IPerCameraData.GetProperty<LastVPData>(cam, getLastVP);
            //Calculate Last VP for motion vector and Temporal AA
            Matrix4x4 nonJitterVP = GL.GetGPUProjectionMatrix(cam.cam.nonJitteredProjectionMatrix, false) * cam.cam.worldToCameraMatrix;
            ref Matrix4x4 lastVp = ref lastData.lastVP;
            Shader.SetGlobalMatrix(ShaderIDs._LastVp, lastVp);
            Shader.SetGlobalMatrix(ShaderIDs._NonJitterVP, nonJitterVP);
            Shader.SetGlobalMatrix(ShaderIDs._InvVP, data.inverseVP);
            Shader.SetGlobalMatrix(ShaderIDs._VP, data.vp);
            Shader.SetGlobalMatrix(ShaderIDs._InvLastVP, lastVp.inverse);
            Shader.SetGlobalVectorArray(ShaderIDs._FarClipCorner, data.arrayCollection.farFrustumCorner);
            lastVp = nonJitterVP;
        }
    }

    public class LastVPData : IPerCameraData
    {
        public Matrix4x4 lastVP = Matrix4x4.identity;
        public LastVPData(Matrix4x4 lastVP)
        {
            this.lastVP = lastVP;
        }
        public override void DisposeProperty()
        {
        }
    }
}