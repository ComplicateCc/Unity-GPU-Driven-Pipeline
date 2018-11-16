Shader "Hidden/DeferredPointLight"
{
    SubShader
    {
        Cull front ZWrite Off ZTest Greater
        Blend one one
CGINCLUDE

            #include "UnityCG.cginc"
            //#include "PointLight.cginc"

            Texture2D _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;
            Texture2D<half4> _CameraGBufferTexture0; SamplerState sampler_CameraGBufferTexture0;
            Texture2D<half4> _CameraGBufferTexture1; SamplerState sampler_CameraGBufferTexture1;
            Texture2D<half4> _CameraGBufferTexture2; SamplerState sampler_CameraGBufferTexture2;
            TextureCube<half> _CubeShadowMap; SamplerState sampler_CubeShadowMap;
            float4 _LightPos;
            float3  _LightColor;
            float _LightIntensity;
            
            float4x4 _InvVP;
            float4x4 _LastVp;
            StructuredBuffer<float3> verticesBuffer;
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 uv : TEXCOORD0;
            };

            v2f vert (uint id : SV_VERTEXID)
            {
                v2f o;
                o.vertex = mul(UNITY_MATRIX_VP, float4((verticesBuffer[id] * _LightPos.w * 2 + _LightPos.xyz), 1));
                o.vertex.z = max(o.vertex.z, 0);
                o.uv = ComputeScreenPos(o.vertex);
                return o;
            }

            inline float Square(float A)
            {
                return A * A;
            }
ENDCG
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            half3 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv.xy / i.uv.w;
                float sceneDepth = _CameraDepthTexture.Sample(sampler_CameraDepthTexture, uv).r;
                float4 worldPostion = mul(_InvVP, float4(uv * 2 - 1, sceneDepth, 1));
                worldPostion /= worldPostion.w;
                half3 albedoColor = _CameraGBufferTexture0.Sample(sampler_CameraGBufferTexture0, uv).xyz;
                half3 worldNormal = _CameraGBufferTexture2.Sample(sampler_CameraGBufferTexture2, uv).xyz * 2 - 1;
                float3 lightPosition = _LightPos.xyz;
                float3 lightDir = normalize(lightPosition - worldPostion.xyz);
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPostion.xyz);
                float NoL = saturate(dot(lightDir, worldNormal));
                half distanceRange = distance(worldPostion.xyz, lightPosition);
                half distanceSqr = dot(lightPosition - worldPostion.xyz, lightPosition - worldPostion.xyz);
                half rangeFalloff = Square(saturate(1 - Square(distanceSqr * Square(abs(1 / _LightPos.w) / 100))));
                half LumianceIntensity = max(0, (_LightIntensity / 4)) / ((4 * UNITY_PI) * pow(distanceRange, 2));
                half pointLightEnergy = LumianceIntensity * NoL * rangeFalloff;
                half3 pointLight_Effect = (pointLightEnergy * _LightColor) * albedoColor;
                return pointLight_Effect;
            }
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half3 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv.xy / i.uv.w;
                float sceneDepth = _CameraDepthTexture.Sample(sampler_CameraDepthTexture, uv).r;
                float4 worldPostion = mul(_InvVP, float4(uv * 2 - 1, sceneDepth, 1));
                worldPostion /= worldPostion.w;
                float3 albedoColor = _CameraGBufferTexture0.Sample(sampler_CameraGBufferTexture0, uv).xyz;
                float3 worldNormal = _CameraGBufferTexture2.Sample(sampler_CameraGBufferTexture2, uv).xyz * 2 - 1;
                float3 lightPosition = _LightPos.xyz;
                float3 lightDir = lightPosition - worldPostion.xyz;
                half lenOfLightDir = length(lightDir);
                lightDir /= lenOfLightDir;
                half shadowDist = _CubeShadowMap.Sample(sampler_CubeShadowMap, lightDir * float3(-1,-1,1));
                half lightDist = (lenOfLightDir - 0.1) / _LightPos.w;
                if(lightDist > shadowDist) return 0;
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPostion.xyz);
                half NoL = saturate(dot(lightDir, worldNormal));
                half distanceRange = distance(worldPostion.xyz, lightPosition);
                half distanceSqr = dot(lightPosition - worldPostion.xyz, lightPosition - worldPostion.xyz);
                half rangeFalloff = Square(saturate(1 - Square(distanceSqr * Square(abs(1 / _LightPos.w) / 100))));
                half LumianceIntensity = max(0, (_LightIntensity / 4)) / ((4 * UNITY_PI) * pow(distanceRange, 2));
                half pointLightEnergy = LumianceIntensity * NoL * rangeFalloff;
                half3 pointLight_Effect = (pointLightEnergy * _LightColor) * albedoColor;
                return pointLight_Effect;
            }
            ENDCG
        }
    }
}
