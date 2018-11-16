using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Rendering;
namespace MPipeline
{
    [PipelineEvent(true, true)]
    public class TemporalAAEvent : PipelineEvent
    {
        [Tooltip("The diameter (in texels) inside which jitter samples are spread. Smaller values result in crisper but more aliased output, while larger values result in more stable but blurrier output.")]
        [Range(0.1f, 1f)]
        public float jitterSpread = 0.75f;

        [Tooltip("Controls the amount of sharpening applied to the color buffer. High values may introduce dark-border artifacts.")]
        [Range(0f, 3f)]
        public float sharpness = 0.25f;

        [Tooltip("The blend coefficient for a stationary fragment. Controls the percentage of history sample blended into the final color.")]
        [Range(0f, 0.99f)]
        public float stationaryBlending = 0.95f;

        [Tooltip("The blend coefficient for a fragment with significant motion. Controls the percentage of history sample blended into the final color.")]
        [Range(0f, 0.99f)]
        public float motionBlending = 0.9f;
        [Tooltip("Screen Space AABB Bounding for stationary state(Larger will take less flask but more ghost)")]
        [Range(0.05f, 6f)]
        public float stationaryAABBScale = 1.25f;
        [Tooltip("Screen Space AABB Bounding for motion state(Larger will take less flask but more ghost)")]
        [Range(0.05f, 6f)]
        public float motionAABBScale = 0.5f;
        private Vector2 jitter;
        private int sampleIndex = 0;
        private const int k_SampleCount = 8;
        private Material taaMat;
        private MaterialPropertyBlock block;
        private PostProcessAction taaFunction;
        private RenderTexture historyTex;
        private System.Func<PipelineCamera, HistoryTexture> GetHistoryTex = (cam) => new HistoryTexture(cam.cam);
        protected override void Init(PipelineResources resources)
        {
            block = new MaterialPropertyBlock();
            taaMat = new Material(resources.taaShader);
            taaFunction = (ref PipelineCommandData data, CommandBuffer buffer, RenderTexture source, RenderTexture dest) =>
            {
                buffer.BlitSRT(block, source, dest, taaMat, 0);
                buffer.Blit(dest, historyTex);
            };
        }

        protected override void Dispose()
        {
            Destroy(taaMat);
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            HistoryTexture texComponent = IPerCameraData.GetProperty<HistoryTexture>(cam, GetHistoryTex);
            texComponent.UpdateProperty(cam);
            SetHistory(cam.cam, buffer, ref texComponent.historyTex, cam.targets.renderTarget);
            historyTex = texComponent.historyTex;
            //TAA Start
            const float kMotionAmplification_Blending = 100f * 60f;
            const float kMotionAmplification_Bounding = 100f * 30f;
            block.SetVector(ShaderIDs._Jitter, jitter);
            block.SetFloat(ShaderIDs._Sharpness, sharpness);
            block.SetVector(ShaderIDs._TemporalClipBounding, new Vector4(stationaryAABBScale, motionAABBScale, kMotionAmplification_Bounding, 0f));
            block.SetVector(ShaderIDs._FinalBlendParameters, new Vector4(stationaryBlending, motionBlending, kMotionAmplification_Blending, 0f));
            block.SetTexture(ShaderIDs._HistoryTex, historyTex);
            PostFunctions.RunPostProcess(ref cam.targets, buffer, ref data, taaFunction);
        }

        public override void PreRenderFrame(PipelineCamera cam, ref PipelineCommandData data)
        {
            cam.cam.ResetProjectionMatrix();
            ConfigureJitteredProjectionMatrix(cam.cam);
        }

        Vector2 GenerateRandomOffset()
        {
            var offset = new Vector2(
                    HaltonSeq.Get((sampleIndex & 1023) + 1, 2) - 0.5f,
                    HaltonSeq.Get((sampleIndex & 1023) + 1, 3) - 0.5f
                );

            if (++sampleIndex >= k_SampleCount)
                sampleIndex = 0;

            return offset;
        }

        public Matrix4x4 GetJitteredProjectionMatrix(Camera camera)
        {
            Matrix4x4 cameraProj;
            jitter = GenerateRandomOffset();
            jitter *= jitterSpread;
            cameraProj = camera.orthographic
                ? RuntimeUtilities.GetJitteredOrthographicProjectionMatrix(camera, jitter)
                : RuntimeUtilities.GetJitteredPerspectiveProjectionMatrix(camera, jitter);
            jitter = new Vector2(jitter.x / camera.pixelWidth, jitter.y / camera.pixelHeight);
            return cameraProj;
        }

        public void ConfigureJitteredProjectionMatrix(Camera camera)
        {
            camera.nonJitteredProjectionMatrix = camera.projectionMatrix;
            camera.projectionMatrix = GetJitteredProjectionMatrix(camera);
            camera.useJitteredProjectionMatrixForTransparentRendering = false;
        }

        public void SetHistory(Camera cam, CommandBuffer buffer, ref RenderTexture history, RenderTexture renderTarget)
        {
            if (history == null)
            {
                history = new RenderTexture(cam.pixelWidth, cam.pixelHeight, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                history.filterMode = FilterMode.Bilinear;
                buffer.Blit(renderTarget, history);
            }
            else if (history.width != cam.pixelWidth || history.height != cam.pixelHeight)
            {
                history.Release();
                Destroy(history);
                history = new RenderTexture(cam.pixelWidth, cam.pixelHeight, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                history.filterMode = FilterMode.Bilinear;
                buffer.Blit(renderTarget, history);
            }
        }
    }

    public class HistoryTexture : IPerCameraData
    {
        public RenderTexture historyTex;
        public HistoryTexture(Camera cam)
        {
            historyTex = new RenderTexture(cam.pixelWidth, cam.pixelHeight, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        }

        public override void DisposeProperty()
        {
            historyTex.Release();
            Object.Destroy(historyTex);
        }
        public void UpdateProperty(PipelineCamera camera)
        {
            int camWidth = camera.cam.pixelWidth;
            int camHeight = camera.cam.pixelHeight;
            if (historyTex.width != camWidth || historyTex.height != camHeight)
            {
                historyTex.Release();
                Object.Destroy(historyTex);
                historyTex = new RenderTexture(camWidth, camHeight, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            }
        }
    }
}