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
            float4 _FarClipCorner[4];
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };
            #define GetScreenPos(pos) ((float2(pos.x, pos.y) * 0.5) / pos.w + 0.5)
            float4x4 _LastVp;
            float4x4 _NonJitterVP;
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
                o.worldPos = _FarClipCorner[v.uv.x + v.uv.y * 2].xyz;
                return o;
            } 
            samplerCUBE _MainTex;

            void frag (v2f i, 
            out half4 skyboxColor : SV_TARGET0,
            out half2 outMotionVector : SV_TARGET1)
            {
                float3 viewDir = normalize(i.worldPos - _WorldSpaceCameraPos);
                skyboxColor = texCUBE(_MainTex, viewDir);
                half4 screenPos = mul(_NonJitterVP, float4(i.worldPos, 1));
                half2 screenUV = GetScreenPos(screenPos);
                outMotionVector = CalculateMotionVector(_LastVp, i.worldPos, screenUV);
            }
            ENDCG
        }
    }
}
