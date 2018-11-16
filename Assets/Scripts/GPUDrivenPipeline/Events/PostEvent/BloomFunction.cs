using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using PPSShaderIDs = UnityEngine.Rendering.PostProcessing.ShaderIDs;
namespace MPipeline
{
    public struct BloomData
    {
        public Level[] m_Pyramid;
        public struct Level
        {
            public RenderTexture down;
            public RenderTexture up;
        }
        public Material bloomMaterial;
        public Bloom settings;
        public bool enabledInUber;
        public bool isFastMode;
    }
    public static class BloomFunction
    {
        const int k_MaxPyramidSize = 16; // Just to make sure we handle 64k screens... Future-proof!
        enum Pass
        {
            Prefilter13,
            Prefilter4,
            Downsample13,
            Downsample4,
            UpsampleTent,
            UpsampleBox,
            DebugOverlayThreshold,
            DebugOverlayTent,
            DebugOverlayBox
        }

        public static void Init(ref BloomData data, PostProcessResources postResources)
        {
            data.bloomMaterial = new Material(postResources.shaders.bloom);
            data.m_Pyramid = new BloomData.Level[k_MaxPyramidSize];

            for (int i = 0; i < k_MaxPyramidSize; i++)
            {
                data.m_Pyramid[i] = new BloomData.Level
                {
                    down = null,
                    up = null
                };
            }
            data.enabledInUber = false;
            data.isFastMode = false;
        }

        public static void Finalize(ref BloomData data, ref PostSharedData sharedData)
        {
            var uberMaterial = sharedData.uberMaterial;
            Object.Destroy(data.bloomMaterial);
        }
        /*
        public static void Render(ref BloomData data, ref PostSharedData context, RenderTexture source)
        {
            if (data.isFastMode != data.settings.fastMode || data.settings.active != data.enabledInUber)
            {
                data.isFastMode = data.settings.fastMode;
                data.enabledInUber = data.settings.active;
                context.keywordsTransformed = true;
                context.shaderKeywords.Remove("BLOOM");
                context.shaderKeywords.Remove("BLOOM_LOW");
                if (data.enabledInUber)
                {
                    context.shaderKeywords.Add(data.isFastMode ? "BLOOM_LOW" : "BLOOM");
                }
            }
            var settings = data.settings;
            var bloomMaterial = data.bloomMaterial;
            // Apply auto exposure adjustment in the prefiltering pass
            bloomMaterial.SetTexture(PPSShaderIDs.AutoExposureTex, context.autoExposureTexture);

            // Negative anamorphic ratio values distort vertically - positive is horizontal
            float ratio = Mathf.Clamp(settings.anamorphicRatio, -1, 1);
            float rw = ratio < 0 ? -ratio : 0f;
            float rh = ratio > 0 ? ratio : 0f;

            // Do bloom on a half-res buffer, full-res doesn't bring much and kills performances on
            // fillrate limited platforms
            int tw = Mathf.FloorToInt(context.screenSize.x / (2f - rw));
            int th = Mathf.FloorToInt(context.screenSize.y / (2f - rh));

            // Determine the iteration count
            int s = Mathf.Max(tw, th);
            float logs = Mathf.Log(s, 2f) + Mathf.Min(settings.diffusion.value, 10f) - 10f;
            int logs_i = Mathf.FloorToInt(logs);
            int iterations = Mathf.Clamp(logs_i, 1, k_MaxPyramidSize);
            float sampleScale = 0.5f + logs - logs_i;
            bloomMaterial.SetFloat(PPSShaderIDs.SampleScale, sampleScale);

            // Prefiltering parameters
            float lthresh = Mathf.GammaToLinearSpace(settings.threshold.value);
            float knee = lthresh * settings.softKnee.value + 1e-5f;
            var threshold = new Vector4(lthresh, lthresh - knee, knee * 2f, 0.25f / knee);
            bloomMaterial.SetVector(PPSShaderIDs.Threshold, threshold);
            float lclamp = Mathf.GammaToLinearSpace(settings.clamp.value);
            bloomMaterial.SetVector(PPSShaderIDs.Params, new Vector4(lclamp, 0f, 0f, 0f));

            int qualityOffset = settings.fastMode ? 1 : 0;

            // Downsample
            RenderTexture lastDown = source;
            for (int i = 0; i < iterations; i++)
            {
                ref RenderTexture mipDown = ref data.m_Pyramid[i].down;
                ref RenderTexture mipUp = ref data.m_Pyramid[i].up;
                int pass = i == 0
                    ? (int)Pass.Prefilter13 + qualityOffset
                    : (int)Pass.Downsample13 + qualityOffset;

                mipDown = PipelineFunctions.GetTemporary(tw, th, 0, source.format, RenderTextureReadWrite.Default, FilterMode.Bilinear, context.temporalRT);
                mipUp = PipelineFunctions.GetTemporary(tw, th, 0, source.format, RenderTextureReadWrite.Default, FilterMode.Bilinear, context.temporalRT);
                PostFunctions.BlitFullScreen(lastDown, mipDown, bloomMaterial, pass);
                lastDown = mipDown;
                tw /= 2;
                tw = Mathf.Max(tw, 1);
                th = Mathf.Max(th / 2, 1);
            }

            // Upsample
            RenderTexture lastUp = data.m_Pyramid[iterations - 1].down;
            for (int i = iterations - 2; i >= 0; i--)
            {
                RenderTexture mipDown = data.m_Pyramid[i].down;
                RenderTexture mipUp = data.m_Pyramid[i].up;
                Shader.SetGlobalTexture(PPSShaderIDs.BloomTex, mipDown);
                PostFunctions.BlitFullScreen(lastUp, mipUp, bloomMaterial, (int)Pass.UpsampleTent + qualityOffset);
                lastUp = mipUp;
            }

            var linearColor = settings.color.value.linear;
            float intensity = RuntimeUtilities.Exp2(settings.intensity.value / 10f) - 1f;
            var shaderSettings = new Vector4(sampleScale, intensity, settings.dirtIntensity.value, iterations);

            // Lens dirtiness
            // Keep the aspect ratio correct & center the dirt texture, we don't want it to be
            // stretched or squashed
            var dirtTexture = settings.dirtTexture.value == null
                ? RuntimeUtilities.blackTexture
                : settings.dirtTexture.value;

            var dirtRatio = (float)dirtTexture.width / (float)dirtTexture.height;
            var screenRatio = (float)context.screenSize.x / (float)context.screenSize.y;
            var dirtTileOffset = new Vector4(1f, 1f, 0f, 0f);

            if (dirtRatio > screenRatio)
            {
                dirtTileOffset.x = screenRatio / dirtRatio;
                dirtTileOffset.z = (1f - dirtTileOffset.x) * 0.5f;
            }
            else if (screenRatio > dirtRatio)
            {
                dirtTileOffset.y = dirtRatio / screenRatio;
                dirtTileOffset.w = (1f - dirtTileOffset.y) * 0.5f;
            }
            var uberMaterial = context.uberMaterial;
            if (settings.fastMode)
                uberMaterial.EnableKeyword("BLOOM_LOW");
            else
                uberMaterial.EnableKeyword("BLOOM");
            uberMaterial.SetVector(PPSShaderIDs.Bloom_DirtTileOffset, dirtTileOffset);
            uberMaterial.SetVector(PPSShaderIDs.Bloom_Settings, shaderSettings);
            uberMaterial.SetColor(PPSShaderIDs.Bloom_Color, linearColor);
            uberMaterial.SetTexture(PPSShaderIDs.Bloom_DirtTex, dirtTexture);
            Shader.SetGlobalTexture(PPSShaderIDs.BloomTex, lastUp);
        }*/
    }
}
