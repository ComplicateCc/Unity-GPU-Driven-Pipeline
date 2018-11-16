Shader "Unlit/PointlightDepth"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #include "CGINC/Procedural.cginc"
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };
            float4x4 _VP;
            float4 _LightPos;
            v2f vert (uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID) 
            {
                Point v = getVertex(vertexID, instanceID);
                v2f o;
                o.worldPos = v.vertex;
                o.vertex = mul(_VP, float4(v.vertex, 1));
                return o;
            }

            half frag (v2f i) : SV_Target
            {
               return distance(i.worldPos, _LightPos.xyz) / _LightPos.w;
            } 
            ENDCG
        }
    }
}
