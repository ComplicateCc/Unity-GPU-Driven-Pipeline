Shader "Hidden/DeferredPointLight"
{
    SubShader
    {

CGINCLUDE
#pragma target 5.0
            #include "UnityCG.cginc"
            #include "CGINC/VoxelLight.cginc"
            #include "CGINC/Shader_Include/Common.hlsl"
            #include "CGINC/Shader_Include/BSDF_Library.hlsl"
            #include "CGINC/Shader_Include/AreaLight.hlsl"
            
            Texture2D _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;
            Texture2D<half4> _CameraGBufferTexture0; SamplerState sampler_CameraGBufferTexture0;
            Texture2D<half4> _CameraGBufferTexture1; SamplerState sampler_CameraGBufferTexture1;
            Texture2D<half4> _CameraGBufferTexture2; SamplerState sampler_CameraGBufferTexture2;
            TextureCube<half> _CubeShadowMap; SamplerState sampler_CubeShadowMap;
            StructuredBuffer<float3> verticesBuffer;
            StructuredBuffer<PointLight> _AllPointLight;
            StructuredBuffer<uint> _PointLightIndexBuffer;
            float4 _LightPos;
            float3  _LightColor;
            float _LightIntensity;
            float2 _CameraClipDistance; //X: Near Y: Far - Near

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
                float4 worldPosition = mul(_InvVP, float4(uv * 2 - 1, sceneDepth, 1));
                worldPosition /= worldPosition.w;
                half3 albedoColor = _CameraGBufferTexture0.Sample(sampler_CameraGBufferTexture0, uv).xyz;
                half3 worldNormal = _CameraGBufferTexture2.Sample(sampler_CameraGBufferTexture2, uv).xyz * 2 - 1;
                float3 lightPosition = _LightPos.xyz;
                float3 lightDir = normalize(lightPosition - worldPosition.xyz);
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPosition.xyz);
                float NoL = saturate(dot(lightDir, worldNormal));
                half distanceRange = distance(worldPosition.xyz, lightPosition);
                half distanceSqr = dot(lightPosition - worldPosition.xyz, lightPosition - worldPosition.xyz);
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
                float4 worldPosition = mul(_InvVP, float4(uv * 2 - 1, sceneDepth, 1));
                worldPosition /= worldPosition.w;
                float3 albedoColor = _CameraGBufferTexture0.Sample(sampler_CameraGBufferTexture0, uv).xyz;
                float3 worldNormal = _CameraGBufferTexture2.Sample(sampler_CameraGBufferTexture2, uv).xyz * 2 - 1;
                float3 lightPosition = _LightPos.xyz;
                float3 lightDir = lightPosition - worldPosition.xyz;
                half lenOfLightDir = length(lightDir);
                lightDir /= lenOfLightDir;
                half shadowDist = _CubeShadowMap.Sample(sampler_CubeShadowMap, lightDir * float3(-1,-1,1));
                half lightDist = (lenOfLightDir - 0.1) / _LightPos.w;
                if(lightDist > shadowDist) return 0;
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPosition.xyz);
                half NoL = saturate(dot(lightDir, worldNormal));
                half distanceRange = distance(worldPosition.xyz, lightPosition);
                half distanceSqr = dot(lightPosition - worldPosition.xyz, lightPosition - worldPosition.xyz);
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
            Cull off ZWrite Off ZTest Greater
            Blend one one
            
            CGPROGRAM

            #pragma vertex screenVert
            #pragma fragment frag

            TextureCubeArray<half> _CubeShadowMapArray; SamplerState sampler_CubeShadowMapArray;

            inline uint2 GetVoxelIte(float eyeDepth, float2 uv)
            {
                float rate = saturate((eyeDepth - _CameraClipDistance.x) / _CameraClipDistance.y);
                uint3 voxelValue =uint3((uint2)(uv * float2(XRES, YRES)), (uint)(rate * ZRES));
                uint sb = GetIndex(voxelValue, VOXELSIZE);
                return uint2(sb + 1, _PointLightIndexBuffer[sb]);
            }

            half3 frag(v2fScreen i) : SV_TARGET
            {
                float2 uv = i.uv;
                float SceneDepth = _CameraDepthTexture.Sample(sampler_CameraDepthTexture, uv).r;
                float2 NDC_UV = uv * 2 - 1;

                //////Screen Data
                half3 AlbedoColor = _CameraGBufferTexture0.SampleLevel(sampler_CameraGBufferTexture0, uv, 0).rgb;
                half3 WorldNormal = _CameraGBufferTexture2.SampleLevel(sampler_CameraGBufferTexture2, uv, 0).rgb * 2 - 1;
                half4 SpecularColor = _CameraGBufferTexture1.SampleLevel(sampler_CameraGBufferTexture1, uv, 0); 
                half Roughness = clamp(1 - SpecularColor.a, 0.02, 1);
                float4 WorldPos = mul(_InvVP, float4(NDC_UV, SceneDepth, 1));
                WorldPos /= WorldPos.w;


                half ShadowTrem = 0;
                half3 ShadingColor = 0;
                uint2 LightIndex = GetVoxelIte(LinearEyeDepth(SceneDepth), uv);

                for(uint c = LightIndex.x; c < LightIndex.y; c++)
                {
                    PointLight Light = _AllPointLight[_PointLightIndexBuffer[c]];

                    //////Light Data
                    half LumianceIntensity = Light.lightIntensity;
                    half LightRange = Light.sphere.a;
                    half3 LightPos = Light.sphere.rgb;
                    half3 LightColor = Light.lightColor;
                    half3 ViewDir = normalize(_WorldSpaceCameraPos.rgb - WorldPos.rgb);
                    half3 Un_LightDir = LightPos - WorldPos.xyz;
                    half3 LightDir = normalize(Un_LightDir);
                    half3 HalfDir = normalize(ViewDir + LightDir);
                    //////Shadow
                    if(Light.shadowIndex >= 0){
                        half Length_LightDir = length(Un_LightDir);
                        half DepthMap = (Length_LightDir - 0.025) / LightRange;
                        half ShadowMap = _CubeShadowMapArray.Sample(sampler_CubeShadowMapArray, float4(Un_LightDir * float3(-1, -1, 1), Light.shadowIndex));
                        ShadowTrem = DepthMap <= ShadowMap;
                    }

                    //////BSDF Variable
                    BSDFContext LightData;
                    Init(LightData, WorldNormal, ViewDir, LightDir, HalfDir);

                    //////Shading
                    half3 Energy = Point_Energy(Un_LightDir, LightColor, LumianceIntensity, LightRange * 5, LightData.NoL) * ShadowTrem;
                    ShadingColor += Defult_Lit(LightData, Energy, 1, AlbedoColor, SpecularColor, Roughness, 1);
                }

                return ShadingColor;
            }
            ENDCG
        }
    }
}