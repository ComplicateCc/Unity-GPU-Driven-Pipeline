using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static partial class ShaderIDs
{
    public static readonly int _Count = Shader.PropertyToID("_Count");
    public static readonly int planes = Shader.PropertyToID("planes");
    public static readonly int _ShadowCamDirection = Shader.PropertyToID("_ShadowCamDirection");
    public static readonly int _DirShadowMap = Shader.PropertyToID("_DirShadowMap");
    public static readonly int _CubeShadowMap = Shader.PropertyToID("_CubeShadowMap");
    public static readonly int _InvVP = Shader.PropertyToID("_InvVP");
    public static readonly int _LastVp = Shader.PropertyToID("_LastVp");
    public static readonly int _ShadowMapVP = Shader.PropertyToID("_ShadowMapVP");
    public static readonly int _ShadowMapVPs = Shader.PropertyToID("_ShadowMapVPs");
    public static readonly int _ShadowCamPoses = Shader.PropertyToID("_ShadowCamPoses");
    public static readonly int _ShadowDisableDistance = Shader.PropertyToID("_ShadowDisableDistance");
    public static readonly int _LightFinalColor = Shader.PropertyToID("_LightFinalColor");
    public static readonly int _LightPos = Shader.PropertyToID("_LightPos");
    public static readonly int _MainTex = Shader.PropertyToID("_MainTex");
    public static readonly int _SoftParam = Shader.PropertyToID("_SoftParam");
    public static readonly int _OffsetIndex = Shader.PropertyToID("_OffsetIndex");
    public static readonly int clusterBuffer = Shader.PropertyToID("clusterBuffer");
    public static readonly int _RemoveVar = Shader.PropertyToID("_RemoveVar");
    public static readonly int instanceCountBuffer = Shader.PropertyToID("instanceCountBuffer");
    public static readonly int resultBuffer = Shader.PropertyToID("resultBuffer");
    public static readonly int verticesBuffer = Shader.PropertyToID("verticesBuffer");
    public static readonly int boneBuffer = Shader.PropertyToID("boneBuffer");
    public static readonly int _NormalBiases = Shader.PropertyToID("_NormalBiases");
    public static readonly int dispatchBuffer = Shader.PropertyToID("dispatchBuffer");
    public static readonly int reCheckResult = Shader.PropertyToID("reCheckResult");
    public static readonly int reCheckCount = Shader.PropertyToID("reCheckCount");

    public static readonly int _FarClipCorner = Shader.PropertyToID("_FarClipCorner");
    public static readonly int _Jitter = Shader.PropertyToID("_Jitter");
    public static readonly int _Sharpness = Shader.PropertyToID("_Sharpness");
    public static readonly int _FinalBlendParameters = Shader.PropertyToID("_FinalBlendParameters");
    public static readonly int _HistoryTex = Shader.PropertyToID("_HistoryTex");
    public static readonly int _CullingPlaneCount = Shader.PropertyToID("_CullingPlaneCount");

    public static readonly int _ShadowMapResolution = Shader.PropertyToID("_ShadowMapResolution");
    public static readonly int _ScreenSize = Shader.PropertyToID("_ScreenSize");
    public static readonly int _VolumetricTex = Shader.PropertyToID("_VolumetricTex");
    public static readonly int _LightDir = Shader.PropertyToID("_LightDir");
    public static readonly int allCubeBuffer = Shader.PropertyToID("allCubeBuffer");
    public static readonly int _WorldPoses = Shader.PropertyToID("_WorldPoses");
    public static readonly int _PreviousLevel = Shader.PropertyToID("_PreviousLevel");
    public static readonly int _HizDepthTex = Shader.PropertyToID("_HizDepthTex");
    public static readonly int _CameraUpVector = Shader.PropertyToID("_CameraUpVector");
    public static readonly int _VP = Shader.PropertyToID("_VP");
    public static readonly int _InvLastVP = Shader.PropertyToID("_InvLastVP");
    public static readonly int _Depth = Shader.PropertyToID("_Depth");
    public static readonly int _LastDepth = Shader.PropertyToID("_LastDepth");
    public static readonly int _NonJitterVP = Shader.PropertyToID("_NonJitterVP");

    public static readonly int _Lut3D = Shader.PropertyToID("_Lut3D");
    public static readonly int _Lut3D_Params = Shader.PropertyToID("_Lut3D_Params");
    public static readonly int _PostExposure = Shader.PropertyToID("_PostExposure");
    public static readonly int _TemporalClipBounding = Shader.PropertyToID("_TemporalClipBounding");

    public static readonly int _LightIntensity = Shader.PropertyToID("_LightIntensity");
    public static readonly int _LightColor = Shader.PropertyToID("_LightColor");
    public static readonly int _LightOffset = Shader.PropertyToID("_LightOffset");
    public static readonly int lightPositionBuffer = Shader.PropertyToID("lightPositionBuffer");
    public static readonly int _LightRadius = Shader.PropertyToID("_LightRadius");

    public static readonly int _ModelBones = Shader.PropertyToID("_ModelBones");
    public static readonly int _TimeVar = Shader.PropertyToID("_TimeVar");
    public static readonly int _AnimTex = Shader.PropertyToID("_AnimTex");
    public static readonly int objBuffer = Shader.PropertyToID("objBuffer");
    public static readonly int bonesBuffer = Shader.PropertyToID("bonesBuffer");
    public static readonly int bindBuffer = Shader.PropertyToID("bindBuffer");
}
