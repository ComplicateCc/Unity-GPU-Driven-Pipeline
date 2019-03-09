#ifndef SH_RUNTIME
#define SH_RUNTIME
#include "GlobalIllumination.cginc"
            Texture3D<float4> _CoeffTexture0; SamplerState sampler_CoeffTexture0;
            Texture3D<float4> _CoeffTexture1; SamplerState sampler_CoeffTexture1;
            Texture3D<float4> _CoeffTexture2; SamplerState sampler_CoeffTexture2;
            Texture3D<float4> _CoeffTexture3; SamplerState sampler_CoeffTexture3;
            Texture3D<float4> _CoeffTexture4; SamplerState sampler_CoeffTexture4;
            Texture3D<float4> _CoeffTexture5; SamplerState sampler_CoeffTexture5;
            Texture3D<float4> _CoeffTexture6; SamplerState sampler_CoeffTexture6;
            float4x4 _WorldToLocalMatrix;
            
            float3 GetSHUV(float3 worldPos)
            {
                 float4 localPos = mul(_WorldToLocalMatrix, float4(worldPos, 1));
                 localPos /= localPos.w;
                 return localPos.xyz + 0.5;
                 
            }
            float3 GetSHColor(float3 worldNormal, float3 worldPos)
            {
                const float A0 = 3.1415927;
				const float A1 = 2.094395;
				const float A2 = 0.785398;
                float3 uv = GetSHUV(worldPos);
                GETCOEFF(worldNormal);
                float3 color[9];
                float4 first = _CoeffTexture0.SampleLevel(sampler_CoeffTexture0, uv, 0);
                float4 second = _CoeffTexture1.SampleLevel(sampler_CoeffTexture1, uv, 0);
                color[0] = first.rgb;
                color[1] = float3(first.a, second.rg);
                first = _CoeffTexture2.SampleLevel(sampler_CoeffTexture2, uv, 0);
                color[2] = float3(second.ba, first.r);
                color[3] = float3(first.gba);
                first = _CoeffTexture3.SampleLevel(sampler_CoeffTexture3, uv, 0);
                second = _CoeffTexture4.SampleLevel(sampler_CoeffTexture4, uv, 0);
                color[4] = first.rgb;
                color[5] = float3(first.a, second.rg);
                first = _CoeffTexture5.SampleLevel(sampler_CoeffTexture5, uv, 0);
                color[6] = float3(second.ba, first.r);
                color[7] = float3(first.gba);
                second = _CoeffTexture6.SampleLevel(sampler_CoeffTexture6, uv, 0);
                color[8] = second.rgb;
                float3 irradiance = A0*Y00*color[0] + A1*Y1_1*color[3] + A1*Y10*color[2] + A1*Y11*color[1]+ A2*Y2_2*color[6] + A2*Y2_1*color[5]  + A2*Y20*color[7]+ A2*Y21*color[4] + A2*Y22*color[8];
                return irradiance / 9.0;
            }

            float3 GetSHColor(float3 color[9], float3 worldNormal)
            {
                const float A0 = 3.1415927;
				const float A1 = 2.094395;
				const float A2 = 0.785398;
                GETCOEFF(worldNormal);
                float3 irradiance = A0*Y00*color[0] + A1*Y1_1*color[3] + A1*Y10*color[2] + A1*Y11*color[1]+ A2*Y2_2*color[6] + A2*Y2_1*color[5]  + A2*Y20*color[7]+ A2*Y21*color[4] + A2*Y22*color[8];
                return irradiance;
            }


            SHColor GetSHFromTex(float3 uv)
            {
                SHColor sh;
                float4 first = _CoeffTexture0.SampleLevel(sampler_CoeffTexture0, uv, 0);
                float4 second = _CoeffTexture1.SampleLevel(sampler_CoeffTexture1, uv, 0);
                sh.c[0] = first.rgb;
                sh.c[1] = float3(first.a, second.rg);
                first = _CoeffTexture2.SampleLevel(sampler_CoeffTexture2, uv, 0);
                sh.c[2] = float3(second.ba, first.r);
                sh.c[3] = float3(first.gba);
                first = _CoeffTexture3.SampleLevel(sampler_CoeffTexture3, uv, 0);
                second = _CoeffTexture4.SampleLevel(sampler_CoeffTexture4, uv, 0);
                sh.c[4] = first.rgb;
                sh.c[5] = float3(first.a, second.rg);
                first = _CoeffTexture5.SampleLevel(sampler_CoeffTexture5, uv, 0);
                sh.c[6] = float3(second.ba, first.r);
                sh.c[7] = float3(first.gba);
                second = _CoeffTexture6.SampleLevel(sampler_CoeffTexture6, uv, 0);
                sh.c[8] = second.rgb;
                return sh;
            }

           
#endif