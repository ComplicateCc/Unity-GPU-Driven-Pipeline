using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
namespace MPipeline
{
    [RequireComponent(typeof(Camera))]
    public class PipelineCamera : MonoBehaviour
    {
        [System.NonSerialized]
        public Camera cam;
        [System.NonSerialized]
        public RenderTargets targets;
        public RenderPipeline.CameraRenderingPath renderingPath = RenderPipeline.CameraRenderingPath.GPUDeferred;
        public List<RenderTexture> temporalRT
        {
            get
            {
                return temporaryTextures;
            }
        }
        private List<RenderTexture> temporaryTextures = new List<RenderTexture>(15);
        public Dictionary<Type, IPerCameraData> postDatas = new Dictionary<Type, IPerCameraData>(47);
        void Awake()
        {
            cam = GetComponent<Camera>();
            targets = RenderTargets.Init();
        }

        private void OnDisable()
        {
            foreach (var i in postDatas.Values)
            {
                i.DisposeProperty();
            }
            postDatas.Clear();
        }

        public void RenderSRP(RenderTargetIdentifier destination, ref ScriptableRenderContext context)
        {
            RenderPipeline.current.Render(renderingPath, this, destination, ref context);
            context.Submit();
            PipelineFunctions.ReleaseRenderTarget(temporaryTextures);
        }
    }
}
