using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
namespace MPipeline
{
    public delegate void PostProcessAction(ref PipelineCommandData data, CommandBuffer buffer, RenderTargetIdentifier source, RenderTargetIdentifier dest);
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
            data = default;
            data.uberMaterial = new Material(Shader.Find("Hidden/PostProcessing/Uber"));
            data.resources = resources;
            data.temporalRT = new List<RenderTexture>(20);
            data.shaderKeywords = new List<string>(10);
            data.keywordsTransformed = true;
        }

        public static void RunPostProcess(ref RenderTargets targets, CommandBuffer buffer, ref PipelineCommandData data, PostProcessAction renderFunc)
        {
            renderFunc(ref data, buffer, targets.renderTargetIdentifier, targets.backupIdentifier);
            int back = targets.backupIdentifier;
            targets.backupIdentifier = targets.renderTargetIdentifier;
            targets.renderTargetIdentifier = back;
        }
    }
}