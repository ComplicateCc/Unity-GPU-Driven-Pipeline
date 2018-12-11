 Shader "Maxwell/ProceduralInstance" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Occlusion("Occlusion Scale", Range(0,1)) = 1
		_SpecularIntensity("Specular Intensity", Range(0,1)) = 0.3
		_MetallicIntensity("Metallic Intensity", Range(0, 1)) = 0.1
		_EmissionColor("Emission Color", Color) = (0,0,0,1)
		_EmissionMultiplier("Emission Level", Range(1, 20)) = 1

		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_BumpMap("Normal Map", 2D) = "bump" {}
		_SpecularMap("Specular Map", 2D) = "white"{}
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

		
	// ------------------------------------------------------------
	// Surface shader code generated out of a CGPROGRAM block:
CGINCLUDE
#define UNITY_PASS_DEFERRED
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
		struct Input {
			float2 uv_MainTex;
		};

    float _SpecularIntensity;
	float _MetallicIntensity;
    float4 _EmissionColor;
	float _EmissionMultiplier;
		float _Occlusion;
		float _VertexScale;
		float _VertexOffset;
		sampler2D _BumpMap;
		sampler2D _SpecularMap;
		sampler2D _MainTex;
		


		float _Glossiness;
		float4 _Color;


		void surf (Input IN, inout SurfaceOutputStandardSpecular o) {
			// Albedo comes from a texture tinted by color
			float2 uv = IN.uv_MainTex;// - parallax_mapping(IN.uv_MainTex,IN.viewDir);
			half4 c = tex2D (_MainTex, uv) * _Color;
			o.Albedo = c.rgb;
			o.Alpha = 1;
			o.Occlusion = lerp(1, c.a, _Occlusion);
			float3 spec = tex2D(_SpecularMap,uv);
			o.Specular = lerp(_SpecularIntensity * spec.r, o.Albedo * _SpecularIntensity * spec.r, _MetallicIntensity * spec.g); 
			o.Smoothness = _Glossiness * spec.b;
			o.Normal = UnpackNormal(tex2D(_BumpMap,uv));
			o.Emission = _EmissionColor * _EmissionMultiplier;
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
    outGBuffer2 = half4(s.Normal * 0.5f + 0.5f, 1);
    half4 emission = half4(s.Emission, 1);
    return emission;
}
float4x4 _LastVp;
float4x4 _NonJitterVP;
inline half2 CalculateMotionVector(float4x4 lastvp, half3 worldPos, half2 screenUV)
{
	half4 lastScreenPos = mul(lastvp, half4(worldPos, 1));
	half2 lastScreenUV = GetScreenPos(lastScreenPos);
	return screenUV - lastScreenUV;
}

struct v2f_surf {
  UNITY_POSITION(pos);
  float2 pack0 : TEXCOORD0; 
  float4 worldTangent : TEXCOORD1;
  float4 worldBinormal : TEXCOORD2;
  float4 worldNormal : TEXCOORD3;
  float3 worldViewDir : TEXCOORD4;
};
float4 _MainTex_ST;
v2f_surf vert_surf (uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID) 
{
  	Point v = getVertex(vertexID, instanceID);
  	v2f_surf o;
  	o.pack0 = v.texcoord;
  	o.pos = mul(UNITY_MATRIX_VP, float4(v.vertex, 1));
  	o.worldTangent = float4( v.tangent.xyz, v.vertex.x);
	o.worldNormal =float4(v.normal, v.vertex.z);
  	o.worldBinormal = float4(cross(v.normal, o.worldTangent.xyz) * v.tangent.w, v.vertex.y);
  	o.worldViewDir = UnityWorldSpaceViewDir(v.vertex);
  	return o;
}
float4 unity_Ambient;

// fragment shader
void frag_surf (v2f_surf IN,
    out half4 outGBuffer0 : SV_Target0,
    out half4 outGBuffer1 : SV_Target1,
    out half4 outGBuffer2 : SV_Target2,
    out half4 outEmission : SV_Target3,
	out half2 outMotionVector : SV_Target4,
	out float outDepth : SV_Target5
) {
  // prepare and unpack data
  Input surfIN;
  surfIN.uv_MainTex = IN.pack0.xy;
  float3 worldPos = float3(IN.worldTangent.w, IN.worldBinormal.w, IN.worldNormal.w);
  float3 worldViewDir = normalize(IN.worldViewDir);
  SurfaceOutputStandardSpecular o;
  float3x3 wdMatrix= float3x3(normalize(IN.worldTangent.xyz), normalize(IN.worldBinormal.xyz), normalize(IN.worldNormal.xyz));
  // call surface function
  surf (surfIN, o);
  
  o.Normal = normalize(mul(o.Normal, wdMatrix));
  float3 n = o.Normal;
  outEmission = ProceduralStandardSpecular_Deferred (o, worldViewDir, outGBuffer0, outGBuffer1, outGBuffer2); //GI neccessary here!
  outDepth = IN.pos.z;
  //Calculate Motion Vector
  half4 screenPos = mul(_NonJitterVP, float4(worldPos, 1));
  half2 screenUV = GetScreenPos(screenPos);
  outMotionVector = CalculateMotionVector(_LastVp, worldPos, screenUV);
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
}
CustomEditor "SpecularShaderEditor"
}

