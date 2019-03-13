using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace MPipeline
{
    public unsafe struct HizDepth
    {
        public RenderTexture backupMip { get; private set; }
        public RenderTexture depthMip { get; private set; }
        private Material getLodMat;
        public bool Check()
        {
            return backupMip != null && getLodMat != null;
        }
        public void InitHiZ(PipelineResources resources)
        {
            const int depthRes = 256;
            backupMip = new RenderTexture(depthRes * 2, depthRes, 16, RenderTextureFormat.R16, RenderTextureReadWrite.Linear);
            backupMip.useMipMap = true;
            backupMip.autoGenerateMips = false;
            backupMip.enableRandomWrite = false;
            backupMip.wrapMode = TextureWrapMode.Clamp;
            backupMip.filterMode = FilterMode.Point;
            depthMip = new RenderTexture(depthRes * 2, depthRes, 16, RenderTextureFormat.R16, RenderTextureReadWrite.Linear);
            depthMip.useMipMap = true;
            depthMip.autoGenerateMips = false;
            depthMip.enableRandomWrite = false;
            depthMip.wrapMode = TextureWrapMode.Clamp;
            depthMip.filterMode = FilterMode.Point;
            depthMip.Create();
            backupMip.Create();
            getLodMat = new Material(resources.shaders.HizLodShader);
        }
        public void GetMipMap(CommandBuffer buffer)
        {
            buffer.SetGlobalTexture(ShaderIDs._MainTex, depthMip);
            for (int i = 1; i < 8; ++i)
            {
                buffer.SetGlobalInt(ShaderIDs._PreviousLevel, i - 1);
                buffer.SetRenderTarget(backupMip, i);
                buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, getLodMat, 0, 0);
                buffer.CopyTexture(backupMip, 0, i, depthMip, 0, i);
            }
        }
        public void DisposeHiZ()
        {
            Object.DestroyImmediate(backupMip);
        }
    }
}