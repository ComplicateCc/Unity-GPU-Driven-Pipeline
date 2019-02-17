Shader "Unlit/ReadLod"
{
    Properties
    {
        _MainTex ("Texture", any) = "white" {}
        _Index("Index", Range(0, 50)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Cull off ZWrite off ZTest Always
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };
            Texture2DArray<half4> _MainTex; SamplerState sampler_MainTex;
            float _Index;
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = v.vertex;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _MainTex.Sample(sampler_MainTex, float3(i.uv, _Index));
            }
            ENDCG
        }
    }
}
