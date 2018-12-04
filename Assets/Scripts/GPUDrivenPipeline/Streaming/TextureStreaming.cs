using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public unsafe class TextureStreaming : System.IDisposable
{
    private ComputeBuffer constBuffer;
    private Material copyMaterial;
    private int width;
    private int height;
    public TextureStreaming(int width, int height)
    {
        this.width = width;
        this.height = height;
    }

    public void Dispose()
    {
        constBuffer.Dispose();
        Object.Destroy(copyMaterial);
    }
    static readonly int _TextureBuffer = Shader.PropertyToID("_TextureBuffer");
    static readonly int _TextureSize = Shader.PropertyToID("_TextureSize");
    public void ReadIn(NativeArray<Color> colors, RenderTexture rt, int depthSlice)
    {
        constBuffer.SetData(colors);
        Shader.SetGlobalVector(_TextureSize, new Vector4(width, height));
        Shader.SetGlobalBuffer(_TextureBuffer, constBuffer);
        Graphics.SetRenderTarget(rt, 0, CubemapFace.Unknown, depthSlice);
        copyMaterial.SetPass(0);
        Graphics.DrawMeshNow(GraphicsUtility.mesh, Matrix4x4.identity);
    }

    public static Color[] GetColors(Texture2D tex)
    {
        if(!tex.isReadable)
        {
            Debug.LogError(tex.name + " is not readable!");
            return null;
        }

        Color[] value = tex.GetPixels();
        for (int i = 0; i < value.Length; ++i)
        {
            ref var col = ref value[i];
            col.r = Mathf.Pow(col.r, 2.2f);
            col.g = Mathf.Pow(col.g, 2.2f);
            col.b = Mathf.Pow(col.b, 2.2f);
            col.a = Mathf.Pow(col.a, 2.2f);
        }
        return value;
    }
}
