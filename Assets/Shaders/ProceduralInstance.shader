// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

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
		sampler2D _MainTex; float4 _MainTex_ST;

		float _Glossiness;
		float4 _Color;


		void surf (Input IN, inout SurfaceOutputStandardSpecular o) {
			// Albedo comes from a texture tinted by color
			float2 uv = IN.uv_MainTex;// - parallax_mapping(IN.uv_MainTex,IN.viewDir);
			uv = TRANSFORM_TEX(uv, _MainTex);
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
float3 _SceneOffset;
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
struct appdata
{
	float4 vertex : POSITION;
	float4 tangent : TANGENT;
	float3 normal : NORMAL;
	float2 texcoord : TEXCOORD0;
};
v2f_surf vert_deferred (appdata v) 
{
  	v2f_surf o;
  	o.pack0 = v.texcoord;
  	o.pos = UnityObjectToClipPos(v.vertex);
	  float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
	  worldPos /= worldPos.w;
  	o.worldTangent = float4( v.tangent.xyz, worldPos.x);
	  v.normal = mul((float3x3)unity_ObjectToWorld, v.normal);
	  v.tangent.xyz = mul((float3x3)unity_ObjectToWorld, v.tangent.xyz);
	o.worldNormal =float4(v.normal, worldPos.z);
  	o.worldBinormal = float4(cross(v.normal, o.worldTangent.xyz) * v.tangent.w, worldPos.y);
  	o.worldViewDir = UnityWorldSpaceViewDir(worldPos);
  	return o;
}
float4 unity_Ambient;

// fragment shader
void frag_surf (v2f_surf IN,
    out half4 outGBuffer0 : SV_Target0,
    out half4 outGBuffer1 : SV_Target1,
    out half4 outGBuffer2 : SV_Target2,
    out half4 outEmission : SV_Target3,
	out half2 outMotionVector : SV_Target4
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
  half4 screenPos = mul(_NonJitterVP, float4(worldPos, 1));
  half2 screenUV = GetScreenPos(screenPos);
  outMotionVector = CalculateMotionVector(_LastVp, worldPos - _SceneOffset, screenUV);
}

ENDCG

pass
{
	stencil{
  Ref 1
  comp always
  pass replace
}
Tags {"LightMode" = "GBuffer"}
ZTest Less
CGPROGRAM

#pragma vertex vert_deferred
#pragma fragment frag_surf
ENDCG
}

		Pass
		{
			ZTest less
			Cull back
			Tags {"LightMode" = "DirectionalLight"}
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
			#pragma exclude_renderers gles
			#include "UnityCG.cginc"
			#include "CGINC/Procedural.cginc"
			float4x4 _ShadowMapVP;
			float4 _NormalBiases;
			struct appdata_shadow
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
			};
			struct v2f
			{
				float4 vertex : SV_POSITION;
			};

			v2f vert (appdata_shadow v)
			{
				float4 worldPos = float4(mul(unity_ObjectToWorld, v.vertex) - _NormalBiases.x * mul((float3x3)unity_ObjectToWorld, v.normal), 1);
				v2f o;
				o.vertex = mul(_ShadowMapVP, worldPos);
				return o;
			}

			
			float frag (v2f i) : SV_Target
			{
				#if UNITY_REVERSED_Z
				return 1 - i.vertex.z;
				#else
				return i.vertex.z;
				#endif
			}

			ENDCG
		}

		Pass
        {
			Tags {"LightMode"="PointLightPass"}
			ZTest less
			Cull back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #include "CGINC/Procedural.cginc"
			struct appdata_shadow
			{
				float4 vertex : POSITION;
			};
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };
            float4x4 _VP;
            float4 _LightPos;
            v2f vert (appdata_shadow v) 
            {
                v2f o;
				float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos = worldPos.xyz;
                o.vertex = mul(_VP, worldPos);
                return o;
            }

            half frag (v2f i) : SV_Target
            {
               return distance(i.worldPos, _LightPos.xyz) / _LightPos.w;
            } 
            ENDCG
        }

		Pass
		{
			Tags {"LightMode"="SpotLightPass"}
			ZTest less
			Cull back
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
			#pragma exclude_renderers gles
			#include "UnityCG.cginc"
			#include "CGINC/Procedural.cginc"
			float4x4 _ShadowMapVP;
			float3 _LightPos;
			float _LightRadius;
			struct v2f
			{
				float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
			};
			struct appdata_shadow
			{
				float4 vertex : POSITION;
			};

			v2f vert (appdata_shadow v)
			{
				float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
				v2f o;
				o.vertex = mul(_ShadowMapVP, worldPos);
				o.worldPos = worldPos.xyz;
				return o;
			}
			float frag (v2f i) : SV_Target
			{
				return distance(_LightPos, i.worldPos) / _LightRadius;
			}

			ENDCG
		}
}
CustomEditor "SpecularShaderEditor"
}

