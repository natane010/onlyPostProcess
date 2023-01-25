#ifndef COMMON_INCLUDE
#define COMMON_INCLUDE

// Set to 1 to support orthographic mode
#define ORTHO 0

// Set to 1 to enable Sobel outline
#define OUTLINE_SOBEL 0


TEXTURE2D_X_FLOAT(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);

float HighPrecisionSampleSceneDepth(float2 uvStereo)
{
    return SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uvStereo).r;
}


#if ORTHO
    #if UNITY_REVERSED_Z
        #define GET_DEPTH_01(x) (1.0-x)
        #define GET_DEPTH_EYE(x) ((1.0-x) * _ProjectionParams.z)

        #define GET_SCENE_DEPTH_01(x) (1.0 - HighPrecisionSampleSceneDepth(x))
        #define GET_SCENE_DEPTH_EYE(x) ((1.0 - HighPrecisionSampleSceneDepth(x)) * _ProjectionParams.z)

        #define GET_CUSTOM_DEPTH_01(s, x) (1.0 - SAMPLE_TEXTURE2D_X(s, sampler_PointClamp, x).r)
    #else
        #define GET_DEPTH_01(x) (x)
        #define GET_DEPTH_EYE(x) (x * _ProjectionParams.z)

        #define GET_SCENE_DEPTH_01(x) HighPrecisionSampleSceneDepth(x)
        #define GET_SCENE_DEPTH_EYE(x) (HighPrecisionSampleSceneDepth(x) * _ProjectionParams.z)

        #define GET_CUSTOM_DEPTH_01(s, x) SAMPLE_TEXTURE2D_X(s, sampler_PointClamp, x).r
    #endif
#else
    #define GET_DEPTH_01(x) Linear01Depth(x, _ZBufferParams)
    #define GET_DEPTH_EYE(x) LinearEyeDepth(x, _ZBufferParams)

    #define GET_SCENE_DEPTH_01(x) Linear01Depth(HighPrecisionSampleSceneDepth(x), _ZBufferParams)
    #define GET_SCENE_DEPTH_EYE(x) LinearEyeDepth(HighPrecisionSampleSceneDepth(x), _ZBufferParams)

    #define GET_CUSTOM_DEPTH_01(s, x) Linear01Depth(SAMPLE_TEXTURE2D_X(s, sampler_PointClamp, x).r, _ZBufferParams)
#endif


// Base for COC - required for Android
#define COC_BASE 128


// Optimization for SSPR
#define uvN uv1
#define uvE uv2
#define uvW uv3
#define uvS uv4


#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED) || defined(SINGLE_PASS_STEREO)
    #define VERTEX_CROSS_UV_DATA
    #define VERTEX_OUTPUT_CROSS_UV(o)
    #define VERTEX_OUTPUT_GAUSSIAN_UV(o)

    #define FRAG_SETUP_CROSS_UV(i) float3 uvInc = float3(_MainTex_TexelSize.x, _MainTex_TexelSize.y, 0); float2 uvN = i.uv + uvInc.zy; float2 uvE = i.uv + uvInc.xz; float2 uvW = i.uv - uvInc.xz; float2 uvS = i.uv - uvInc.zy;

    #if defined(BLUR_HORIZ)
        #define FRAG_SETUP_GAUSSIAN_UV(i) float2 inc = float2(_MainTex_TexelSize.x * 1.3846153846 * _BlurScale, 0); float2 uv1 = i.uv - inc; float2 uv2 = i.uv + inc; float2 inc2 = float2(_MainTex_TexelSize.x * 3.2307692308 * _BlurScale, 0); float2 uv3 = i.uv - inc2; float2 uv4 = i.uv + inc2;
    #else
        #define FRAG_SETUP_GAUSSIAN_UV(i) float2 inc = float2(0, _MainTex_TexelSize.y * 1.3846153846 * _BlurScale); float2 uv1 = i.uv - inc; float2 uv2 = i.uv + inc; float2 inc2 = float2(0, _MainTex_TexelSize.y * 3.2307692308 * _BlurScale); float2 uv3 = i.uv - inc2; float2 uv4 = i.uv + inc2;
    #endif

#else
    #define VERTEX_CROSS_UV_DATA float2 uvN : TEXCOORD1; float2 uvW: TEXCOORD2; float2 uvE: TEXCOORD3; float2 uvS: TEXCOORD4;

    #define VERTEX_OUTPUT_CROSS_UV(o) float3 uvInc = float3(_MainTex_TexelSize.x, _MainTex_TexelSize.y, 0); o.uvN = o.uv + uvInc.zy; o.uvE = o.uv + uvInc.xz; o.uvW = o.uv - uvInc.xz; o.uvS = o.uv - uvInc.zy;
    #define FRAG_SETUP_CROSS_UV(i) float2 uv1 = i.uv1; float2 uv2 = i.uv2; float2 uv3 = i.uv3; float2 uv4 = i.uv4;

    #if defined(BLUR_HORIZ)
        #define VERTEX_OUTPUT_GAUSSIAN_UV(o) float2 inc = float2(_MainTex_TexelSize.x * 1.3846153846 * _BlurScale, 0); o.uv1 = o.uv - inc; o.uv2 = o.uv + inc; float2 inc2 = float2(_MainTex_TexelSize.x * 3.2307692308 * _BlurScale, 0); o.uv3 = o.uv - inc2; o.uv4 = o.uv + inc2;
    #else
        #define VERTEX_OUTPUT_GAUSSIAN_UV(o) float2 inc = float2(0, _MainTex_TexelSize.y * 1.3846153846 * _BlurScale); o.uv1 = o.uv - inc; o.uv2 = o.uv + inc; float2 inc2 = float2(0, _MainTex_TexelSize.y * 3.2307692308 * _BlurScale); o.uv3 = o.uv - inc2; o.uv4 = o.uv + inc2;
    #endif
    #define FRAG_SETUP_GAUSSIAN_UV(i) float2 uv1 = i.uv1; float2 uv2 = i.uv2; float2 uv3 = i.uv3; float2 uv4 = i.uv4;

#endif


#if TURBO
    #define GAMMA_TO_LINEAR(x) FastSRGBToLinear(x)
    #define LINEAR_TO_GAMMA(x) FastLinearToSRGB(x)
#else
    #define GAMMA_TO_LINEAR(x) SRGBToLinear(x)
    #define LINEAR_TO_GAMMA(x) LinearToSRGB(x)
#endif


// Common functions

Varyings VertOS(Attributes input) {
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = input.positionOS;
    output.positionCS.y *= _ProjectionParams.x;
    output.uv = input.uv.xy;
    return output;
}


inline float getLuma(float3 rgb) { 
	const float3 lum = float3(0.299, 0.587, 0.114);
	return dot(rgb, lum);
}


float3 getNormal(float depth, float depth1, float depth2, float2 offset1, float2 offset2) {
    float3 p1 = float3(offset1, depth1 - depth);
    float3 p2 = float3(offset2, depth2 - depth);
    float3 normal = cross(p1, p2);
    return normalize(normal);
}

#endif // COMMON_INCLUDE