using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
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
        private List<RenderTexture> temporaryTextures = new List<RenderTexture>(15);
        public Dictionary<Type, IPerCameraData> postDatas = new Dictionary<Type, IPerCameraData>(47);
        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.renderingPath = RenderingPath.Forward;
            cam.cullingMask = 0;
            targets = RenderTargets.Init();
        }

        private void OnDisable()
        {
            foreach(var i in postDatas.Values)
            {
                i.DisposeProperty();
            }
            postDatas.Clear();
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (RenderPipeline.current)
            {
                PipelineFunctions.InitRenderTarget(ref targets, cam, temporaryTextures);
                RenderPipeline.current.Render(renderingPath, this, destination);
                PipelineFunctions.ReleaseRenderTarget(temporaryTextures);
            }
            else
            {
                Graphics.Blit(source, destination);
            }
        }
    }
}
