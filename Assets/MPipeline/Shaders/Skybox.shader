Shader "Skybox/MaxwellPipelineSkybox"
{
    Properties
    {
        _MainTex ("Cubemap", Cube) = "white" {}
    }
    SubShader
    {
        // No culling or depth
       
        Pass
        {
             Cull Off ZWrite Off ZTest LEqual
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
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            float4x4 _InvVP;
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = v.vertex;
                o.uv = v.uv * 2  -1;
                return o;
            } 
            samplerCUBE _MainTex;

            half4 frag (v2f i) : SV_TARGET
            {
                float4 worldPos = mul(_InvVP, float4(i.uv, 0.5, 1));
                worldPos /= worldPos.w;
                float3 viewDir = normalize(worldPos.xyz - _WorldSpaceCameraPos);
                return  texCUBE(_MainTex, viewDir);
            }
            ENDCG
        }
    }
}
