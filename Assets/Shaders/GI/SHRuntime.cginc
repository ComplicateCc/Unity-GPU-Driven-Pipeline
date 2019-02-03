#ifndef SH_RUNTIME
#define SH_RUNTIME

            Texture3D<float4> _CoeffTexture0; SamplerState sampler_CoeffTexture0;
            Texture3D<float4> _CoeffTexture1; SamplerState sampler_CoeffTexture1;
            Texture3D<float4> _CoeffTexture2; SamplerState sampler_CoeffTexture2;
            Texture3D<float4> _CoeffTexture3; SamplerState sampler_CoeffTexture3;
            Texture3D<float4> _CoeffTexture4; SamplerState sampler_CoeffTexture4;
            Texture3D<float4> _CoeffTexture5; SamplerState sampler_CoeffTexture5;
            Texture3D<float4> _CoeffTexture6; SamplerState sampler_CoeffTexture6;
            float3 _LeftDownBack;
            float3 _SHSize;
            float3 GetSHColor(float3 worldNormal, float3 worldPos)
            {
                #if ENABLESH
                float3 color[9];
                float4 first, second;
                float3 uv = (worldPos - _LeftDownBack) / (_SHSize);
                first = _CoeffTexture0.Sample(sampler_CoeffTexture0, uv);
                second = _CoeffTexture1.Sample(sampler_CoeffTexture1, uv);
                color[0] = first.rgb;
                color[1] = float3(first.a, second.rg);
                first = _CoeffTexture2.Sample(sampler_CoeffTexture2, uv);
                color[2] = float3(second.ba, first.r);
                color[3] = float3(first.gba);
                first = _CoeffTexture3.Sample(sampler_CoeffTexture3, uv);
                second = _CoeffTexture4.Sample(sampler_CoeffTexture4, uv);
                color[4] = first.rgb;
                color[5] = float3(first.a, second.rg);
                first = _CoeffTexture5.Sample(sampler_CoeffTexture5, uv);
                color[6] = float3(second.ba, first.r);
                color[7] = float3(first.gba);
                second = _CoeffTexture6.Sample(sampler_CoeffTexture6, uv);
                color[8] = second.rgb;
                SH9 sh = SHCosineLobe(worldNormal);
                float3 finalColor = 0;
                [unroll]
                for(int i = 0; i < 9; ++i)
                {
                    finalColor += sh.c[i] * color[i];
                }
                finalColor /= 9;
                return finalColor;
                #endif
                return 0;
            }

            float3 GetVolumetricColor(float3 worldPos)
            {
                
                #if ENABLESH
                float3 color[9];
                float4 first, second;
                float3 uv = (worldPos - _LeftDownBack) / (_SHSize);
                first = _CoeffTexture0.Sample(sampler_CoeffTexture0, uv);
                second = _CoeffTexture1.Sample(sampler_CoeffTexture1, uv);
                color[0] = first.rgb;
                color[1] = float3(first.a, second.rg);
                first = _CoeffTexture2.Sample(sampler_CoeffTexture2, uv);
                color[2] = float3(second.ba, first.r);
                color[3] = float3(first.gba);
                first = _CoeffTexture3.Sample(sampler_CoeffTexture3, uv);
                second = _CoeffTexture4.Sample(sampler_CoeffTexture4, uv);
                color[4] = first.rgb;
                color[5] = float3(first.a, second.rg);
                first = _CoeffTexture5.Sample(sampler_CoeffTexture5, uv);
                color[6] = float3(second.ba, first.r);
                color[7] = float3(first.gba);
                second = _CoeffTexture6.Sample(sampler_CoeffTexture6, uv);
                color[8] = second.rgb;
                SH9 sh = SHCosineLobe(float3(1,0,0));
                float3 finalColor = 0;
                int i;
                [unroll]
                for(i = 0; i < 9; ++i)
                {
                    finalColor += sh.c[i] * color[i];
                }
                sh = SHCosineLobe(float3(-1,0,0));
                [unroll]
                for(i = 0; i < 9; ++i)
                {
                    finalColor += sh.c[i] * color[i];
                }
                sh = SHCosineLobe(float3(0,1,0));
                [unroll]
                for(i = 0; i < 9; ++i)
                {
                    finalColor += sh.c[i] * color[i];
                }
                sh = SHCosineLobe(float3(0,-1,0));
                [unroll]
                for(i = 0; i < 9; ++i)
                {
                    finalColor += sh.c[i] * color[i];
                }
                sh = SHCosineLobe(float3(0, 0, 1));
                [unroll]
                for(i = 0; i < 9; ++i)
                {
                    finalColor += sh.c[i] * color[i];
                }
                sh = SHCosineLobe(float3(0, 0, -1));
                [unroll]
                for(i = 0; i < 9; ++i)
                {
                    finalColor += sh.c[i] * color[i];
                }
                finalColor /= 54;
                return finalColor;
                #endif
                return 0;
            }
#endif