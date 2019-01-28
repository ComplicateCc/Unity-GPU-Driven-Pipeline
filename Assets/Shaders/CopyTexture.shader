Shader "Unlit/CopyTexture"
{
    SubShader
    {
        LOD 100
        ZWrite off ZTest Always Cull off
        CGINCLUDE
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
            StructuredBuffer<uint> _TextureBuffer;
            float2 _TextureSize;
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = v.vertex;
                o.uv = v.uv;
                return o;
            }
            uint4 GetValues(uint value)
            {
                uint4 values = 0;
                values.x = value & 255;
                value >>= 8;
                values.y = value & 255;
                value >>= 8;
                values.z = value & 255;
                value >>= 8;
                values.w = value & 255;
                return values;
            }
        ENDCG
        //Pass 0: Linear Transform
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag (v2f i) : SV_Target
            {
                if(i.uv.x > 0.5)
                    i.uv.x -= 0.5;
                else if(i.uv.x < 0.5)
                    i.uv.x += 0.5;
                return ((half4)GetValues(_TextureBuffer[i.uv.y * _TextureSize.y * _TextureSize.x + i.uv.x * _TextureSize.x])) / 255.0;
            }
            ENDCG
        }
        //Pass 1: Non Linear Transform
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag (v2f i) : SV_Target
            {
                if(i.uv.x > 0.5)
                    i.uv.x -= 0.5;
                else if(i.uv.x < 0.5)
                    i.uv.x += 0.5;
                half4 value = ((half4)GetValues(_TextureBuffer[i.uv.y * _TextureSize.y * _TextureSize.x + i.uv.x * _TextureSize.x])) / 255.0;
                value = lerp(pow((value+0.055)/1.055, 2.4), value / 12.92, step(value, 0.0404482362771082));
                return value;
            }
            ENDCG
        }
    }
}
