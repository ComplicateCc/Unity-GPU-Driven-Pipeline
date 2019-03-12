using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace MPipeline
{
    [System.Serializable]
    public class StochasticScreenSpaceReflection
    {
        public bool enabled;
        private enum RenderResolution
        {
            Full = 1,
            Half = 2
        };

        private enum DebugPass
        {
            Combine = 9,
            SSRColor = 10
        };

        private enum TraceApprox
        {
            HiZTrace = 0,
            LinearTrace = 1
        };


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        [Header("Common Property")]

        [SerializeField]
        TraceApprox TraceMethod = TraceApprox.HiZTrace;


        [SerializeField]
        RenderResolution RayCastingResolution = RenderResolution.Full;


        [SerializeField]
        bool Denoise = true;


        [Range(1, 8)]
        [SerializeField]
        int RayNums = 1;


        [Range(0, 1)]
        [SerializeField]
        float BRDFBias = 0.7f;


        [Range(0.05f, 5)]
        [SerializeField]
        float Thickness = 0.1f;


        [Range(0, 0.5f)]
        [SerializeField]
        float ScreenFade = 0.1f;



        [Header("HiZ_Trace Property")]

        [Range(64, 512)]
        [SerializeField]
        int HiZ_RaySteps = 64;


        [Range(0, 4)]
        [SerializeField]
        int HiZ_MaxLevel = 4;


        [Range(0, 2)]
        [SerializeField]
        int HiZ_StartLevel = 1;


        [Range(0, 2)]
        [SerializeField]
        int HiZ_StopLevel = 0;



        [Header("Linear_Trace Property")]

        [SerializeField]
        bool Linear_TowardRay = true;


        [SerializeField]
        bool Linear_TraceBehind = false;


        [Range(64, 512)]
        [SerializeField]
        int Linear_RayDistance = 128;


        [Range(64, 512)]
        [SerializeField]
        int Linear_RaySteps = 256;


        [Range(5, 20)]
        [SerializeField]
        int Linear_StepSize = 10;



        [Header("Filtter Property")]

        [SerializeField]
        Texture2D BlueNoise_LUT = null;

        [SerializeField]
        Texture2D preint;

        [Range(1, 4)]
        [SerializeField]
        int SpatioSampler = 4;


        [Range(0, 0.99f)]
        [SerializeField]
        float TemporalWeight = 0.98f;


        [Range(1, 5)]
        [SerializeField]
        float TemporalScale = 1.25f;



        [Header("DeBug Property")]


        [SerializeField]
        bool RunTimeDebugMod = true;


        [SerializeField]
        DebugPass DeBugPass = DebugPass.SSRColor;


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private const int RenderPass_HiZ_Depth = 0;
        private const int RenderPass_Linear2D_SingelSPP = 1;
        private const int RenderPass_HiZ3D_SingelSpp = 2;
        private const int RenderPass_Linear2D_MultiSPP = 3;
        private const int RenderPass_HiZ3D_MultiSpp = 4;
        private const int RenderPass_Spatiofilter_SingleSPP = 5;
        private const int RenderPass_Spatiofilter_MultiSPP = 6;
        private const int RenderPass_Temporalfilter_SingleSPP = 7;
        private const int RenderPass_Temporalfilter_MultiSpp = 8;

        private Material StochasticScreenSpaceReflectionMaterial;

        private Vector2 RandomSampler = new Vector2(1, 1);


        private static int SSR_Jitter_ID = Shader.PropertyToID("_SSR_Jitter");
        private static int SSR_BRDFBias_ID = Shader.PropertyToID("_SSR_BRDFBias");
        private static int SSR_NumSteps_Linear_ID = Shader.PropertyToID("_SSR_NumSteps_Linear");
        private static int SSR_NumSteps_HiZ_ID = Shader.PropertyToID("_SSR_NumSteps_HiZ");
        private static int SSR_NumRays_ID = Shader.PropertyToID("_SSR_NumRays");
        private static int SSR_NumResolver_ID = Shader.PropertyToID("_SSR_NumResolver");
        private static int SSR_ScreenFade_ID = Shader.PropertyToID("_SSR_ScreenFade");
        private static int SSR_Thickness_ID = Shader.PropertyToID("_SSR_Thickness");
        private static int SSR_TemporalScale_ID = Shader.PropertyToID("_SSR_TemporalScale");
        private static int SSR_TemporalWeight_ID = Shader.PropertyToID("_SSR_TemporalWeight");
        private static int SSR_ScreenSize_ID = Shader.PropertyToID("_SSR_ScreenSize");
        private static int SSR_RayCastSize_ID = Shader.PropertyToID("_SSR_RayCastSize");
        private static int SSR_NoiseSize_ID = Shader.PropertyToID("_SSR_NoiseSize");
        private static int SSR_RayStepSize_ID = Shader.PropertyToID("_SSR_RayStepSize");
        private static int SSR_ProjInfo_ID = Shader.PropertyToID("_SSR_ProjInfo");
        private static int SSR_CameraClipInfo_ID = Shader.PropertyToID("_SSR_CameraClipInfo");
        private static int SSR_TraceDistance_ID = Shader.PropertyToID("_SSR_TraceDistance");
        private static int SSR_BackwardsRay_ID = Shader.PropertyToID("_SSR_BackwardsRay");
        private static int SSR_TraceBehind_ID = Shader.PropertyToID("_SSR_TraceBehind");
        private static int SSR_CullBack_ID = Shader.PropertyToID("_SSR_CullBack");
        private static int SSR_HiZ_PrevDepthLevel_ID = Shader.PropertyToID("_SSR_HiZ_PrevDepthLevel");
        private static int SSR_HiZ_MaxLevel_ID = Shader.PropertyToID("_SSR_HiZ_MaxLevel");
        private static int SSR_HiZ_StartLevel_ID = Shader.PropertyToID("_SSR_HiZ_StartLevel");
        private static int SSR_HiZ_StopLevel_ID = Shader.PropertyToID("_SSR_HiZ_StopLevel");



        private static int SSR_Noise_ID = Shader.PropertyToID("_SSR_Noise");
        private static int SSR_PreintegratedGF_LUT_ID = Shader.PropertyToID("_SSR_PreintegratedGF_LUT");

        private static int SSR_HierarchicalDepth_ID = Shader.PropertyToID("_SSR_HierarchicalDepth_RT");
        private static int SSR_SceneColor_ID = Shader.PropertyToID("_SSR_SceneColor_RT");

        private static int SSR_Trace_ID = Shader.PropertyToID("_SSR_RayCastRT");
        private static int SSR_Mask_ID = Shader.PropertyToID("_SSR_RayMask_RT");
        private static int SSR_Spatial_ID = Shader.PropertyToID("_SSR_Spatial_RT");
        private static int SSR_TemporalPrev_ID = Shader.PropertyToID("_SSR_TemporalPrev_RT");
        private static int SSR_TemporalCurr_ID = Shader.PropertyToID("_SSR_TemporalCurr_RT");



        private static int SSR_ProjectionMatrix_ID = Shader.PropertyToID("_SSR_ProjectionMatrix");
        private static int SSR_InverseProjectionMatrix_ID = Shader.PropertyToID("_SSR_InverseProjectionMatrix");
        private static int SSR_WorldToCameraMatrix_ID = Shader.PropertyToID("_SSR_WorldToCameraMatrix");
        private static int SSR_CameraToWorldMatrix_ID = Shader.PropertyToID("_SSR_CameraToWorldMatrix");
        private static int SSR_ProjectToPixelMatrix_ID = Shader.PropertyToID("_SSR_ProjectToPixelMatrix");

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private System.Func<PipelineCamera, SSRCameraData> getDataFunc;
        public void Init(PipelineResources res)
        {
            getDataFunc = (c) => new SSRCameraData(new Vector2Int(c.cam.pixelWidth, c.cam.pixelHeight), (int)RayCastingResolution);
            StochasticScreenSpaceReflectionMaterial = new Material(res.shaders.ssrShader);
        }

        public void Dispose()
        {
            Object.DestroyImmediate(StochasticScreenSpaceReflectionMaterial);
        }

        public RenderTexture Render(ref PipelineCommandData data, PipelineCamera cam, ReflectionEvent parentEvent)
        {
            RandomSampler = GenerateRandomOffset();
            SSRCameraData cameraData = IPerCameraData.GetProperty(cam, getDataFunc, parentEvent);
            SSR_UpdateVariable(cameraData, cam.cam, ref data);
            RenderScreenSpaceReflection(data.buffer, cameraData, cam);
            return cameraData.SSR_TemporalCurr_RT;
        }


        ////////////////////////////////////////////////////////////////SSR Function////////////////////////////////////////////////////////////////
        private int m_SampleIndex = 0;
        private const int k_SampleCount = 64;
        private float GetHaltonValue(int index, int radix)
        {
            float result = 0f;
            float fraction = 1f / radix;

            while (index > 0)
            {
                result += (index % radix) * fraction;
                index /= radix;
                fraction /= radix;
            }
            return result;
        }
        private Vector2 GenerateRandomOffset()
        {
            var offset = new Vector2(GetHaltonValue(m_SampleIndex & 1023, 2), GetHaltonValue(m_SampleIndex & 1023, 3));
            if (m_SampleIndex++ >= k_SampleCount)
                m_SampleIndex = 0;
            return offset;
        }



        private void SSR_UpdateUniformVariable(CommandBuffer buffer)
        {
            buffer.SetGlobalTexture(SSR_PreintegratedGF_LUT_ID, preint);
            buffer.SetGlobalTexture(SSR_Noise_ID, BlueNoise_LUT);
            buffer.SetGlobalVector(SSR_NoiseSize_ID, new Vector2(1024, 1024));
            buffer.SetGlobalFloat(SSR_BRDFBias_ID, BRDFBias);
            buffer.SetGlobalFloat(SSR_ScreenFade_ID, ScreenFade);
            buffer.SetGlobalFloat(SSR_Thickness_ID, Thickness);
            buffer.SetGlobalFloat(SSR_RayStepSize_ID, Linear_StepSize);
            buffer.SetGlobalFloat(SSR_TraceDistance_ID, Linear_RayDistance);
            buffer.SetGlobalInt(SSR_NumSteps_Linear_ID, Linear_RaySteps);
            buffer.SetGlobalInt(SSR_NumSteps_HiZ_ID, HiZ_RaySteps);
            buffer.SetGlobalInt(SSR_NumRays_ID, RayNums);
            buffer.SetGlobalInt(SSR_BackwardsRay_ID, Linear_TowardRay ? 1 : 0);
            buffer.SetGlobalInt(SSR_CullBack_ID, Linear_TowardRay ? 1 : 0);
            buffer.SetGlobalInt(SSR_TraceBehind_ID, Linear_TraceBehind ? 1 : 0);
            buffer.SetGlobalInt(SSR_HiZ_MaxLevel_ID, HiZ_MaxLevel);
            buffer.SetGlobalInt(SSR_HiZ_StartLevel_ID, HiZ_StartLevel);
            buffer.SetGlobalInt(SSR_HiZ_StopLevel_ID, HiZ_StopLevel);
            if (Denoise)
            {
                buffer.SetGlobalInt(SSR_NumResolver_ID, SpatioSampler);
                buffer.SetGlobalFloat(SSR_TemporalScale_ID, TemporalScale);
                buffer.SetGlobalFloat(SSR_TemporalWeight_ID, TemporalWeight);
            }
            else
            {
                buffer.SetGlobalInt(SSR_NumResolver_ID, 1);
                buffer.SetGlobalFloat(SSR_TemporalScale_ID, 0);
                buffer.SetGlobalFloat(SSR_TemporalWeight_ID, 0);
            }
        }

        private void SSR_UpdateVariable(SSRCameraData cameraData, Camera RenderCamera, ref PipelineCommandData data)
        {
            Vector2Int CameraSize = new Vector2Int(RenderCamera.pixelWidth, RenderCamera.pixelHeight);
            CommandBuffer buffer = data.buffer;
            if (cameraData.UpdateCameraSize(CameraSize, (int)RayCastingResolution))
            {
                SSR_UpdateUniformVariable(data.buffer);
            }
            ////////////Set Matrix
#if UNITY_EDITOR
            else if (RunTimeDebugMod)
            {
                SSR_UpdateUniformVariable(data.buffer);
            }
#endif
            buffer.SetGlobalVector(SSR_ScreenSize_ID, new Vector2(CameraSize.x, CameraSize.y));
            buffer.SetGlobalVector(SSR_RayCastSize_ID, new Vector2(CameraSize.x, CameraSize.y) / (int)RayCastingResolution);

            buffer.SetGlobalVector(SSR_Jitter_ID, new Vector4((float)CameraSize.x / 1024, (float)CameraSize.y / 1024, RandomSampler.x, RandomSampler.y));
            Matrix4x4 proj = GL.GetGPUProjectionMatrix(RenderCamera.projectionMatrix, false);
            buffer.SetGlobalMatrix(SSR_ProjectionMatrix_ID, proj);
            buffer.SetGlobalMatrix(SSR_InverseProjectionMatrix_ID, proj.inverse);
            buffer.SetGlobalMatrix(SSR_WorldToCameraMatrix_ID, RenderCamera.worldToCameraMatrix);
            buffer.SetGlobalMatrix(SSR_CameraToWorldMatrix_ID, RenderCamera.cameraToWorldMatrix);

            Matrix4x4 warpToScreenSpaceMatrix = Matrix4x4.identity;
            Vector2 HalfCameraSize = new Vector2(CameraSize.x, CameraSize.y) / 2;
            warpToScreenSpaceMatrix.m00 = HalfCameraSize.x; warpToScreenSpaceMatrix.m03 = HalfCameraSize.x;
            warpToScreenSpaceMatrix.m11 = HalfCameraSize.y; warpToScreenSpaceMatrix.m13 = HalfCameraSize.y;

            Matrix4x4 SSR_ProjectToPixelMatrix = warpToScreenSpaceMatrix * proj;
            buffer.SetGlobalMatrix(SSR_ProjectToPixelMatrix_ID, SSR_ProjectToPixelMatrix);

            Vector4 SSR_ProjInfo = new Vector4
                    ((-2 / (CameraSize.x * proj[0])),
                    (-2 / (CameraSize.y * proj[5])),
                    ((1 - proj[2]) / proj[0]),
                    ((1 + proj[6]) / proj[5]));
            buffer.SetGlobalVector(SSR_ProjInfo_ID, SSR_ProjInfo);

            Vector3 SSR_ClipInfo = (float.IsPositiveInfinity(RenderCamera.farClipPlane)) ?
                    new Vector3(RenderCamera.nearClipPlane, -1, 1) :
                    new Vector3(RenderCamera.nearClipPlane * RenderCamera.farClipPlane, RenderCamera.nearClipPlane - RenderCamera.farClipPlane, RenderCamera.farClipPlane);
            buffer.SetGlobalVector(SSR_CameraClipInfo_ID, SSR_ClipInfo);
        }



        private void RenderScreenSpaceReflection(CommandBuffer ScreenSpaceReflectionBuffer, SSRCameraData camData, PipelineCamera cam)
        {
            //////Set HierarchicalDepthRT//////
            ScreenSpaceReflectionBuffer.CopyTexture(cam.targets.renderTargetIdentifier, 0, 0, camData.SSR_SceneColor_RT, 0, 0);
            ScreenSpaceReflectionBuffer.CopyTexture(cam.targets.depthTexture, 0, 0, camData.SSR_HierarchicalDepth_RT, 0, 0);//TODO
            for (int i = 1; i < 5; ++i)
            {
                ScreenSpaceReflectionBuffer.SetGlobalInt(SSR_HiZ_PrevDepthLevel_ID, i - 1);
                ScreenSpaceReflectionBuffer.SetRenderTarget(camData.SSR_HierarchicalDepth_BackUp_RT, i);
                ScreenSpaceReflectionBuffer.DrawMesh(GraphicsUtility.mesh, Matrix4x4.identity, StochasticScreenSpaceReflectionMaterial, 0, RenderPass_HiZ_Depth);
                ScreenSpaceReflectionBuffer.CopyTexture(camData.SSR_HierarchicalDepth_BackUp_RT, 0, i, camData.SSR_HierarchicalDepth_RT, 0, i);
            }
            ScreenSpaceReflectionBuffer.SetGlobalTexture(SSR_HierarchicalDepth_ID, camData.SSR_HierarchicalDepth_RT);

            //////Set SceneColorRT//////
            ScreenSpaceReflectionBuffer.SetGlobalTexture(SSR_SceneColor_ID, camData.SSR_SceneColor_RT);

            //////RayCasting//////
            ScreenSpaceReflectionBuffer.SetGlobalTexture(SSR_Trace_ID, camData.SSR_TraceMask_RT[0]);
            ScreenSpaceReflectionBuffer.SetGlobalTexture(SSR_Mask_ID, camData.SSR_TraceMask_RT[1]);
            if (TraceMethod == TraceApprox.HiZTrace)
            {
                ScreenSpaceReflectionBuffer.BlitMRT(camData.SSR_TraceMask_ID, camData.SSR_TraceMask_RT[0], StochasticScreenSpaceReflectionMaterial, (RayNums > 1) ? RenderPass_HiZ3D_MultiSpp : RenderPass_HiZ3D_SingelSpp);
            }
            else
            {
                ScreenSpaceReflectionBuffer.BlitMRT(camData.SSR_TraceMask_ID, camData.SSR_TraceMask_RT[0], StochasticScreenSpaceReflectionMaterial, (RayNums > 1) ? RenderPass_Linear2D_MultiSPP : RenderPass_Linear2D_SingelSPP);
            }
            //////Spatial filter//////  
            ScreenSpaceReflectionBuffer.SetGlobalTexture(SSR_Spatial_ID, camData.SSR_Spatial_RT);
            ScreenSpaceReflectionBuffer.BlitSRT(camData.SSR_Spatial_RT, StochasticScreenSpaceReflectionMaterial, (RayNums > 1) ? RenderPass_Spatiofilter_MultiSPP : RenderPass_Spatiofilter_SingleSPP);
            //////Temporal filter//////
            ScreenSpaceReflectionBuffer.SetGlobalTexture(SSR_TemporalPrev_ID, camData.SSR_TemporalPrev_RT);
            ScreenSpaceReflectionBuffer.SetGlobalTexture(SSR_TemporalCurr_ID, camData.SSR_TemporalCurr_RT);
            ScreenSpaceReflectionBuffer.BlitSRT(camData.SSR_TemporalCurr_RT, StochasticScreenSpaceReflectionMaterial, (RayNums > 1) ? RenderPass_Temporalfilter_MultiSpp : RenderPass_Temporalfilter_SingleSPP);
            ScreenSpaceReflectionBuffer.CopyTexture(camData.SSR_TemporalCurr_RT, 0, 0, camData.SSR_TemporalPrev_RT, 0, 0);
        }

        public class SSRCameraData : IPerCameraData
        {
            public Vector2 CameraSize { get; private set; }
            public int RayCastingResolution { get; private set; }
            public RenderTexture[] SSR_TraceMask_RT = new RenderTexture[2]; public RenderTargetIdentifier[] SSR_TraceMask_ID = new RenderTargetIdentifier[2];
            public RenderTexture SSR_Spatial_RT, SSR_TemporalPrev_RT, SSR_TemporalCurr_RT, SSR_HierarchicalDepth_RT, SSR_HierarchicalDepth_BackUp_RT, SSR_SceneColor_RT;
            private static void CheckAndRelease(RenderTexture targetRT)
            {
                if (targetRT && targetRT.IsCreated())
                {
                    Object.DestroyImmediate(targetRT);
                }
            }

            public SSRCameraData(Vector2Int currentSize, int targetResolution)
            {
                CameraSize = currentSize;
                RayCastingResolution = targetResolution;
                SSR_HierarchicalDepth_RT = new RenderTexture(currentSize.x, currentSize.y, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
                SSR_HierarchicalDepth_RT.filterMode = FilterMode.Point;
                SSR_HierarchicalDepth_RT.useMipMap = true;
                SSR_HierarchicalDepth_RT.autoGenerateMips = true;

                SSR_HierarchicalDepth_BackUp_RT = new RenderTexture(currentSize.x, currentSize.y, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
                SSR_HierarchicalDepth_BackUp_RT.filterMode = FilterMode.Point;
                SSR_HierarchicalDepth_BackUp_RT.useMipMap = true;
                SSR_HierarchicalDepth_BackUp_RT.autoGenerateMips = false;

                SSR_SceneColor_RT = new RenderTexture(currentSize.x, currentSize.y, 0, RenderTextureFormat.ARGBHalf);

                SSR_TraceMask_RT[0] = new RenderTexture(currentSize.x / (int)RayCastingResolution, currentSize.y / (int)RayCastingResolution, 0, RenderTextureFormat.ARGBHalf);
                SSR_TraceMask_RT[0].filterMode = FilterMode.Point;
                SSR_TraceMask_ID[0] = SSR_TraceMask_RT[0].colorBuffer;

                SSR_TraceMask_RT[1] = new RenderTexture(currentSize.x / (int)RayCastingResolution, currentSize.y / (int)RayCastingResolution, 0, RenderTextureFormat.ARGBHalf);
                SSR_TraceMask_RT[1].filterMode = FilterMode.Point;
                SSR_TraceMask_ID[1] = SSR_TraceMask_RT[1].colorBuffer;

                SSR_Spatial_RT = new RenderTexture(currentSize.x, currentSize.y, 0, RenderTextureFormat.ARGBHalf);
                SSR_Spatial_RT.filterMode = FilterMode.Bilinear;

                SSR_TemporalPrev_RT = new RenderTexture(currentSize.x, currentSize.y, 0, RenderTextureFormat.ARGBHalf);
                SSR_TemporalPrev_RT.filterMode = FilterMode.Bilinear;

                SSR_TemporalCurr_RT = new RenderTexture(currentSize.x, currentSize.y, 0, RenderTextureFormat.ARGBHalf);
                SSR_TemporalCurr_RT.filterMode = FilterMode.Bilinear;

                SSR_HierarchicalDepth_RT.Create();
                SSR_HierarchicalDepth_BackUp_RT.Create();
                SSR_SceneColor_RT.Create();
                SSR_TraceMask_RT[0].Create();
                SSR_TraceMask_RT[1].Create();
                SSR_Spatial_RT.Create();
                SSR_TemporalPrev_RT.Create();
                SSR_TemporalCurr_RT.Create();

            }

            private static void ChangeSet(RenderTexture targetRT, int width, int height, int depth, RenderTextureFormat format)
            {
                targetRT.Release();
                targetRT.width = width;
                targetRT.height = height;
                targetRT.depth = depth;
                targetRT.format = format;
                targetRT.Create();
            }

            public bool UpdateCameraSize(Vector2Int currentSize, int targetResolution)
            {
                if (CameraSize == currentSize && RayCastingResolution == targetResolution) return false;
                CameraSize = currentSize;
                RayCastingResolution = targetResolution;
                ChangeSet(SSR_HierarchicalDepth_RT, currentSize.x, currentSize.y, 0, RenderTextureFormat.RHalf);
                ChangeSet(SSR_HierarchicalDepth_BackUp_RT, currentSize.x, currentSize.y, 0, RenderTextureFormat.RHalf);
                ChangeSet(SSR_SceneColor_RT, currentSize.x, currentSize.y, 0, RenderTextureFormat.DefaultHDR);
                ChangeSet(SSR_TraceMask_RT[0], currentSize.x / (int)RayCastingResolution, currentSize.y / (int)RayCastingResolution, 0, RenderTextureFormat.ARGBHalf);
                ChangeSet(SSR_TraceMask_RT[1], currentSize.x / (int)RayCastingResolution, currentSize.y / (int)RayCastingResolution, 0, RenderTextureFormat.ARGBHalf);
                ChangeSet(SSR_Spatial_RT, currentSize.x, currentSize.y, 0, RenderTextureFormat.ARGBHalf);
                ChangeSet(SSR_TemporalPrev_RT, currentSize.x, currentSize.y, 0, RenderTextureFormat.ARGBHalf);
                ChangeSet(SSR_TemporalCurr_RT, currentSize.x, currentSize.y, 0, RenderTextureFormat.ARGBHalf);
                return true;
            }

            public override void DisposeProperty()
            {
                CheckAndRelease(SSR_HierarchicalDepth_RT);
                CheckAndRelease(SSR_SceneColor_RT);
                CheckAndRelease(SSR_TraceMask_RT[0]);
                CheckAndRelease(SSR_TraceMask_RT[1]);
                CheckAndRelease(SSR_Spatial_RT);
                CheckAndRelease(SSR_TemporalPrev_RT);
                CheckAndRelease(SSR_TemporalCurr_RT);
            }
        }
    }
}