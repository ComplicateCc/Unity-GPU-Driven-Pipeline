using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;
using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;
namespace MPipeline
{
    [CreateAssetMenu(menuName = "GPURP Events/AmbientOcclusion")]
    [RequireEvent(typeof(PropertySetEvent))]
    public class AOEvents : PipelineEvent
    {
        private PropertySetEvent propertySetEvent;


        //C# To Shader Property
        ///Public
        [Header("Render Property")]

        [SerializeField]
        [Range(1, 4)]
        int DirSampler = 2;


        [SerializeField]
        [Range(1, 8)]
        int SliceSampler = 2;


        [SerializeField]
        [Range(1, 5)]
        float Radius = 2.5f;


        [SerializeField]
        [Range(0, 1)]
        float Intensity = 1;


        [SerializeField]
        [Range(1, 8)]
        float Power = 2.5f;


        [SerializeField]
        bool MultiBounce = true;



        [Header("Filtter Property")]

        [Range(0, 1)]
        [SerializeField]
        float Sharpeness = 0.25f;

        [Range(1, 5)]
        [SerializeField]
        float TemporalScale = 1;

        [Range(0, 1)]
        [SerializeField]
        float TemporalResponse = 1;


        //BaseProperty
        private Material GTAOMaterial;

        //Transform property 


        // private
        private float HalfProjScale;
        private float TemporalOffsets;
        private float TemporalDirections;
        private Vector2 CameraSize;
        private Vector4 UVToView;
        private Vector4 oneOverSize_Size;
        private Vector4 Target_TexelSize;
        private PropertySetEvent propertySetEvents;

        private uint m_sampleStep = 0;
        private static readonly float[] m_temporalRotations = { 60, 300, 180, 240, 120, 0 };
        private static readonly float[] m_spatialOffsets = { 0, 0.5f, 0.25f, 0.75f };



        //Shader Property
        ///Public
        private static int _ProjectionMatrix_ID = Shader.PropertyToID("_ProjectionMatrix");
        private static int _LastFrameViewProjectionMatrix_ID = Shader.PropertyToID("_LastFrameViewProjectionMatrix");
        private static int _View_ProjectionMatrix_ID = Shader.PropertyToID("_View_ProjectionMatrix");
        private static int _Inverse_View_ProjectionMatrix_ID = Shader.PropertyToID("_Inverse_View_ProjectionMatrix");
        private static int _WorldToCameraMatrix_ID = Shader.PropertyToID("_WorldToCameraMatrix");
        private static int _CameraToWorldMatrix_ID = Shader.PropertyToID("_CameraToWorldMatrix");


        private static int _AO_DirSampler_ID = Shader.PropertyToID("_AO_DirSampler");
        private static int _AO_SliceSampler_ID = Shader.PropertyToID("_AO_SliceSampler");
        private static int _AO_Power_ID = Shader.PropertyToID("_AO_Power");
        private static int _AO_Intensity_ID = Shader.PropertyToID("_AO_Intensity");
        private static int _AO_Radius_ID = Shader.PropertyToID("_AO_Radius");
        private static int _AO_Sharpeness_ID = Shader.PropertyToID("_AO_Sharpeness");
        private static int _AO_TemporalScale_ID = Shader.PropertyToID("_AO_TemporalScale");
        private static int _AO_TemporalResponse_ID = Shader.PropertyToID("_AO_TemporalResponse");
        private static int _AO_MultiBounce_ID = Shader.PropertyToID("_AO_MultiBounce");


        ///Private
        private static int _AO_HalfProjScale_ID = Shader.PropertyToID("_AO_HalfProjScale");
        private static int _AO_TemporalOffsets_ID = Shader.PropertyToID("_AO_TemporalOffsets");
        private static int _AO_TemporalDirections_ID = Shader.PropertyToID("_AO_TemporalDirections");
        private static int _AO_UVToView_ID = Shader.PropertyToID("_AO_UVToView");
        private static int _AO_RT_TexelSize_ID = Shader.PropertyToID("_AO_RT_TexelSize");

        private static int _BentNormal_Texture_ID = Shader.PropertyToID("_BentNormal_Texture");
        private static int _GTAO_Texture_ID = Shader.PropertyToID("_GTAO_Texture");
        private static int _GTAO_Spatial_Texture_ID = Shader.PropertyToID("_GTAO_Spatial_Texture");
        private static int _PrevRT_ID = Shader.PropertyToID("_PrevRT");
        private static int _CurrRT_ID = Shader.PropertyToID("_CurrRT");
        private static int _Combien_AO_RT_ID = Shader.PropertyToID("_Combien_AO_RT");

        /* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* *//* */

        protected override void Init(PipelineResources resources)
        {
            GTAOMaterial = new Material(resources.shaders.gtaoShader);
            propertySetEvent = RenderPipeline.GetEvent<PropertySetEvent>();
        }

        public override bool CheckProperty()
        {
            return GTAOMaterial != null;
        }

        protected override void OnEnable()
        {
            RenderPipeline.ExecuteBufferAtFrameEnding((cb) => cb.EnableShaderKeyword("EnableGTAO"));
        }

        protected override void OnDisable()
        {
            RenderPipeline.ExecuteBufferAtFrameEnding((cb) => cb.DisableShaderKeyword("EnableGTAO"));
        }

        public override void FrameUpdate(PipelineCamera cam, ref PipelineCommandData data)
        {
            AOHistoryData historyData = IPerCameraData.GetProperty(cam, (c) => new AOHistoryData(c.cam.pixelWidth, c.cam.pixelHeight));
            UpdateVariable_SSAO(historyData, cam, ref data);
            RenderSSAO(historyData, cam, ref data);
        }

        protected override void Dispose()
        {
            if (Application.isPlaying)
            {
                Destroy(GTAOMaterial);
            }
            else
            {
                DestroyImmediate(GTAOMaterial);
            }

        }


        ////////////////////////SSAO Function////////////////////////
        private void UpdateVariable_SSAO(AOHistoryData historyData, PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            int2 renderResolution = int2(cam.cam.pixelWidth, cam.cam.pixelHeight);
            buffer.SetGlobalMatrix(_WorldToCameraMatrix_ID, cam.cam.worldToCameraMatrix);
            buffer.SetGlobalMatrix(_CameraToWorldMatrix_ID, cam.cam.cameraToWorldMatrix);
            buffer.SetGlobalMatrix(_ProjectionMatrix_ID, GL.GetGPUProjectionMatrix(cam.cam.projectionMatrix, false)); ;
            buffer.SetGlobalMatrix(_View_ProjectionMatrix_ID, data.vp);
            buffer.SetGlobalMatrix(_Inverse_View_ProjectionMatrix_ID, data.inverseVP);
            buffer.SetGlobalMatrix(_LastFrameViewProjectionMatrix_ID, propertySetEvent.lastViewProjection);

            //----------------------------------------------------------------------------------
            buffer.SetGlobalFloat(_AO_DirSampler_ID, DirSampler);
            buffer.SetGlobalFloat(_AO_SliceSampler_ID, SliceSampler);
            buffer.SetGlobalFloat(_AO_Intensity_ID, Intensity);
            buffer.SetGlobalFloat(_AO_Radius_ID, Radius);
            buffer.SetGlobalFloat(_AO_Power_ID, Power);
            buffer.SetGlobalFloat(_AO_Sharpeness_ID, Sharpeness);
            buffer.SetGlobalFloat(_AO_TemporalScale_ID, TemporalScale);
            buffer.SetGlobalFloat(_AO_TemporalResponse_ID, TemporalResponse);
            buffer.SetGlobalInt(_AO_MultiBounce_ID, MultiBounce ? 1 : 0);


            //----------------------------------------------------------------------------------
            float fovRad = cam.cam.fieldOfView * Mathf.Deg2Rad;
            float invHalfTanFov = 1 / Mathf.Tan(fovRad * 0.5f);
            Vector2 focalLen = new Vector2(invHalfTanFov * ((float)renderResolution.y / (float)renderResolution.x), invHalfTanFov);
            Vector2 invFocalLen = new Vector2(1 / focalLen.x, 1 / focalLen.y);
            buffer.SetGlobalVector(_AO_UVToView_ID, new Vector4(2 * invFocalLen.x, 2 * invFocalLen.y, -1 * invFocalLen.x, -1 * invFocalLen.y));

            //----------------------------------------------------------------------------------
            float projScale;
            projScale = renderResolution.y / (Mathf.Tan(fovRad * 0.5f) * 2) * 0.5f;
            buffer.SetGlobalFloat(_AO_HalfProjScale_ID, projScale);

            //----------------------------------------------------------------------------------
            oneOverSize_Size = new Vector4(1 / (float)renderResolution.x, 1 / (float)renderResolution.y, (float)renderResolution.x, (float)renderResolution.y);
            buffer.SetGlobalVector(_AO_RT_TexelSize_ID, oneOverSize_Size);

            //----------------------------------------------------------------------------------
            float temporalRotation = m_temporalRotations[m_sampleStep % 6];
            float temporalOffset = m_spatialOffsets[(m_sampleStep / 6) % 4];
            buffer.SetGlobalFloat(_AO_TemporalDirections_ID, temporalRotation / 360);
            buffer.SetGlobalFloat(_AO_TemporalOffsets_ID, temporalOffset);
            m_sampleStep++;

            //----------------------------------------------------------------------------------
            //TODO
            //Resize
            historyData.UpdateSize(renderResolution.x, renderResolution.y);
        }

        private void RenderSSAO(AOHistoryData historyData, PipelineCamera cam, ref PipelineCommandData data)
        {
            CommandBuffer buffer = data.buffer;
            int2 renderResolution = int2(cam.cam.pixelWidth, cam.cam.pixelHeight);

            //Resolve GTAO 
            buffer.SetGlobalTexture(_GTAO_Texture_ID, historyData.AO_BentNormal_RT[0]);
            buffer.SetGlobalTexture(_BentNormal_Texture_ID, historyData.AO_BentNormal_RT[1]);
            buffer.BlitMRT(historyData.AO_BentNormal_ID, cam.targets.renderTargetIdentifier, GTAOMaterial, 0);

            //Spatial filter
            //------//XBlur
            buffer.GetTemporaryRT(_GTAO_Spatial_Texture_ID, renderResolution.x, renderResolution.y, 0, FilterMode.Point, RenderTextureFormat.RGHalf);
            buffer.BlitSRTWithDepth(_GTAO_Spatial_Texture_ID, cam.targets.depthBuffer, GTAOMaterial, 1);
            //------//YBlur
            buffer.CopyTexture(_GTAO_Spatial_Texture_ID, historyData.AO_BentNormal_RT[0]);
            buffer.BlitSRTWithDepth(_GTAO_Spatial_Texture_ID, cam.targets.depthBuffer, GTAOMaterial, 2);

            //Temporal filter
            buffer.SetGlobalTexture(_PrevRT_ID, historyData.prev_Texture);
            buffer.GetTemporaryRT(_CurrRT_ID, renderResolution.x, renderResolution.y, 0, FilterMode.Point, RenderTextureFormat.RGHalf);
            buffer.BlitSRTWithDepth(_CurrRT_ID, cam.targets.depthBuffer, GTAOMaterial, 3);
            buffer.CopyTexture(_CurrRT_ID, historyData.prev_Texture);

            buffer.ReleaseTemporaryRT(_GTAO_Spatial_Texture_ID);
            buffer.ReleaseTemporaryRT(_CurrRT_ID);
            buffer.ReleaseTemporaryRT(_Combien_AO_RT_ID);
            buffer.SetGlobalTexture(ShaderIDs._AOROTexture, historyData.prev_Texture);
        }
    }

    public class AOHistoryData : IPerCameraData
    {
        public RenderTexture prev_Texture { get; private set; }
        public RenderTexture[] AO_BentNormal_RT { get; private set; }
        public RenderTargetIdentifier[] AO_BentNormal_ID { get; private set; }
        public AOHistoryData(int width, int height)
        {
            AO_BentNormal_RT = new RenderTexture[2];
            AO_BentNormal_ID = new RenderTargetIdentifier[2];
            prev_Texture = new RenderTexture(width, height, 0, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear);
            prev_Texture.filterMode = FilterMode.Bilinear;
            prev_Texture.Create();
            AO_BentNormal_RT[0] = new RenderTexture(width, height, 0, RenderTextureFormat.RGHalf);
            AO_BentNormal_RT[1] = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf);
            AO_BentNormal_ID[0] = AO_BentNormal_RT[0].colorBuffer;
            AO_BentNormal_ID[1] = AO_BentNormal_RT[1].colorBuffer;
            AO_BentNormal_RT[0].Create();
            AO_BentNormal_RT[1].Create();
        }

        public void UpdateSize(int width, int height)
        {
            if (width != prev_Texture.width || height != prev_Texture.height)
            {
                prev_Texture.Release();
                prev_Texture.width = width;
                prev_Texture.height = height;
                prev_Texture.Create();
                AO_BentNormal_RT[0].Release();
                AO_BentNormal_RT[1].Release();
                AO_BentNormal_RT[0].width = width;
                AO_BentNormal_RT[1].width = width;
                AO_BentNormal_RT[0].height = height;
                AO_BentNormal_RT[1].height = height;
                AO_BentNormal_RT[0].Create();
                AO_BentNormal_RT[1].Create();
            }
        }

        public override void DisposeProperty()
        {
            if (Application.isPlaying)
            {
                Object.Destroy(prev_Texture);
            }
            else
            {
                Object.DestroyImmediate(prev_Texture);
            }

            if (AO_BentNormal_RT[0] != null)
            {
                AO_BentNormal_RT[0].Release();
                AO_BentNormal_RT[0] = null;
            }

            if (AO_BentNormal_RT[1] != null)
            {
                AO_BentNormal_RT[1].Release();
                AO_BentNormal_RT[1] = null;
            }
        }
    }
}