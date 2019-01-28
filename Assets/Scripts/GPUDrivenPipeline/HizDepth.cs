using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace MPipeline
{
    public unsafe struct HizDepth
    {
        private RenderTexture backupMip;
        private Material getLodMat;
        public bool Check()
        {
            return backupMip != null && getLodMat != null;
        }
        public void InitHiZ(PipelineResources resources)
        {
            const int depthRes = 256;
            backupMip = new RenderTexture(depthRes * 2, depthRes, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
            backupMip.useMipMap = true;
            backupMip.autoGenerateMips = false;
            backupMip.enableRandomWrite = false;
            backupMip.wrapMode = TextureWrapMode.Clamp;
            backupMip.filterMode = FilterMode.Point;
            getLodMat = new Material(resources.shaders.HizLodShader);
        }
        public void GetMipMap(RenderTexture depthMipTexture, CommandBuffer buffer)
        {
            buffer.SetGlobalTexture(ShaderIDs._MainTex, depthMipTexture);
            for (int i = 1; i < 8; ++i)
            {
                buffer.SetGlobalInt(ShaderIDs._PreviousLevel, i - 1);
                buffer.SetRenderTarget(backupMip, i);
                buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, getLodMat, 0, 0);
                buffer.CopyTexture(backupMip, 0, i, depthMipTexture, 0, i);
            }
        }
        public void DisposeHiZ()
        {
            backupMip.Release();
            Object.DestroyImmediate(backupMip);
        }
    }
}