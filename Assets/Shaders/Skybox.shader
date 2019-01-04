Shader "Skybox/MaxwellPipelineSkybox"
{
    Properties
    {
        _MainTex ("Cubemap", Cube) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest LEqual

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
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            #define GetScreenPos(pos) ((float2(pos.x, pos.y) * 0.5) / pos.w + 0.5)
            float4x4 _LastVp;
            float4x4 _NonJitterVP;
            float4x4 _InvVP;
            inline half2 CalculateMotionVector(float4x4 lastvp, half3 worldPos, half2 screenUV)
            {
	            half4 lastScreenPos = mul(lastvp, half4(worldPos, 1));
	            half2 lastScreenUV = GetScreenPos(lastScreenPos);
	            return screenUV - lastScreenUV;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = v.vertex;
                o.uv = v.uv * 2  -1;
                return o;
            } 
            samplerCUBE _MainTex;

            void frag (v2f i, 
            out half4 skyboxColor : SV_TARGET0,
            out half2 outMotionVector : SV_TARGET1)
            {
                float4 worldPos = mul(_InvVP, float4(i.uv, 0.5, 1));
                worldPos /= worldPos.w;
                float3 viewDir = normalize(worldPos.xyz - _WorldSpaceCameraPos);
                skyboxColor = texCUBE(_MainTex, viewDir);
                half4 screenPos = mul(_NonJitterVP, float4(worldPos.xyz, 1));
                half2 screenUV = GetScreenPos(screenPos);
                outMotionVector = CalculateMotionVector(_LastVp, worldPos.xyz, screenUV);
            }
            ENDCG
        }
    }
}
