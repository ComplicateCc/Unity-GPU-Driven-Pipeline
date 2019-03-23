
 Shader "Maxwell/Dynamic Object" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Occlusion("Occlusion Scale", Range(0,1)) = 1
		_SpecularIntensity("Specular Intensity", Range(0,1)) = 0.3
		_MetallicIntensity("Metallic Intensity", Range(0, 1)) = 0.1
		_EmissionColor("Emission Color", Color) = (0,0,0,1)
		_EmissionMap("Emission Map", 2D) = "white"{}
		_MainTex ("Albedo (RGB)AO(A)", 2D) = "white" {}
		_BumpMap("Normal Map", 2D) = "bump" {}
		_SpecularMap("R(Spec)G(Smooth)B(DetailMask)", 2D) = "white"{}
		_DetailAlbedo("Detail Albedo", 2D) = "white"{}
		_DetailNormal("Detail Normal", 2D) = "bump"{}
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

	// ------------------------------------------------------------
	// Surface shader code generated out of a CGPROGRAM block:
CGINCLUDE
#pragma shader_feature DETAIL_ON
#pragma target 5.0
#pragma multi_compile __ ENABLE_SUN
			#pragma multi_compile __ ENABLE_SUNSHADOW
			#pragma multi_compile __ POINTLIGHT
			#pragma multi_compile __ SPOTLIGHT
//#define LIGHTMAP
#define MOTION_VECTOR
#include "UnityCG.cginc"
#include "UnityDeferredLibrary.cginc"
#include "UnityPBSLighting.cginc"
#include "CGINC/VoxelLight.cginc"
#include "CGINC/Shader_Include/Common.hlsl"
#include "CGINC/Shader_Include/BSDF_Library.hlsl"
#include "CGINC/Shader_Include/AreaLight.hlsl"
#include "CGINC/Sunlight.cginc"
#include "CGINC/Lighting.cginc"
#include "CGINC/MPipeDeferred.cginc"
ENDCG

pass
{
	stencil{
  Ref 1
  comp always
  pass replace
}
Name "GBuffer"
Tags {"LightMode" = "GBuffer" "Name" = "GBuffer"}
ZTest Less
CGPROGRAM

#pragma vertex vert_surf
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
				float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
				v2f o;
				o.vertex = mul(_ShadowMapVP, worldPos);
				return o;
			}

			
			float frag (v2f i)  : SV_TARGET
			{
				return i.vertex.z;
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
            v2f vert (appdata_shadow v) 
            {
                v2f o;
				float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos = worldPos.xyz;
                o.vertex = mul(_VP, worldPos);
				
                return o;
            }

            float frag (v2f i) : SV_Target
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
			float frag (v2f i) : SV_TARGET
			{
				return i.vertex.z;
			}

			ENDCG
		}
	
}
	CustomEditor "ShouShouEditor"
}

