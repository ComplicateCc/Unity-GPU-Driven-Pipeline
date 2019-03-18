Shader "Unlit/Debug"
{
    SubShader
    {
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
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            Texture2DArray _MainTex;
            SamplerState sampler_MainTex;

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = _MainTex.Sample(sampler_MainTex, float3(i.uv, 0));

                return col;
            }
            ENDCG
        }
    }
}
