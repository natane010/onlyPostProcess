Shader "TK/PostFX/SSPR"
{
    Properties
    {
        [MainColor] _BaseColor("BaseColor", Color) = (1,1,1,1)
        [MainTexture] _BaseMap("BaseMap", 2D) = "black" {}
        [Normal] _NormalMap("NormalMap", 2D) = "bump" {}

        _Roughness("_Roughness", range(0,1)) = 0.25
        [NoScaleOffset]_SSPR_UVNoiseTex("_SSPR_UVNoiseTex", 2D) = "gray" {}
        _SSPR_NoiseIntensity("_SSPR_NoiseIntensity", range(-0.2,0.2)) = 0.0

        _UV_MoveSpeed("_UV_MoveSpeed (xy only)(for things like water flow)", Vector) = (0,0,0,0)

        [NoScaleOffset]_ReflectionAreaTex("_ReflectionArea", 2D) = "white" {}
    }

        SubShader
    {
        Pass
        {
            Tags { "LightMode" = "MobileSSPR" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MobileSSPR

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
            #define UNITY_CALC_FOG_FACTOR(outpos) float unityFogFactor = ComputeFogFactor((outpos).z)
            #define UNITY_FOG_COORDS(idx) float1 fogCoord : TEXCOORD##idx;
            #define UNITY_TRANSFER_FOG(o,outpos) o.fogCoord.x = ComputeFogFactor((outpos).z)
            #define UNITY_APPLY_FOG_COLOR(coord,col,fogCol) col.rgb = MixFogColor(col.rgb,(fogCol).rgb,(coord).x)
#else
            #define UNITY_FOG_COORDS(idx)
            #define UNITY_TRANSFER_FOG(o,outpos)
            #define UNITY_APPLY_FOG_COLOR(coord,col,fogCol)
#endif

            TEXTURE2D(_MobileSSPR_ColorRT);
            sampler LinearClampSampler;

            struct ReflectionInput
            {
                float3 posWS;
                float4 screenPos;
                float2 screenSpaceNoise;
                float roughness;
                float SSPR_Usage;
            };
            half3 GetResultReflection(ReflectionInput data)
            {
                half3 viewWS = (data.posWS - _WorldSpaceCameraPos);
                viewWS = normalize(viewWS);

                half3 reflectDirWS = viewWS * half3(1, -1, 1);

                half3 reflectionProbeResult = GlossyEnvironmentReflection(reflectDirWS, data.roughness, 1);
                half4 SSPRResult = 0;
#if _MobileSSPR    
                half2 screenUV = data.screenPos.xy / data.screenPos.w;
                SSPRResult = SAMPLE_TEXTURE2D(_MobileSSPR_ColorRT, LinearClampSampler, screenUV + data.screenSpaceNoise); //use LinearClampSampler to make it blurry
#endif

                half3 finalReflection = lerp(reflectionProbeResult, SSPRResult.rgb, SSPRResult.a * data.SSPR_Usage);//combine reflection probe and SSPR

                return finalReflection;
            }

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 screenPos    : TEXCOORD4;
                float3 posWS        : TEXCOORD2;
                float4 positionHCS  : SV_POSITION;
#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                float4 shadowCoord  : TEXCOORD3;
#endif // defined(Main_Light_CALCLATE_SHADOW)
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_OUTPUT_STEREO

            };

            //textures
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_SSPR_UVNoiseTex);
            SAMPLER(sampler_SSPR_UVNoiseTex);
            TEXTURE2D(_ReflectionAreaTex);
            SAMPLER(sampler_ReflectionAreaTex);

            //cbuffer
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _NormalMap_ST;
            half4 _BaseColor;
            half _SSPR_NoiseIntensity;
            float2 _UV_MoveSpeed;
            half _Roughness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(worldPos);
    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                OUT.shadowCoord = TransformWorldToShadowCoord(worldPos);
    #else
                OUT.shadowCoord = float4(worldPos, 1);
    #endif
#else
                OUT.positionHCS = float4(IN.positionOS.x, IN.positionOS.y, -1, -1);
#endif
                //OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap) + _Time.y * _UV_MoveSpeed;
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                OUT.posWS = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor.rgb;

                float2 noise = SAMPLE_TEXTURE2D(_SSPR_UVNoiseTex,sampler_SSPR_UVNoiseTex, IN.uv);
                noise = noise * 2 - 1;
                noise.y = -abs(noise);
                noise.x *= 0.25;
                noise *= _SSPR_NoiseIntensity;

                ReflectionInput reflectionData;
                reflectionData.posWS = IN.posWS;
                reflectionData.screenPos = IN.screenPos;
                reflectionData.screenSpaceNoise = noise;
                reflectionData.roughness = _Roughness;
                reflectionData.SSPR_Usage = _BaseColor.a;

                half3 resultReflection = GetResultReflection(reflectionData);

                half reflectionArea = SAMPLE_TEXTURE2D(_ReflectionAreaTex,sampler_ReflectionAreaTex, IN.uv);

                half3 finalRGB = lerp(baseColor / 2,resultReflection,reflectionArea) / 2 + baseColor / 2;

#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                float4 shadowCoord = IN.shadowCoord;
    #if !defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                shadowCoord = TransformWorldToShadowCoord(shadowCoord.xyz);
    #endif
                half shadow = MainLightRealtimeShadow(shadowCoord);
                //half4 col = lerp(1.0f, shadow, half4(finalRGB, 1));
                half4 col = half4(finalRGB,1) * shadow;
                return col;
#else
                return half4(finalRGB, 1);
#endif

                return half4(finalRGB,1);
            }

            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

                // -------------------------------------
                // Material Keywords
                #pragma shader_feature_local_fragment _ALPHATEST_ON
                #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

                //--------------------------------------
                // GPU Instancing
                #pragma multi_compile_instancing
                #pragma multi_compile _ DOTS_INSTANCING_ON

                #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
                ENDHLSL
            }
            Pass
            {
                Name "DepthNormals"
                Tags{"LightMode" = "DepthNormals"}

                ZWrite On
                Cull[_Cull]

                HLSLPROGRAM
                #pragma exclude_renderers gles gles3 glcore
                #pragma target 4.5

                #pragma vertex DepthNormalsVertex
                #pragma fragment DepthNormalsFragment

                // -------------------------------------
                // Material Keywords
                #pragma shader_feature_local _NORMALMAP
                #pragma shader_feature_local _PARALLAXMAP
                #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
                #pragma shader_feature_local_fragment _ALPHATEST_ON
                #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

                //--------------------------------------
                // GPU Instancing
                #pragma multi_compile_instancing
                #pragma multi_compile _ DOTS_INSTANCING_ON

                #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
                ENDHLSL
            }
    }
}
