using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class GraphicsUtility
{
    /// <summary>
    /// Full Screen triangle Mesh
    /// </summary>
    public static Mesh mesh
    {
        get
        {
            if (m_mesh != null)
                return m_mesh;
            m_mesh = new Mesh();
            m_mesh.vertices = new Vector3[] {
                new Vector3(-1,-1,0f),
                new Vector3(-1,1,0f),
                new Vector3(1,1,0f),
                new Vector3(1,-1,0f)
            };
            m_mesh.uv = new Vector2[] {
                new Vector2(0,1),
                new Vector2(0,0),
                new Vector2(1,0),
                new Vector2(1,1)
            };

            m_mesh.SetIndices(new int[] { 0, 1, 2, 0, 3, 2 }, MeshTopology.Triangles, 0);
            return m_mesh;
        }
    }

    public static Mesh cubeMesh
    {
        get
        {
            if (m_cubeMesh != null) return m_cubeMesh;
            m_cubeMesh = new Mesh();
            m_cubeMesh.vertices = new Vector3[]
            {
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
            };
            m_cubeMesh.normals = new Vector3[]
            {
                new Vector3(0f, 0f, 1f),
new Vector3(0f, 0f, 1f),
new Vector3(0f, 0f, 1f),
new Vector3(0f, 0f, 1f),
new Vector3(0f, 1f, 0f),
new Vector3(0f, 1f, 0f),
new Vector3(0f, 0f, -1f),
new Vector3(0f, 0f, -1f),
new Vector3(0f, 1f, 0f),
new Vector3(0f, 1f, 0f),
new Vector3(0f, 0f, -1f),
new Vector3(0f, 0f, -1f),
new Vector3(0f, -1f, 0f),
new Vector3(0f, -1f, 0f),
new Vector3(0f, -1f, 0f),
new Vector3(0f, -1f, 0f),
new Vector3(-1f, 0f, 0f),
new Vector3(-1f, 0f, 0f),
new Vector3(-1f, 0f, 0f),
new Vector3(-1f, 0f, 0f),
new Vector3(1f, 0f, 0f),
new Vector3(1f, 0f, 0f),
new Vector3(1f, 0f, 0f),
new Vector3(1f, 0f, 0f)

            };
            m_cubeMesh.triangles = new int[]
            {
                0, 2, 3, 0, 3, 1, 8, 4, 5, 8, 5, 9, 10, 6, 7, 10, 7, 11, 12, 13, 14, 12, 14, 15, 16, 17, 18, 16, 18, 19, 20, 21, 22, 20, 22, 23,
            };
            return m_cubeMesh;
        }
    }
    private static Mesh m_cubeMesh;
    private static Mesh m_mesh;
    public static void BlitMRT(this CommandBuffer buffer, RenderTargetIdentifier[] colorIdentifier, RenderTargetIdentifier depthIdentifier, Material mat, int pass)
    {
        buffer.SetRenderTarget(colorIdentifier, depthIdentifier);
        buffer.DrawMesh(mesh, Matrix4x4.identity, mat, 0, pass);
    }

    public static void BlitSRT(this CommandBuffer buffer, RenderTargetIdentifier destination, Material mat, int pass)
    {
        buffer.SetRenderTarget(destination);
        buffer.DrawMesh(mesh, Matrix4x4.identity, mat, 0, pass);
    }

    public static void BlitMRT(this CommandBuffer buffer, Texture source, RenderTargetIdentifier[] colorIdentifier, RenderTargetIdentifier depthIdentifier, Material mat, int pass)
    {
        buffer.SetRenderTarget(colorIdentifier, depthIdentifier);
        buffer.DrawMesh(mesh, Matrix4x4.identity, mat, 0, pass);
    }

    public static void BlitSRT(this CommandBuffer buffer, Texture source, RenderTargetIdentifier destination, Material mat, int pass)
    {
        buffer.SetGlobalTexture(ShaderIDs._MainTex, source);
        buffer.SetRenderTarget(destination);
        buffer.DrawMesh(mesh, Matrix4x4.identity, mat, 0, pass);
    }

    public static void BlitSRT(this CommandBuffer buffer, RenderTargetIdentifier source, RenderTargetIdentifier destination, Material mat, int pass)
    {
        buffer.SetGlobalTexture(ShaderIDs._MainTex, source);
        buffer.SetRenderTarget(destination);
        buffer.DrawMesh(mesh, Matrix4x4.identity, mat, 0, pass);
    }//Use This

    public static void BlitSRT(this CommandBuffer buffer, MaterialPropertyBlock block, RenderTargetIdentifier source, RenderTargetIdentifier destination, Material mat, int pass)
    {
        buffer.SetGlobalTexture(ShaderIDs._MainTex, source);
        buffer.SetRenderTarget(destination);
        buffer.DrawMesh(mesh, Matrix4x4.identity, mat, 0, pass, block);
    }//Use This

    public static void BlitStencil(this CommandBuffer buffer, RenderTargetIdentifier colorSrc, RenderTargetIdentifier colorBuffer, RenderTargetIdentifier depthStencilBuffer, Material mat, int pass)
    {
        buffer.SetGlobalTexture(ShaderIDs._MainTex, colorSrc);
        buffer.SetRenderTarget(colorBuffer, depthStencilBuffer);
        buffer.DrawMesh(mesh, Matrix4x4.identity, mat, 0, pass);
    }//UseThis

    public static void BlitStencil(this CommandBuffer buffer, RenderTargetIdentifier colorBuffer, RenderTargetIdentifier depthStencilBuffer, Material mat, int pass)
    {
        buffer.SetRenderTarget(colorBuffer, depthStencilBuffer);
        buffer.DrawMesh(mesh, Matrix4x4.identity, mat, 0, pass);
    }
}
