 Shader "Maxwell/CombinedProcedural" {
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

		
	// ------------------------------------------------------------
	// Surface shader code generated out of a CGPROGRAM block:
CGINCLUDE
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
#pragma target 5.0
#include "HLSLSupport.cginc"
#include "UnityShaderVariables.cginc"
#include "UnityShaderUtilities.cginc"
#include "UnityCG.cginc"
#include "Lighting.cginc"
#include "UnityMetaPass.cginc"
#include "AutoLight.cginc"
#include "UnityPBSLighting.cginc"
#include "CGINC/Procedural.cginc"
	Texture2DArray<half4> _MainTex; SamplerState sampler_MainTex;
  Texture2DArray<half3> _LightMap; SamplerState sampler_LightMap;
	StructuredBuffer<PropertyValue> _PropertiesBuffer;


	
	void surf (float2 uv, float2 lightmapUV, int lightmapIndex, uint index, inout SurfaceOutputStandardSpecular o) {
		PropertyValue prop = _PropertiesBuffer[index];
    float2 detailUV = uv * prop.detailScaleOffset.xy + prop.detailScaleOffset.zw;
    float4 detailAlbedo = prop.detailTextureIndex.x >= 0 ? _MainTex.Sample(sampler_MainTex, float3(detailUV, prop.detailTextureIndex.x)) : 1;
    float3 detailNormal = prop.detailTextureIndex.y >= 0 ? UnpackNormal(_MainTex.Sample(sampler_MainTex, float3(detailUV, prop.detailTextureIndex.y))) : float3(0,0,1);
    uv *= prop.mainScaleOffset.xy;
    uv += prop.mainScaleOffset.zw;
    half4 spec = prop.textureIndex.z >= 0 ? _MainTex.Sample(sampler_MainTex, float3(uv, prop.textureIndex.z)) : 1;
		half4 c = (prop.textureIndex.x >= 0 ? _MainTex.Sample(sampler_MainTex, float3(uv, prop.textureIndex.x)) : 1);
		o.Albedo = c.rgb;
    o.Albedo = lerp(detailAlbedo.rgb, o.Albedo, spec.b) * prop._Color;
		o.Alpha = 1;
		o.Specular = lerp(prop._SpecularIntensity * spec.r, o.Albedo * prop._SpecularIntensity * spec.r, prop._MetallicIntensity); 
		o.Smoothness = prop._Glossiness * spec.g;
    o.Occlusion = lerp(min(detailAlbedo.a, c.a), c.a, spec.b);
    o.Occlusion = lerp(1, o.Occlusion, prop._Occlusion);
		if(prop.textureIndex.y >= 0){
			o.Normal =  UnpackNormal(_MainTex.Sample(sampler_MainTex, float3(uv, prop.textureIndex.y)));
		}else{
			o.Normal =  float3(0,0,1);
		}
		o.Emission = prop._EmissionColor;
    if(lightmapIndex >= 0)
    {
      o.Emission.rgb += _LightMap.Sample(sampler_LightMap, float3(lightmapUV, lightmapIndex)) * c.rgb;
    }    
    o.Normal = lerp(detailNormal, o.Normal, spec.b);
    
	}


#define GetScreenPos(pos) ((float2(pos.x, pos.y) * 0.5) / pos.w + 0.5)


half4 ProceduralStandardSpecular_Deferred (SurfaceOutputStandardSpecular s, float3 viewDir, out half4 outGBuffer0, out half4 outGBuffer1, out half4 outGBuffer2)
{
    // energy conservation
    float oneMinusReflectivity;
    s.Albedo = EnergyConservationBetweenDiffuseAndSpecular (s.Albedo, s.Specular, /*out*/ oneMinusReflectivity);
    // RT0: diffuse color (rgb), occlusion (a) - sRGB rendertarget
    outGBuffer0 = half4(s.Albedo, s.Occlusion);
    // RT1: spec color (rgb), smoothness (a) - sRGB rendertarget
    outGBuffer1 = half4(s.Specular, s.Smoothness);
    // RT2: normal (rgb), --unused, very low precision-- (a)
    outGBuffer2 = half4(s.Normal * 0.5f + 0.5f, 0);
    half4 emission = half4(s.Emission, 1);
    return emission;
}

float4x4 _LastVp;
float4x4 _NonJitterVP;
inline half2 CalculateMotionVector(float4x4 lastvp, float3 worldPos, half2 screenUV)
{
	half4 lastScreenPos = mul(lastvp, half4(worldPos, 1));
	half2 lastScreenUV = GetScreenPos(lastScreenPos);
	return screenUV - lastScreenUV;
}

struct v2f_surf {
  UNITY_POSITION(pos);
  float4 pack0 : TEXCOORD0; 
  float4 worldTangent : TEXCOORD1;
  float4 worldBinormal : TEXCOORD2;
  float4 worldNormal : TEXCOORD3;
  float3 worldViewDir : TEXCOORD4;
  nointerpolation uint objectIndex : TEXCOORD5;
  nointerpolation int lightmapIndex : TEXCOORD6;
};
float4 _MainTex_ST;
v2f_surf vert_surf (uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID) 
{
  	Point v = getVertex(vertexID, instanceID);
  	v2f_surf o;
  	o.pack0.xy = v.texcoord;
    o.pack0.zw = v.lightmapUV;
	o.objectIndex = v.objIndex;
  o.lightmapIndex = v.lightmapIndex;
  	o.pos = mul(UNITY_MATRIX_VP, float4(v.vertex, 1));
  	o.worldTangent = float4( v.tangent.xyz, v.vertex.x);
	o.worldNormal =float4(v.normal, v.vertex.z);
  	float tangentSign = v.tangent.w;
  	o.worldBinormal = float4(cross(v.normal, o.worldTangent.xyz) * tangentSign, v.vertex.y);
  	o.worldViewDir = UnityWorldSpaceViewDir(v.vertex);
    
  	return o;
}
float4x4 _VP;
v2f_surf vert_gbuffer (uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID) 
{
  	Point v = getVertex(vertexID, instanceID);
  	v2f_surf o;
  	o.pack0.xy = v.texcoord;
    o.pack0.zw = v.lightmapUV;
	o.objectIndex = v.objIndex;
  o.lightmapIndex = v.lightmapIndex;
  	o.pos = mul(_VP, float4(v.vertex, 1));
  	o.worldTangent = float4( v.tangent.xyz, v.vertex.x);
	o.worldNormal =float4(v.normal, v.vertex.z);
  	float tangentSign = v.tangent.w;
  	o.worldBinormal = float4(cross(v.normal, o.worldTangent.xyz) * tangentSign, v.vertex.y);
  	o.worldViewDir = UnityWorldSpaceViewDir(v.vertex);
  	return o;
}
float3 _SceneOffset;

// fragment shader
void frag_surf (v2f_surf IN,
    out half4 outGBuffer0 : SV_Target0,
    out half4 outGBuffer1 : SV_Target1,
    out half4 outGBuffer2 : SV_Target2,
    out half4 outEmission : SV_Target3,
	out half2 outMotionVector : SV_Target4
) {
  // prepare and unpack data
  float3 worldPos = float3(IN.worldTangent.w, IN.worldBinormal.w, IN.worldNormal.w);
  float3 worldViewDir = normalize(IN.worldViewDir);
  SurfaceOutputStandardSpecular o;
  half3x3 wdMatrix= half3x3(normalize(IN.worldTangent.xyz), normalize(IN.worldBinormal.xyz), normalize(IN.worldNormal.xyz));
  // call surface function
  surf (IN.pack0.xy, IN.pack0.zw, IN.lightmapIndex, IN.objectIndex, o);
  o.Normal = normalize(mul(o.Normal, wdMatrix));
  outEmission = ProceduralStandardSpecular_Deferred (o, worldViewDir, outGBuffer0, outGBuffer1, outGBuffer2); //GI neccessary here!
  //Calculate Motion Vector
  half4 screenPos = mul(_NonJitterVP, float4(worldPos, 1));
  half2 screenUV = GetScreenPos(screenPos);
  outMotionVector = CalculateMotionVector(_LastVp, worldPos - _SceneOffset, screenUV);
}


float3 frag_gi (v2f_surf IN) : SV_TARGET{
  // prepare and unpack data
  float3 worldPos = float3(IN.worldTangent.w, IN.worldBinormal.w, IN.worldNormal.w);
  float3 worldViewDir = normalize(IN.worldViewDir);
  SurfaceOutputStandardSpecular o;
  half3x3 wdMatrix= half3x3(normalize(IN.worldTangent.xyz), normalize(IN.worldBinormal.xyz), normalize(IN.worldNormal.xyz));
  // call surface function
  surf (IN.pack0.xy, IN.pack0.zw, IN.lightmapIndex, IN.objectIndex, o);
  return o.Emission;
  //TODO
}

ENDCG

//Pass 0 deferred
Pass {
stencil{
  Ref 1
  comp always
  pass replace
}
ZTest Less
CGPROGRAM

#pragma vertex vert_surf
#pragma fragment frag_surf
#pragma exclude_renderers nomrt
ENDCG
}

Pass {
ZTest Less
Cull off
CGPROGRAM

#pragma vertex vert_gbuffer
#pragma fragment frag_gi
#pragma exclude_renderers nomrt
ENDCG
}
}
CustomEditor "SpecularShaderEditor"
}

