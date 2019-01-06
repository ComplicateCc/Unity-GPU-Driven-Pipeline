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
            #pragma multi_compile _ POINTLIGHT
            #pragma multi_compile _ SPOTLIGHT
            Texture2D _CameraDepthTexture; SamplerState sampler_CameraDepthTexture;
            Texture2D<half4> _CameraGBufferTexture0; SamplerState sampler_CameraGBufferTexture0;
            Texture2D<half4> _CameraGBufferTexture1; SamplerState sampler_CameraGBufferTexture1;
            Texture2D<half4> _CameraGBufferTexture2; SamplerState sampler_CameraGBufferTexture2;

            StructuredBuffer<PointLight> _AllPointLight;
            StructuredBuffer<uint> _PointLightIndexBuffer;
            StructuredBuffer<SpotLight> _AllSpotLight;
            StructuredBuffer<uint> _SpotLightIndexBuffer;
            
            float4 _LightPos;
            float3  _LightColor;
            float _LightIntensity;
            float2 _CameraClipDistance; //X: Near Y: Far - Near

            float4x4 _InvVP;

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
            Cull off ZWrite Off ZTest Greater
            Blend one one
            
            CGPROGRAM

            #pragma vertex screenVert
            #pragma fragment frag

            TextureCubeArray<half> _CubeShadowMapArray; SamplerState sampler_CubeShadowMapArray;
            Texture2DArray<half> _SpotMapArray; SamplerState sampler_SpotMapArray;

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


                half ShadowTrem;
                half3 ShadingColor = 0;
                float rate = saturate((LinearEyeDepth(SceneDepth) - _CameraClipDistance.x) / _CameraClipDistance.y);
                uint3 voxelValue =uint3((uint2)(uv * float2(XRES, YRES)), (uint)(rate * ZRES));
                uint sb = GetIndex(voxelValue, VOXELSIZE,  (MAXLIGHTPERCLUSTER + 1));
                uint2 LightIndex;// = uint2(sb + 1, _PointLightIndexBuffer[sb]);
                uint c;
                float3 ViewDir = normalize(_WorldSpaceCameraPos.rgb - WorldPos.rgb);
                #if SPOTLIGHT
                
                LightIndex = uint2(sb + 1, _SpotLightIndexBuffer[sb]);
                [loop]
                for(c = LightIndex.x; c < LightIndex.y; c++)
                {
                    SpotLight Light = _AllSpotLight[_SpotLightIndexBuffer[c]];
                    Cone SpotCone = Light.lightCone;
                    
                        //////Light Data
                        float LumianceIntensity = Light.lightIntensity;
                        float LightRange = SpotCone.radius;
                        float3 LightPos = SpotCone.vertex;
                        float3 LightColor = Light.lightColor;
                        
                        half LightAngle = Light.angle;
                        float3 LightForward = SpotCone.direction;
                        float3 Un_LightDir = LightPos - WorldPos.xyz;
                        float3 LightDir = normalize(Un_LightDir);
                        float3 HalfDir = normalize(ViewDir + LightDir);

                        //////BSDF Variable
                        BSDFContext LightData;
                        Init(LightData, WorldNormal, ViewDir, LightDir, HalfDir);

                        //////Shading
                        ShadowTrem = 1;
                        float3 Energy = Spot_Energy(-Un_LightDir, LightColor, LightForward, cos(LightAngle * 0.5), cos(LightAngle), LumianceIntensity, 1.0 / LightRange, LightData.NoL);
                        if(Light.shadowIndex >= 0)
                    {
                        float4 clipPos = mul(Light.vpMatrix, WorldPos);
                        clipPos /= clipPos.w;
                        float2 uv = clipPos.xy * 0.5 + 0.5;
                        half shadowDist = _SpotMapArray.Sample(sampler_SpotMapArray, float3(uv, Light.shadowIndex));
                        ShadowTrem = (length(Un_LightDir) - 0.25) / SpotCone.height < shadowDist;
                    }
                        ShadingColor += max(0, Defult_Lit(LightData, Energy, 1, AlbedoColor, SpecularColor, Roughness, 1) * ShadowTrem);
                    

                }
                #endif
                #if POINTLIGHT
                ShadowTrem = 1;
                LightIndex = uint2(sb + 1, _PointLightIndexBuffer[sb]);
                [loop]
                for(c = LightIndex.x; c < LightIndex.y; c++)
                {
                    PointLight Light = _AllPointLight[_PointLightIndexBuffer[c]];

                    //////Light Data
                    float LumianceIntensity = Light.lightIntensity;
                    float LightRange = Light.sphere.a;
                    float3 LightPos = Light.sphere.rgb;
                    float3 LightColor = Light.lightColor;
                    
                    float3 Un_LightDir = LightPos - WorldPos.xyz;
                    float3 LightDir = normalize(Un_LightDir);
                    float3 HalfDir = normalize(ViewDir + LightDir);
                    
                    //////Shadow
                    if(Light.shadowIndex >= 0){
                        float Length_LightDir = length(Un_LightDir);
                        float DepthMap = (Length_LightDir - 0.25) / LightRange;
                        float ShadowMap = _CubeShadowMapArray.Sample(sampler_CubeShadowMapArray, float4(Un_LightDir * float3(-1, -1, 1), Light.shadowIndex));
                       // ShadingColor += ShadowMap;
                      //  continue;
                        ShadowTrem = saturate(DepthMap < ShadowMap);
                    }

                    //////BSDF Variable
                    BSDFContext LightData;
                    Init(LightData, WorldNormal, ViewDir, LightDir, HalfDir);

                    //////Shading
                    float3 Energy = Point_Energy(Un_LightDir, LightColor, LumianceIntensity, 1 / LightRange, LightData.NoL) * ShadowTrem;
                   ShadingColor += max(0, Defult_Lit(LightData, Energy, 1, AlbedoColor, SpecularColor, Roughness, 1));
                }
                #endif

                return ShadingColor;
            }
            ENDCG
        }
    }
}