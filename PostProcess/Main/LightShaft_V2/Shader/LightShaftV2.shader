Shader "TK/PostFX/LightShaftV2"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _NoiseTex("NoiseTex", 2D) = "black" {}
    }

     SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        LOD 100

        HLSLPROGRAM

        float curve(float src, float factor) 
        {
            return src - (src - src * src) * -factor;
        }
        float2 curve(float2 src, float factor) 
        {
            return src - (src - src * src) * -factor;
        }
        float3 curve(float3 src, float factor) 
        {
            return src - (src - src * src) * -factor;
        }
        float4 curve(float4 src, float factor) 
        {
            return src - (src - src * src) * -factor;
        }

        ENDHLSL

        Pass // 0 raymarch
        {
            Name "RayMarch"

            HLSLPROGRAM

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma shader_feature _RAYLEIGH
            //#pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4x4 _InvVP;
            int _SampleCount;
            half _MaxRayDistance;
            half _g;
            half _ExtinctionMie;
            half _ExtinctionRayleigh;
            half _FinalScale;
            half _NoiseScale;
            half _NoiseIntensity;

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_point_repeat_noise);
            float4 _NoiseTex_ST;

            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionNDC : TEXCOORD0;
                float4 positionCS  : SV_POSITION;
            };

            half3 TransformPositionNDCToWorld(float2 positionNDC)
            {
                float depth = SampleSceneDepth(positionNDC);
                float4 ndc = float4(positionNDC,1.0,1.0) * 2 - 1;
                half IsGL = step(UNITY_NEAR_CLIP_VALUE,0);
                ndc.z = depth * (IsGL + 1) - IsGL; //GL_NDC -1-1 DX_NDC 1-0

                float eyeDepth = LinearEyeDepth(depth,_ZBufferParams);
                half3 positionWS = mul(_InvVP ,ndc.rgb * eyeDepth);
                return positionWS;
            }

            half MiePhase(half cos, half g)
            {
                half g2 = g * g;
                return (1 - g2) / pow((12.56 * (1 + g2 - 2 * g * cos)),1.5);
            }

            half RayleighPhase(half cos)
            {
                return 0.05968 * (1.0 + cos * cos);
            }

            half AdditionalLightRealtimeShadowVolumetric(int lightIndex, float3 positionWS, half3 lightDirection)
            {
                half4 shadowParams = GetAdditionalLightShadowParams(lightIndex);
                int shadowSliceIndex = shadowParams.w;
                half isPointLight = shadowParams.z;
                UNITY_BRANCH
                if (isPointLight)
                {
                    return 0.0;
                }

                ShadowSamplingData shadowSamplingData = GetAdditionalLightShadowSamplingData();

                UNITY_BRANCH
                if (shadowSliceIndex < 0)
                    return 1.0;

                float4 shadowCoord = mul(_AdditionalLightsWorldToShadow[shadowSliceIndex], float4(positionWS, 1.0));

                return SampleShadowmap(TEXTURE2D_ARGS(_AdditionalLightsShadowmapTexture, sampler_AdditionalLightsShadowmapTexture), shadowCoord, shadowSamplingData, shadowParams, true);
            }

            half RayMarchShadowAttention(float2 positionNDC)
            {
                Light mainLight = GetMainLight();
                half3 cameraPosWS = GetCameraPositionWS();
                half3 positionWS = TransformPositionNDCToWorld(positionNDC);

                half3 rayDir = positionWS - cameraPosWS;
                half3 rayDirLength = length(rayDir);
                rayDir /= rayDirLength;
                half rayLength = min(_MaxRayDistance,rayDirLength);
                half stepSize = rayLength / _SampleCount;
                half cos = dot(mainLight.direction,-rayDir);
                float2 noiseUV = TRANSFORM_TEX(float2(positionNDC.x,positionNDC.y * _ScreenParams.y / _ScreenParams.x), _NoiseTex);
                half noise = SAMPLE_TEXTURE2D(_NoiseTex,sampler_point_repeat_noise,noiseUV * _NoiseScale).r * _NoiseIntensity;
                noise = 1.0 - noise;
                half3 rayEndPosition = cameraPosWS + rayDir * stepSize * noise;

                half extinction;
                half extinctionCoef;
                half phase;
#ifndef _RAYLEIGH
                extinction = rayLength * _ExtinctionMie;
                extinctionCoef = _ExtinctionMie;
                phase = MiePhase(cos,_g);
#else
                extinction = rayLength * _ExtinctionRayleigh;
                extinctionCoef = _ExtinctionRayleigh;
                phase = RayleighPhase(cos);
#endif
                half FinalAtten = 0;
                uint pixelLightCount = int(_AdditionalLightsCount.x);

                [loop]
                for (int i = 0; i < _SampleCount; ++i)
                {
                    half atten = MainLightRealtimeShadow(TransformWorldToShadowCoord(rayEndPosition));
                    extinction += extinctionCoef * stepSize;

                    for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex) {
                        Light light = GetAdditionalLight(lightIndex, rayEndPosition);
                        atten += AdditionalLightRealtimeShadowVolumetric(lightIndex, rayEndPosition, light.direction) * light.distanceAttenuation;
                    }

                    FinalAtten += atten * exp(-extinction);
                    rayEndPosition += rayDir * stepSize * noise;
                }

                FinalAtten *= phase * stepSize * _FinalScale;
                return FinalAtten;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionNDC = vertexInput.positionNDC;
                return output;
            }

            // The fragment shader definition.
            half4 frag(Varyings input) : SV_Target
            {
                return RayMarchShadowAttention(input.positionNDC.xy);
            }
            ENDHLSL
        }

        Pass // 1 blur1
        {
            Name "Blur1"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #pragma vertex vert
            #pragma fragment frag

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            half _blurOffset;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;

                output.uv = input.texcoord;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                int i = _blurOffset;

                half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).r;
                mask += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, i) * _MainTex_TexelSize.xy).r;
                mask += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, i * 2) * _MainTex_TexelSize.xy).r;
                mask += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, -i * 2) * _MainTex_TexelSize.xy).r;
                mask += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, -i) * _MainTex_TexelSize.xy).r;

                return mask / 5.0;
            }
            ENDHLSL
        }

        Pass // 2 blur 2
        {
            Name "Blur2"
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #pragma vertex vert
            #pragma fragment frag

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            half _blurOffset;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;

                output.uv = input.texcoord;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                int i = _blurOffset;

                half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).r;
                mask += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(i, 0) * _MainTex_TexelSize.xy).r;
                mask += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(i * 2, 0) * _MainTex_TexelSize.xy).r;
                mask += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(-i, 0) * _MainTex_TexelSize.xy).r;
                mask += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(-i * 2, 0) * _MainTex_TexelSize.xy).r;

                return mask / 5.0;
            }
            ENDHLSL
        }
    
        Pass // 3 Blit Additive
        {
            Name "BlitAdd"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #pragma vertex vert
            #pragma fragment frag

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_VolumetricLightTexture);
            SAMPLER(sampler_VolumetricLightTexture);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;

                output.uv = input.texcoord;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {

                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half mask = SAMPLE_TEXTURE2D(_VolumetricLightTexture, sampler_VolumetricLightTexture, input.uv).r;
                mask = 3 * mask * mask - 2 * mask * mask * mask; //smoothstep
                color.rgb += mask * GetMainLight().color;

                return color;
            }
            ENDHLSL
        }
    }
}
