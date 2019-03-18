Shader "Hidden/DepthToLinear"
{
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = v.vertex;
                o.uv = v.uv;
                return o;
            }

            Texture2D<float> _MainTex; SamplerState sampler_MainTex;
            Texture2D<float> _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;
            float4 _MainTex_TexelSize;
            float frag (v2f i) : SV_Target
            {
                float2 offset = _MainTex_TexelSize.xy * 0.5;
                float occDepth = _MainTex.SampleLevel(sampler_MainTex, i.uv, 0);
                float4 readDepth = float4(_CameraDepthTexture.SampleLevel(sampler_CameraDepthTexture, i.uv + offset, 0),
                                        _CameraDepthTexture.SampleLevel(sampler_CameraDepthTexture, i.uv - offset, 0),
                                        _CameraDepthTexture.SampleLevel(sampler_CameraDepthTexture, i.uv + float2(offset.x, -offset.y), 0),
                                        _CameraDepthTexture.SampleLevel(sampler_CameraDepthTexture, i.uv + float2(-offset.x, offset.y), 0));
                #if UNITY_REVERSED_Z
                readDepth.xy = min(readDepth.xy, readDepth.zw);
                readDepth.x = min(readDepth.x, readDepth.y);
                #else
                readDepth.xy = max(readDepth.xy, readDepth.zw);
                readDepth.x = max(readDepth.x, readDepth.y);
                #endif
                float texDepth = Linear01Depth(readDepth.x);
                return min(occDepth, texDepth);
                
            }
            ENDCG
        }
    }
}
