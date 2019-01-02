Shader "Hidden/Debug"
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
            #pragma target 5.0
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

            Texture3D<int> _MainTex;

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = _MainTex[uint3(i.uv * uint2(16, 8), 0)] * 0.1;
                return col;
            }
            ENDCG
        }
    }
}
