using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using MPipeline;
using UnityEngine.Rendering;
public unsafe class Test : MonoBehaviour
{
    public Material mat;
    public Material setColor;
    [EasyButtons.Button]
    public void Try()
    {
        CommandBuffer buffer = new CommandBuffer();
        RenderTexture rt = new RenderTexture(new RenderTextureDescriptor
        {
            autoGenerateMips = false,
            bindMS = false,
            colorFormat = RenderTextureFormat.ARGBHalf,
            depthBufferBits = 16,
            dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray,
            enableRandomWrite = false,
            height = 1,
            memoryless = RenderTextureMemoryless.None,
            msaaSamples = 1,
            shadowSamplingMode = UnityEngine.Rendering.ShadowSamplingMode.None,
            sRGB = false,
            useMipMap = false,
            volumeDepth = 2,
            vrUsage = VRTextureUsage.None,
            width = 1
        });
        rt.Create();
        RenderTexture tex = new RenderTexture(new RenderTextureDescriptor
        {
            autoGenerateMips = false,
            bindMS = false,
            colorFormat = RenderTextureFormat.ARGBHalf,
            depthBufferBits = 0,
            dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
            enableRandomWrite = false,
            height = 1,
            memoryless = RenderTextureMemoryless.None,
            msaaSamples = 1,
            shadowSamplingMode = UnityEngine.Rendering.ShadowSamplingMode.None,
            sRGB = false,
            useMipMap = false,
            volumeDepth = 2,
            vrUsage = VRTextureUsage.None,
            width = 1
        });
        tex.Create();
        buffer.Blit(null, tex, setColor);
        buffer.CopyTexture(tex, 0, rt, 0);
        buffer.SetGlobalTexture(ShaderIDs._MainTex, rt);
        Graphics.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}
