Shader "Unlit/Reflection"
{

CGINCLUDE
#include "UnityCG.cginc"
#pragma target 5.0
            struct appdata
            {
                float4 vertex : POSITION;
                
            };

            struct v2f
            {
                float4 screenUV : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            float4x4 _InvVP;    //Inverse View Project Matrix
            float3 _Size;
            TextureCube<half4> _ReflectionProbe; SamplerState sampler_ReflectionProbe;
            Texture2D<half4> _CameraGBufferTexture0; SamplerState sampler_CameraGBufferTexture0;       //RGB Diffuse A AO
            Texture2D<half4> _CameraGBufferTexture1; SamplerState sampler_CameraGBufferTexture1;       //RGB Specular A Smoothness
            Texture2D<half3> _CameraGBufferTexture2; SamplerState sampler_CameraGBufferTexture2;       //RGB Normal
            Texture2D<float> _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;
ENDCG
    SubShader
    {
        ZTest Greater ZWrite off
        Cull front
        Blend one one
        Tags { "RenderType"="Opaque" }
        LOD 100
//Pass 0 Regular Projection
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenUV = ComputeScreenPos(o.vertex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 screenUV = i.screenUV.xy / i.screenUV.w;
                float depth = _CameraDepthTexture.Sample(sampler_CameraDepthTexture, screenUV);
                float4 v_worldPos = mul(_InvVP, float4(screenUV * 2 - 1, depth, 1));
                float3 worldPos = v_worldPos.xyz / v_worldPos.w;
                float3 viewDir = worldPos - _WorldSpaceCameraPos;
                half3 normal = _CameraGBufferTexture2.Sample(sampler_CameraGBufferTexture2, screenUV);
                float3 sampleVector = reflect(normalize(viewDir), normalize(normal));
                float4 result = _ReflectionProbe.Sample(sampler_ReflectionProbe, sampleVector);
                return result;
            }
            ENDCG
        }
    }
}
