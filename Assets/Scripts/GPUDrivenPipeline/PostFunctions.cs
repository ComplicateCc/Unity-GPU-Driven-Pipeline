using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
namespace MPipeline
{
    public delegate void PostProcessAction(ref PipelineCommandData data, CommandBuffer buffer, RenderTexture source, RenderTexture dest);
    public struct PostSharedData
    {
        public Material uberMaterial;
        public Vector2Int screenSize;
        public PostProcessResources resources;
        public Texture autoExposureTexture;
        public List<RenderTexture> temporalRT;
        public List<string> shaderKeywords;
        public bool keywordsTransformed;
    }
    public static class PostFunctions
    {
        public static void InitSharedData(ref PostSharedData data, PostProcessResources resources)
        {
            data = default(PostSharedData);
            data.uberMaterial = new Material(Shader.Find("Hidden/PostProcessing/Uber"));
            data.resources = resources;
            data.temporalRT = new List<RenderTexture>(20);
            data.shaderKeywords = new List<string>(10);
            data.keywordsTransformed = true;
        }

        public static void RunPostProcess(ref RenderTargets targets, CommandBuffer buffer, ref PipelineCommandData data, PostProcessAction renderFunc)
        {
            RenderTexture source = targets.renderTarget;
            RenderTexture dest = targets.backupTarget;
            renderFunc(ref data, buffer, source, dest);
            targets.renderTarget = dest;
            targets.backupTarget = source;
            RenderTargetIdentifier back = targets.backupIdentifier;
            targets.backupIdentifier = targets.renderTargetIdentifier;
            targets.renderTargetIdentifier = back;
        }

        public static void BlitFullScreen(this CommandBuffer buffer, RenderTexture source, RenderTexture dest, Material mat, MaterialPropertyBlock block, int pass)
        {
            block.SetTexture(ShaderIDs._MainTex, source);
            buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, mat, 0, pass, block);
        }


        public static void BlitFullScreen(this CommandBuffer buffer, RenderTexture dest, Material mat, MaterialPropertyBlock block, int pass)
        {
            buffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, mat, 0, pass, block);
        }
    }
}