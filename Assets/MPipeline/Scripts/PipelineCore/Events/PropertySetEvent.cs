using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/Property Set")]
    public class PropertySetEvent : PipelineEvent
    {
        private Random rand;
        public Matrix4x4 lastViewProjection { get; private set; }
       // public Matrix4x4 inverseNonJitterVP { get; private set; }
        private System.Func<PipelineCamera, LastVPData> getLastVP = (c) => {
            Matrix4x4 p = GL.GetGPUProjectionMatrix(c.cam.projectionMatrix, false);
            Matrix4x4 vp = p * c.cam.worldToCameraMatrix;
            return new LastVPData(vp);
        };
        
        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            LastVPData lastData = IPerCameraData.GetProperty(cam, getLastVP, this);
            //Calculate Last VP for motion vector and Temporal AA
            Matrix4x4 nonJitterP = GL.GetGPUProjectionMatrix(cam.cam.nonJitteredProjectionMatrix, false);
            Matrix4x4 nonJitterVP = nonJitterP * cam.cam.worldToCameraMatrix;
            ref Matrix4x4 lastVp = ref lastData.lastVP;
            lastViewProjection = lastVp;
            CommandBuffer buffer = data.buffer;
            buffer.SetKeyword("RENDERING_TEXTURE", cam.cam.targetTexture != null);
            buffer.SetGlobalMatrix(ShaderIDs._LastVp, lastVp);
            buffer.SetGlobalMatrix(ShaderIDs._NonJitterVP, nonJitterVP);
            buffer.SetGlobalMatrix(ShaderIDs._NonJitterTextureVP, GL.GetGPUProjectionMatrix(cam.cam.nonJitteredProjectionMatrix, true) * cam.cam.worldToCameraMatrix);
            buffer.SetGlobalMatrix(ShaderIDs._InvVP, data.inverseVP);
            buffer.SetGlobalVector(ShaderIDs._RandomSeed, (float4)(rand.NextDouble4() * 10000 + 1000));
            lastVp = nonJitterVP;
        }
        protected override void Init(PipelineResources resources)
        {
            rand = new Random((uint)System.Guid.NewGuid().GetHashCode());
            Shader.SetGlobalMatrix(ShaderIDs._LastFrameModel, Matrix4x4.zero);
        }
        protected override void Dispose()
        {
        }
        public override bool CheckProperty()
        {
            return true;
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