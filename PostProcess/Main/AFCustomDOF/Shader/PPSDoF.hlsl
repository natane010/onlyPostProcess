#ifndef PPSDOF_FX
#define PPSDOF_FX

    #include "Common.hlsl"
    
    TEXTURE2D_X(_DoFTransparentDepth);
    //TEXTURE2D_X(_DofExclusionTexture);    
    TEXTURE2D_X(_MainTex);

    float4    _MainTex_TexelSize;
    float4    _MainTex_ST;
    float4    _BokehData;
    float4    _BokehData2;
    float3    _BokehData3;
    float     _BlurScale;

    struct VaryingsDoFCross {
	    float4 positionCS : SV_POSITION;
        float2 uv: TEXCOORD0;
        VERTEX_CROSS_UV_DATA
        UNITY_VERTEX_OUTPUT_STEREO
    };

    
    float getCoc(Varyings i) {
        #if DOF_TRANSPARENT
            float depthTex = GET_CUSTOM_DEPTH_01(_DoFTransparentDepth, i.uv);
            //float exclusionDepth = DecodeFloatRGBA(SAMPLE_TEXTURE2D_LOD(_DofExclusionTexture, sampler_PointClamp, i.uvNonStereo, 0));
            float depth  = GET_SCENE_DEPTH_01(i.uv);
            depth = min(depth, depthTex);
            //if (exclusionDepth < depth) return 0;
            depth *= _ProjectionParams.z;
        #else
            float depth  = GET_SCENE_DEPTH_EYE(i.uv);
        #endif
        if (depth>_BokehData3.y) return 0;
        float xd     = abs(depth - _BokehData.x) - _BokehData2.x * (depth < _BokehData.x);
        return 0.5 * _BokehData.y * xd/depth;    // radius of CoC
    }
                
    float4 FragCoC (Varyings i) : SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        i.uv = UnityStereoTransformScreenSpaceTex(i.uv);

        float4 pixel  = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, i.uv);
        pixel         = clamp(pixel, 0.0.xxxx, _BokehData3.xxxx);
        #if UNITY_COLORSPACE_GAMMA
            pixel.rgb     = GAMMA_TO_LINEAR(pixel.rgb);
        #endif
        float coc = getCoc(i) / COC_BASE;
        return float4(pixel.rgb, coc);
    }    
    
    float4 FragCoCDebug (Varyings i) : SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        i.uv = UnityStereoTransformScreenSpaceTex(i.uv);

        float4 pixel  = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, i.uv);
        float  CoC   = min(getCoc(i) / 16.0, 1.0);
        return float4(CoC.xxx, 1.0);
    }
    
    float4 FragDoFDebugTransparent (Varyings i) : SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        i.uv = UnityStereoTransformScreenSpaceTex(i.uv);

        float depthTex = GET_CUSTOM_DEPTH_01(_DoFTransparentDepth, i.uv);
        return float4(depthTex.xxx, 1.0);
    }

    float4 FragBlur (Varyings i): SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        i.uv = UnityStereoTransformScreenSpaceTex(i.uv);

        float4 sum     = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, i.uv );
        float  samples = ceil(sum.a * COC_BASE);

        float4 dir     = float4(_BokehData.zw * _MainTex_TexelSize.xy, 0, 0);
               dir    *= max(1.0, samples / _BokehData2.y);
        float  jitter  = dot(float2(2.4084507, 3.2535211), i.uv * _MainTex_TexelSize.zw);
        float2 disp0   = dir.xy * (frac(jitter) + 0.5);
        float4 disp1   = float4(i.uv + disp0, 0, 0);
        float4 disp2   = float4(i.uv - disp0, 0, 0);
        float  w       = 1.0;

        const int sampleCount = (int)min(_BokehData2.y, samples);
        UNITY_UNROLL
        for (int k=1;k<16;k++) {
            if (k<sampleCount) {

                float4 pixel1       = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_LinearClamp, disp1.xy, 0);
                float  bt1         = saturate(pixel1.a * COC_BASE - k);
                       pixel1.rgb += _BokehData2.www * max(pixel1.rgb - _BokehData2.zzz, 0.0.xxx);
                       sum        += pixel1 * bt1;
                       w           += bt1;
                       disp1      += dir;

                       float4 pixel2 = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_LinearClamp, disp2.xy, 0);
                       float  bt2  = saturate(pixel2.a * COC_BASE - k);
                       pixel2.rgb += _BokehData2.www * max(pixel2.rgb - _BokehData2.zzz, 0.0.xxx);
                       sum        += pixel2 * bt2;
                       w          += bt2;
                       disp2      -= dir;
            }
        }
        return sum / w;
    }

    float4 FragBlurNoBokeh (Varyings i): SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        i.uv = UnityStereoTransformScreenSpaceTex(i.uv);

        float4 sum     = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, i.uv );
        float samples  = ceil(sum.a * COC_BASE);
        float4 dir     = float4(_BokehData.zw * _MainTex_TexelSize.xy, 0, 0);
               dir    *= max(1.0, samples / _BokehData2.y);
        float  jitter  = dot(float2(2.4084507, 3.2535211), i.uv * _MainTex_TexelSize.zw);
        float2 disp0   = dir.xy * (frac(jitter) + 0.5);
        float4 disp1   = float4(i.uv + disp0, 0, 0);
        float4 disp2   = float4(i.uv - disp0, 0, 0);
        float  w       = 1.0;

        const int sampleCount = (int)min(_BokehData2.y, samples);
        UNITY_UNROLL
        for (int k=1;k<16;k++) {
            if (k<sampleCount) {
                    float4 pixel1      = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_LinearClamp, disp1.xy, 0);
                float  bt1         = saturate(pixel1.a * COC_BASE - k);                
                       sum        += bt1 * pixel1;
                       w           += bt1;
                       disp1      += dir;
                    float4 pixel2      = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_LinearClamp, disp2.xy, 0);
                float  bt2  = saturate(pixel2.a * COC_BASE - k);                
                       sum        += bt2 * pixel2;
                       w          += bt2;
                       disp2      -= dir;
            }
        }
        return sum / w;
    }

    VaryingsDoFCross VertBlur(Attributes input) {
        VaryingsDoFCross output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        output.positionCS = input.positionOS;
        output.positionCS.y *= _ProjectionParams.x;
        output.uv = input.uv;

        VERTEX_OUTPUT_GAUSSIAN_UV(output);

        return output;
    }


   float4 FragBlurCoC (VaryingsDoFCross i): SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        i.uv = UnityStereoTransformScreenSpaceTex(i.uv);
        FRAG_SETUP_GAUSSIAN_UV(i)

        float depth   = GET_SCENE_DEPTH_EYE(i.uv);
        float depth1   = GET_SCENE_DEPTH_EYE(uv1);
        float depth2   = GET_SCENE_DEPTH_EYE(uv2);
        float depth3   = GET_SCENE_DEPTH_EYE(uv3);
        float depth4   = GET_SCENE_DEPTH_EYE(uv4);

        const float f = 10;
        float w1      = saturate((depth - depth1)/f) * 0.3162162162; 
        float w2      = saturate((depth - depth2)/f) * 0.3162162162; 
        float w3      = saturate((depth - depth3)/f) * 0.0702702703; 
        float w4      = saturate((depth - depth4)/f) * 0.0702702703; 

        float coc1    = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv1).a;
        float coc2    = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv2).a;
        float coc3    = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv3).a;
        float coc4    = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv4).a;

        float w0      = 0.2270270270;

        half4 pixel = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, i.uv);

        float coc     = (pixel.a * w0 + coc1 * w1 + coc2 * w2 + coc3 * w3 + coc4 * w4) / (w0 + w1 + w2 + w3 + w4);
        pixel.a = coc;
        return pixel;
    }   

               
    float4 FragThreshold (Varyings input) : SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        input.uv = UnityStereoTransformScreenSpaceTex(input.uv);
        float2 uv = input.uv;
        float4 pixel  = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv);
        pixel         = clamp(pixel, 0.0.xxxx, _BokehData3.xxxx);
        #if UNITY_COLORSPACE_GAMMA
            pixel.rgb     = GAMMA_TO_LINEAR(pixel.rgb);
        #endif
        pixel.rgb     = _BokehData2.www * max(pixel.rgb - _BokehData2.zzz, 0.0.xxx);
        float coc = getCoc(input) / COC_BASE;
        return float4(pixel.rgb, coc);
    }    

    float4 FragCopyBokeh(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        input.uv = UnityStereoTransformScreenSpaceTex(input.uv);
        float4 bokeh = SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, input.uv);
        return bokeh;
    }

  float4 FragBlurSeparateBokeh (Varyings input): SV_Target {

        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        input.uv = UnityStereoTransformScreenSpaceTex(input.uv);

        float2 uv = input.uv;

        float4 sum     = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv);
        float samples  = ceil(sum.a * COC_BASE);
        float4 dir     = float4(_BokehData.zw * _MainTex_TexelSize.xy, 0, 0);
               dir    *= max(1.0, samples / _BokehData2.y);
        float  jitter  = dot(float2(2.4084507, 3.2535211), uv * _MainTex_TexelSize.zw);
        float2 disp0   = dir.xy * (frac(jitter) + 0.5);
        float4 disp1   = float4(input.uv + disp0, 0, 0);
        float4 disp2   = float4(input.uv - disp0, 0, 0);

        const int sampleCount = (int)min(_BokehData2.y, samples);
        UNITY_UNROLL
        for (int k=1;k<16;k++) {
            if (k<sampleCount) {
                    float4 pixel1  = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_LinearClamp, disp1.xy, 0);
                float  bt1         = saturate(pixel1.a * COC_BASE - k);                
                       sum        = max(sum, bt1 * pixel1);
                       disp1      += dir;
                    float4 pixel2      = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_LinearClamp, disp2.xy, 0);
                float  bt2  = saturate(pixel2.a * COC_BASE - k);                
                       sum        = max(sum, bt2 * pixel2);
                       disp2      -= dir;
            }
        }
        return sum;
    }


#endif // PPSDOF_FX


