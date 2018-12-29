Shader "Unlit/Terrain"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "CGINC/Terrain.cginc"
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                nointerpolation uint4 texID : TEXCOORD1;
            };

            v2f vert (uint vertexID : SV_VERTEXID, uint instanceID : SV_INSTANCEID)
            {
                v2f o = (v2f)0;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                return 1;
            }
            ENDCG
        }
    }
}
