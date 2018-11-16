 Shader "Maxwell/Grass" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Occlusion("Occlusion Scale", Range(0,1)) = 1
		_SpecularIntensity("Specular Intensity", Range(0,1)) = 0.3
		_MetallicIntensity("Metallic Intensity", Range(0, 1)) = 0.1

		_MainTex ("Albedo (RGB)", 2D) = "white" {}
	}
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
    float _SpecularIntensity;
	float _MetallicIntensity;
		float _Occlusion;
		float _VertexScale;
		float _VertexOffset;
		sampler2D _MainTex;
		float _Glossiness;
		float4 _Color;
		float _Height;
		float _Width;
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
			struct GrassPoint
			{
				float3x4 localToWorld;
				uint2 replCoord;
			};
			struct v2g
			{
				nointerpolation uint instanceID : TEXCOORD0;
			};

			struct g2f
			{
				float4 pos : SV_POSITION;
				float4 worldPos : TEXCOORD1;
				float4 norm : TEXCOORD0;
			};
StructuredBuffer<GrassPoint> _GrassPointsBuffer;
//RGB: position A: uv.x
Texture2D<float4> _GrassPosTexture;
//RGB: normal A: uv.y
Texture2D<float4> _GrassNormalTexture;
v2g vert_surf (uint vertexID : SV_VertexID) 
{
  	v2g o;
	o.instanceID = vertexID;
  	return o;
}
#define GRASSMAXCOUNT 36
		[maxvertexcount(GRASSMAXCOUNT)]
		void geom(point v2g points[1], inout TriangleStream<g2f> triStream)
		{
			uint id = points[0].instanceID;
			GrassPoint pt = _GrassPointsBuffer[id];
			//TODO
			//Add LOD Here
			[loop]
			for(uint ite = 0; ite < GRASSMAXCOUNT; ++ite)
			{
				g2f o;
				float4 posTexValue = _GrassPosTexture[pt.replCoord];
				float4 normTexValue = _GrassNormalTexture[pt.replCoord];
				pt.replCoord.y++;
				float4 localPos = float4(posTexValue.xyz, 1);
				o.worldPos = float4(mul(pt.localToWorld, localPos), posTexValue.w);
				o.pos = mul(UNITY_MATRIX_VP, float4(o.worldPos.xyz, 1));
				o.norm = float4(mul((float3x3)pt.localToWorld, normalize(normTexValue.xyz)), normTexValue.w);
				triStream.Append(o);
			}
		}
// fragment shader
void frag_surf (g2f IN,
    out half4 outGBuffer0 : SV_Target0,
    out half4 outGBuffer1 : SV_Target1,
    out half4 outGBuffer2 : SV_Target2,
    out half4 outEmission : SV_Target3,
	out float outDepth : SV_Target5
) {
  // prepare and unpack data
  float2 uv = float2(IN.worldPos.w, IN.norm.w);
  float3 worldPos = IN.worldPos,xyz;
  float3 worldViewDir = normalize(worldPos - _WorldSpaceCameraPos);
  SurfaceOutputStandardSpecular o;
 	half4 c = tex2D (_MainTex, uv) * _Color;
	o.Albedo = c.rgb;
	o.Alpha = c.a;
	o.Occlusion = _Occlusion;
	o.Specular = lerp(_SpecularIntensity, o.Albedo * _SpecularIntensity, _MetallicIntensity); 
	o.Smoothness = _Glossiness;
	o.Normal = IN.norm.xyz;
	o.Emission = 0;
  outEmission = ProceduralStandardSpecular_Deferred (o, worldViewDir, outGBuffer0, outGBuffer1, outGBuffer2); //GI neccessary here!
  outDepth = IN.pos.z;
  //Calculate Motion Vector
  //I dont want to have grass' motion vector
  /*
  half4 screenPos = mul(_NonJitterVP, float4(worldPos, 1));
  half2 screenUV = GetScreenPos(screenPos);
  outMotionVector = CalculateMotionVector(_LastVp, worldPos, screenUV);*/
}

ENDCG

//Pass 0 deferred
Pass {
stencil{
  Ref 1
  comp always
  pass replace
}
ZWrite on
Cull Back
ZTest Less
CGPROGRAM
#define UNITY_PASS_DEFERRED
#pragma vertex vert_surf
#pragma fragment frag_surf
#pragma geometry geom
#pragma exclude_renderers nomrt
ENDCG
}
}
CustomEditor "SpecularShaderEditor"
}

