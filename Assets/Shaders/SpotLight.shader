Shader "Hidden/SpotLight"
{
	CGINCLUDE
    #pragma vertex vert
    #pragma fragment frag
#pragma target 5.0
#include "UnityCG.cginc"
#include "UnityDeferredLibrary.cginc"
#include "UnityPBSLighting.cginc"
#include "UnityStandardUtils.cginc"
#include "UnityGBuffer.cginc"
#include "UnityStandardBRDF.cginc"
    float4 _LightFinalColor;//RGB:Color A:Range
    Texture2D _CameraGBufferTexture0; SamplerState sampler_CameraGBufferTexture0;
	Texture2D _CameraGBufferTexture1; SamplerState sampler_CameraGBufferTexture1;
	Texture2D _CameraGBufferTexture2; SamplerState sampler_CameraGBufferTexture2;
    float4x4 _InvVP;

float3 GetSpotLight(float3 worldPos)
{
    float3 dir = worldPos - _LightPos.xyz;
    float dirLen = length(dir);
    
    float angle =  acos(dot(-_LightDir.xyz, dir / dirLen));
    float softValue = 1 - angle / _LightDir.w;
    float atten = saturate(exp(-dirLen / _LightFinalColor.w * 8) * softValue);
    return _LightFinalColor.rgb * atten;
}

ENDCG
    SubShader
    {
        Cull back ZWrite Off ZTest Greater
		Blend one one
        Pass
        {
            CGPROGRAM
            
            struct v2f
            {
                float4 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
	//near 0
	//far left down 1
	//far right down 2
	//far left top 3
	//far right top 4
	static const uint VertIndex[18] = 
	{
		0,1,2,
		0,2,4,
		0,4,3,
		0,3,1,
        3,2,1,
        4,2,3
	};

	float4 _WorldPoses[5];
            v2f vert (uint vertexID : SV_VertexID)
            {

				float4 worldpos = _WorldPoses[VertIndex[vertexID]];
				worldpos.w = 1;
                 v2f o;
				o.vertex = mul(UNITY_MATRIX_VP, worldpos);
				o.uv = ComputeScreenPos(o.vertex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                i.uv /= i.uv.w;
                float4 gbuffer0 = _CameraGBufferTexture0.Sample(sampler_CameraGBufferTexture0, i.uv);
    			float4 gbuffer1 = _CameraGBufferTexture1.Sample(sampler_CameraGBufferTexture1, i.uv);
    			float4 gbuffer2 = _CameraGBufferTexture2.Sample(sampler_CameraGBufferTexture2, i.uv);
				float depth = gbuffer2.w;
                float4 worldPos = mul(_InvVP, float4(i.uv.xy * 2 - 1, depth, 1));
                worldPos /= worldPos.w;
                float3 lightColor = GetSpotLight(worldPos.xyz);
                UnityStandardData data = UnityStandardDataFromGbuffer(gbuffer0, gbuffer1, gbuffer2);
				float3 eyeVec = normalize(worldPos.xyz - _WorldSpaceCameraPos);
				half oneMinusReflectivity = 1 - SpecularStrength(data.specularColor.rgb);
    			UnityIndirect ind;
    			UNITY_INITIALIZE_OUTPUT(UnityIndirect, ind);
    			ind.diffuse = 0;
    			ind.specular = 0;
				UnityLight light;
				light.dir = _LightPos.xyz - worldPos.xyz;
				light.color = lightColor;
                return UNITY_BRDF_PBS (data.diffuseColor, data.specularColor, oneMinusReflectivity, data.smoothness, data.normalWorld, -eyeVec, light, ind);
            }
            ENDCG
        }
    }
}
