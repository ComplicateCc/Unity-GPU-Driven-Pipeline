Shader "Hidden/DeferredPointLight"
{
    SubShader
    {

CGINCLUDE
#pragma target 5.0
            #include "UnityCG.cginc"
            //#include "PointLight.cginc"
#define ZRES 128
#define RES 16
            Texture2D _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;
            Texture2D<half4> _CameraGBufferTexture0; SamplerState sampler_CameraGBufferTexture0;
            Texture2D<half4> _CameraGBufferTexture1; SamplerState sampler_CameraGBufferTexture1;
            Texture2D<half4> _CameraGBufferTexture2; SamplerState sampler_CameraGBufferTexture2;
            TextureCube<half> _CubeShadowMap; SamplerState sampler_CubeShadowMap;
            StructuredBuffer<float3> verticesBuffer;
            float4 _LightPos;
            float3  _LightColor;
            float _LightIntensity;

            struct PointLight{
                float3 lightColor;
                float lightIntensity;
                float4 sphere;
            };

            float4x4 _InvVP;

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
            Cull front ZWrite Off ZTest Greater
            Blend one one
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
                    Cull front ZWrite Off ZTest Greater
        Blend one one
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
        Pass
        {
            Cull off ZWrite off ZTest Always
           Blend one one
            CGPROGRAM
            #pragma vertex screenVert
            #pragma fragment frag
            float3 _CameraForward;
            float3 _CameraNearPos;
            float3 _CameraFarPos;
            Texture3D<int2> _PointLightTexture;
            StructuredBuffer<PointLight> _AllPointLight;
            StructuredBuffer<uint> _PointLightIndexBuffer;
            struct v2fScreen
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            v2fScreen screenVert (appdata v)
            {
                v2fScreen o;
                o.vertex = v.vertex;
                o.uv = v.uv;
                return o;
            }
            half3 frag(v2fScreen i) : SV_TARGET
            {
                
                float2 uv = i.uv;
                
                float sceneDepth = _CameraDepthTexture.Sample(sampler_CameraDepthTexture, uv).r;
                float2 clip = uv * 2 - 1;
                
                float4 worldPostion = mul(_InvVP, float4(clip, sceneDepth, 1));
                float4 worldFarPosition = mul(_InvVP, float4(clip, 0, 1));
                float4 worldNearPosition = mul(_InvVP, float4(clip, 1, 1));
                worldFarPosition /= worldFarPosition.w;
                worldNearPosition /= worldNearPosition.w;
                worldPostion /= worldPostion.w;
                
                float rate = distance(worldNearPosition.xyz, worldPostion.xyz) / distance(worldFarPosition.xyz, worldNearPosition.xyz);
                uint3 voxelValue =uint3((uint2)(uv * RES), (uint)(rate * ZRES));
                int2 index = _PointLightTexture[voxelValue];
                half3 color = 0;
                for(int c = index.x; c < index.y; c++)
                {
                    PointLight pt = _AllPointLight[_PointLightIndexBuffer[c]];
                    color += pt.lightColor * saturate(1 - distance(worldPostion.xyz , pt.sphere.xyz) / pt.sphere.w);
                }
                 return color;
            }
            ENDCG
        }
    }
}
