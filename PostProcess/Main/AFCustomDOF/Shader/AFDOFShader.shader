Shader "TK/PostFX/AFDOFShader"
{
    Properties
    {
        _FlareTex("Flare Texture", 2D) = "white" {}
        _OverlayTex("Lens Dirt Texture", 2D) = "black" {}
        _Color("", Color) = (1,1,1)
        _BlueNoise("Blue Noise", 2D) = "black" {}
        _BokehData2("", Vector) = (1,1,1,1)
        _BokehData3("", Vector) = (1,1,1,1)
        _BlurMask("Blur Mask", 2D) = "white" {}
    }

    HLSLINCLUDE
    #pragma target 3.0
    ENDHLSL

    Subshader
    {

            Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
            LOD 100
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLINCLUDE
            #pragma target 3.0
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
            ENDHLSL

          Pass { // 0 Raw Copy (Point Filtering)
              HLSLPROGRAM
              #pragma vertex VertOS
              #pragma fragment FragCopy
              #include "Core.hlsl"
              ENDHLSL
          }

          Pass { // 1 Compare View
              HLSLPROGRAM
              #pragma vertex VertOS
              #pragma fragment FragCompare
              #include "Core.hlsl"
              ENDHLSL
          }

          Pass { // 2  Main  Pass (core)
              HLSLPROGRAM
              #pragma vertex PVert
              #pragma fragment Frag
              #pragma multi_compile_local __ TONEMAP_ACES
              #pragma multi_compile_local __ DIRT
              #pragma multi_compile_local __ DEPTH_OF_FIELD DOF_TRANSPARENT CHROMATIC_ABERRATION
              #pragma multi_compile_local __ COLOR_TWEAKS
              #pragma multi_compile_local __ TURBO
              #include "Core.hlsl"
              ENDHLSL
          }

          Pass { // 5 Blur horizontally
              HLSLPROGRAM
              #pragma vertex VertBlur
              #pragma fragment FragBlur
              #define BLUR_HORIZ
              #include "PPSLum.hlsl"
              ENDHLSL
          }

          Pass { // 6 Blur vertically
              HLSLPROGRAM
              #pragma vertex VertBlur
              #pragma fragment FragBlur
              #include "PPSLum.hlsl"
              ENDHLSL
          }


           Pass { // 11 Resample Anamorphic Flares
              HLSLPROGRAM
              #pragma vertex VertCross
              #pragma fragment FragResampleAF
              #define COMBINE_BLOOM
              #include "PPSLum.hlsl"
              ENDHLSL
          }

          Pass { // 12 Combine AF
              HLSLPROGRAM
              #pragma vertex VertOS
              #pragma fragment FragCombine
              #define COMBINE_BLOOM
              #include "PPSLum.hlsl"
              ENDHLSL
          }



         Pass { // 21 DoF CoC
              HLSLPROGRAM
              #pragma vertex VertOS
              #pragma fragment FragCoC
              #pragma fragmentoption ARB_precision_hint_fastest
              #pragma multi_compile_local __ DOF_TRANSPARENT
              #include "PPSDoF.hlsl"
              ENDHLSL
          }

          Pass { // 22 DoF CoC Debug
              HLSLPROGRAM
              #pragma vertex VertOS
              #pragma fragment FragCoCDebug
              #pragma fragmentoption ARB_precision_hint_fastest
              #pragma multi_compile_local __ DOF_TRANSPARENT
              #include "PPSDoF.hlsl"
              ENDHLSL
          }

          Pass { // 23 DoF Blur
              HLSLPROGRAM
              #pragma vertex VertOS
              #pragma fragment FragBlur
              #pragma fragmentoption ARB_precision_hint_fastest
              #include "PPSDoF.hlsl"
              ENDHLSL
          }

          Pass { // 24 DoF Blur wo/Bokeh
              HLSLPROGRAM
              #pragma vertex VertOS
              #pragma fragment FragBlurNoBokeh
              #pragma fragmentoption ARB_precision_hint_fastest
              #include "PPSDoF.hlsl"
              ENDHLSL
          }

          Pass { // 25 DoF Blur Horizontally
              HLSLPROGRAM
              #pragma vertex VertBlur
              #pragma fragment FragBlurCoC
              #pragma fragmentoption ARB_precision_hint_fastest
              #define BLUR_HORIZ
              #include "PPSDoF.hlsl"
              ENDHLSL
          }

          Pass { // 26 DoF Blur Vertically
              HLSLPROGRAM
              #pragma vertex VertBlur
              #pragma fragment FragBlurCoC
              #pragma fragmentoption ARB_precision_hint_fastest
              #include "PPSDoF.hlsl"
              ENDHLSL
          }

          Pass { // 27 Raw Copy (Bilinear Filtering)
              HLSLPROGRAM
              #pragma vertex VertOS
              #pragma fragment FragCopy
              #define USE_BILINEAR
              #include "Core.hlsl"
              ENDHLSL
          }


          Pass { // 29 DoF Debug Transparent
              HLSLPROGRAM
              #pragma vertex VertOS
              #pragma fragment FragDoFDebugTransparent
              #pragma fragmentoption ARB_precision_hint_fastest
              #pragma multi_compile_local __ DOF_TRANSPARENT
              #include "PPSDoF.hlsl"
              ENDHLSL
          }

          Pass { // 30 Chromatic Aberration Custom Pass
              HLSLPROGRAM
              #pragma vertex VertOS
              #pragma fragment fragChromaticAberration
              #pragma fragmentoption ARB_precision_hint_fastest
              #pragma multi_compile_local __ TURBO
              #define CHROMATIC_ABERRATION 1
              #include "CAberration.hlsl"
              ENDHLSL
          }


          Pass { // 35 Mask Blur
              HLSLPROGRAM
              #pragma vertex VertOS
              #pragma fragment FragCopyWithMask
              #include "Core.hlsl"
              ENDHLSL
          }


          Pass { // 36 - DoF Threshold for bokeh
              HLSLPROGRAM
              #pragma vertex VertOS
              #pragma fragment FragThreshold
              #include "PPSDoF.hlsl"
              ENDHLSL
          }

          Pass { // 37 - DoF Additive
              Blend One One
              HLSLPROGRAM
              #pragma vertex VertOS
              #pragma fragment FragCopyBokeh
              #include "PPSDoF.hlsl"
              ENDHLSL
          }

          Pass { // 38 - DoF Blur bokeh
              HLSLPROGRAM
              #pragma vertex VertOS
              #pragma fragment FragBlurSeparateBokeh
              #include "PPSDoF.hlsl"
              ENDHLSL
          }

    }
    FallBack Off
}
