Shader "Hidden/DownSampleDepth"
{
    SubShader
    {
        CGINCLUDE
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

            Texture2D<float> _CameraDepthTexture; SamplerState sampler_CameraDepthTexture; float4 _CameraDepthTexture_TexelSize;
            Texture2D<float2> _CurrRT; SamplerState sampler_CurrRT; float4 _CurrRT_TexelSize;

        ENDCG
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float frag (v2f i) : SV_Target
            {
                float2 offset = _CameraDepthTexture_TexelSize.xy * 0.5;
                float4 depth = float4(
                    _CameraDepthTexture.Sample(sampler_CameraDepthTexture, i.uv + offset),
                    _CameraDepthTexture.Sample(sampler_CameraDepthTexture, i.uv - offset),
                    _CameraDepthTexture.Sample(sampler_CameraDepthTexture, i.uv + float2(offset.x, -offset.y)),
                    _CameraDepthTexture.Sample(sampler_CameraDepthTexture, i.uv - float2(offset.x, -offset.y))
                );
                #if UNITY_REVERSED_Z
                depth.xy = min(depth.xy, depth.zw);
                return min(depth.x, depth.y);
                #else
                depth.xy = max(depth.xy, depth.zw);
                return max(depth.x, depth.y);
                #endif
            }
            ENDCG
        }
    }
}
