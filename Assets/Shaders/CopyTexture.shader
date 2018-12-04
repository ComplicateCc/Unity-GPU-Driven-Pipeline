Shader "Unlit/CopyTexture"
{
    SubShader
    {
        LOD 100
        ZWrite off ZTest Always Cull off
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
            StructuredBuffer<float4> _TextureBuffer;
            float2 _TextureSize;
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = v.vertex;
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                if(i.uv.x > 0.5)
                    i.uv.x -= 0.5;
                else if(i.uv.x < 0.5)
                    i.uv.x += 0.5;
                return _TextureBuffer[i.uv.y * _TextureSize.y * _TextureSize.x + i.uv.x * _TextureSize.x];
            }
            ENDCG
        }
    }
}
