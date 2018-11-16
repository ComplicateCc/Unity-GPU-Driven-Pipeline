using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System;
using ColorUtilities = UnityEngine.Rendering.PostProcessing.ColorUtilities;
using ColorGradingSetting = UnityEngine.Rendering.PostProcessing.ColorGrading;
namespace MPipeline
{
    public struct ColorGradingData
    {
        public int kernel;
        public Texture2D m_GradingCurves;
        public Color[] m_Pixels;
        public RenderTexture m_InternalLogLut;
        public ColorGradingSetting settings;
        public bool enabledInUber;
    }

    public static class ColorGradingFunction
    {
        #region STATIC_CONST_FIELD
        static readonly int _Output = Shader.PropertyToID("_Output");
        static readonly int _Size = Shader.PropertyToID("_Size");
        static readonly int _ColorBalance = Shader.PropertyToID("_ColorBalance");
        static readonly int _ColorFilter = Shader.PropertyToID("_ColorFilter");
        static readonly int _ChannelMixerRed = Shader.PropertyToID("_ChannelMixerRed");
        static readonly int _ChannelMixerGreen = Shader.PropertyToID("_ChannelMixerGreen");
        static readonly int _ChannelMixerBlue = Shader.PropertyToID("_ChannelMixerBlue");
        static readonly int _HueSatCon = Shader.PropertyToID("_HueSatCon");
        static readonly int _Lift = Shader.PropertyToID("_Lift");
        static readonly int _InvGamma = Shader.PropertyToID("_InvGamma");
        static readonly int _Gain = Shader.PropertyToID("_Gain");
        static readonly int _Curves = Shader.PropertyToID("_Curves");
        #endregion
        const int k_Lut3DSize = 33;
        public const int k_Precision = 128;
        public static void InitializeColorGrading(ColorGradingSetting settings, ref ColorGradingData data, ComputeShader lut3dShader)
        {
            data.settings = settings;
            data.enabledInUber = false;
            data.m_Pixels = new Color[k_Precision * 2];
            data.m_InternalLogLut = null;
            data.m_GradingCurves = null;
            data.settings.hueVsHueCurve.value.Cache(Time.renderedFrameCount);
            data.settings.hueVsSatCurve.value.Cache(Time.renderedFrameCount);
            data.settings.satVsSatCurve.value.Cache(Time.renderedFrameCount);
            data.settings.lumVsSatCurve.value.Cache(Time.renderedFrameCount);
            data.kernel = lut3dShader.FindKernel("KGenLut3D_AcesTonemap");
        }
        public static void Finalize(ref ColorGradingData data, Material uberMaterial)
        {
            if (data.m_InternalLogLut)
            {
                data.m_InternalLogLut.Release();
                UnityEngine.Object.Destroy(data.m_InternalLogLut);
            }
            if (data.m_GradingCurves)
                UnityEngine.Object.Destroy(data.m_GradingCurves);
        }
        public static void PrepareRender(ref ColorGradingData data, ref PostSharedData sharedData)
        {
            Material uberMaterial = sharedData.uberMaterial;
            if (data.settings.active != data.enabledInUber)
            {
                data.enabledInUber = data.settings.active;
                sharedData.keywordsTransformed = true;
                if (data.enabledInUber)
                {
                    sharedData.shaderKeywords.Add("COLOR_GRADING_HDR_3D");
                }
                else
                {
                    sharedData.shaderKeywords.Remove("COLOR_GRADING_HDR_3D");
                }
            }
            if (!data.settings.enabled)
            {
                return;
            }
            CheckInternalLogLut(ref data.m_InternalLogLut);
            // Lut setup
            var compute = sharedData.resources.computeShaders.lut3DBaker;
            var kernel = data.kernel;
            var settings = data.settings;
            compute.SetTexture(data.kernel, _Output, data.m_InternalLogLut);
            compute.SetVector(_Size, new Vector4(k_Lut3DSize, 1f / (k_Lut3DSize - 1f), 0f, 0f));
            var colorBalance = ColorUtilities.ComputeColorBalance(settings.temperature, settings.tint);
            compute.SetVector(_ColorBalance, colorBalance);
            compute.SetVector(_ColorFilter, settings.colorFilter);

            float hue = settings.hueShift / 360f;         // Remap to [-0.5;0.5]
            float sat = settings.saturation / 100f + 1f;  // Remap to [0;2]
            float con = settings.contrast / 100f + 1f;    // Remap to [0;2]
            compute.SetVector(_HueSatCon, new Vector4(hue, sat, con, 0f));

            var channelMixerR = new Vector4(settings.mixerRedOutRedIn, settings.mixerRedOutGreenIn, settings.mixerRedOutBlueIn, 0f);
            var channelMixerG = new Vector4(settings.mixerGreenOutRedIn, settings.mixerGreenOutGreenIn, settings.mixerGreenOutBlueIn, 0f);
            var channelMixerB = new Vector4(settings.mixerBlueOutRedIn, settings.mixerBlueOutGreenIn, settings.mixerBlueOutBlueIn, 0f);
            compute.SetVector(_ChannelMixerRed, channelMixerR / 100f); // Remap to [-2;2]
            compute.SetVector(_ChannelMixerGreen, channelMixerG / 100f);
            compute.SetVector(_ChannelMixerBlue, channelMixerB / 100f);

            var liftV = ColorUtilities.ColorToLift(settings.lift.value * 0.2f);
            var gainV = ColorUtilities.ColorToGain(settings.gain.value * 0.8f);
            var invgamma = ColorUtilities.ColorToInverseGamma(settings.gamma.value * 0.8f);
            compute.SetVector(_Lift, new Vector4(liftV.x, liftV.y, liftV.z, 0f));
            compute.SetVector(_InvGamma, new Vector4(invgamma.x, invgamma.y, invgamma.z, 0f));
            compute.SetVector(_Gain, new Vector4(gainV.x, gainV.y, gainV.z, 0f));

            compute.SetTexture(kernel, _Curves, GetCurveTexture(ref data));

            int groupSize = Mathf.CeilToInt(k_Lut3DSize / 4f);
            compute.Dispatch(kernel, groupSize, groupSize, groupSize);
            var lut = data.m_InternalLogLut;
            uberMaterial.SetTexture(ShaderIDs._Lut3D, lut);
            uberMaterial.SetVector(ShaderIDs._Lut3D_Params, new Vector2(1f / lut.width, lut.width - 1f));
            uberMaterial.SetFloat(ShaderIDs._PostExposure, Mathf.Exp(settings.postExposure * 0.69314718055994530941723212145818f));
        }

        private static void CheckInternalLogLut(ref RenderTexture m_InternalLogLut)
        {
            // Check internal lut state, (re)create it if needed
            if (m_InternalLogLut == null)
            {
                var format = RenderTextureFormat.ARGBHalf;
                m_InternalLogLut = new RenderTexture(k_Lut3DSize, k_Lut3DSize, 0, format, RenderTextureReadWrite.Linear)
                {
                    name = "Color Grading Log Lut",
                    dimension = TextureDimension.Tex3D,
                    hideFlags = HideFlags.DontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    anisoLevel = 0,
                    enableRandomWrite = true,
                    volumeDepth = k_Lut3DSize,
                    autoGenerateMips = false,
                    useMipMap = false
                };
                m_InternalLogLut.Create();
            }
            else if (!m_InternalLogLut.IsCreated())
                m_InternalLogLut.Create();
        }

        private static Texture2D GetCurveTexture(ref ColorGradingData data)
        {
            if (data.m_GradingCurves == null)
            {
                var format = TextureFormat.RGBAHalf;
                data.m_GradingCurves = new Texture2D(k_Precision, 2, format, false, true)
                {
                    name = "Internal Curves Texture",
                    hideFlags = HideFlags.DontSave,
                    anisoLevel = 0,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
            }

            var pixels = data.m_Pixels;
            var settings = data.settings;
            for (int i = 0; i < k_Precision; i++)
            {
                // Secondary/VS curves
                float x = settings.hueVsHueCurve.value.cachedData[i];
                float y = settings.hueVsSatCurve.value.cachedData[i];
                float z = settings.satVsSatCurve.value.cachedData[i];
                float w = settings.lumVsSatCurve.value.cachedData[i];
                pixels[i] = new Color(x, y, z, w);
            }

            data.m_GradingCurves.SetPixels(pixels);
            data.m_GradingCurves.Apply(false, false);

            return data.m_GradingCurves;
        }
    }
}