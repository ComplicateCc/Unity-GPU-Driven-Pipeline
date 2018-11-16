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
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = v.vertex;
                o.uv = v.uv;
                o.worldPos = _FarClipCorner[v.uv.x + v.uv.y * 2].xyz;
                return o;
            }

            samplerCUBE _MainTex;

            float4 frag (v2f i) : SV_Target
            {
                float3 viewDir = normalize(i.worldPos - _WorldSpaceCameraPos);
                return texCUBE(_MainTex, viewDir);
            }
            ENDCG
        }
    }
}
