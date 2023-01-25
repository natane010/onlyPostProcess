#ifndef UNITY_DECLARE_DEPTH_TEXTURE_INCLUDED
#define UNITY_DECLARE_DEPTH_TEXTURE_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D_X_FLOAT(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);

float SampleSceneDepth(float2 uv)
{
	return SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(uv)).r;
}

float LoadSceneDepth(uint2 uv)
{
	return LOAD_TEXTURE2D_X(_CameraDepthTexture, uv).r;
}

// Z buffer to linear 0..1 depth (0 at eye, 1 at far plane)
inline float Linear01Depth(float z)
{
	return 1.0 / (_ZBufferParams.x * z + _ZBufferParams.y);
}
// Z buffer to linear depthss
inline float LinearEyeDepth(float z)
{
	return 1.0 / (_ZBufferParams.z * z + _ZBufferParams.w);
}

#endif

#ifndef TK_POSTPROCESS_CORE_INCLUDED
#define TK_POSTPROCESS_CORE_INCLUDED
#pragma warning( disable:4005 )
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

shared float2 texelSize;
float2 DIRECTION;
shared float centerTapWeight;
shared float4 tapWeights, tapOffsets;
struct FullScreenTrianglePostProcessAttributes
{
	uint vertexID : SV_VertexID;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct PostProcessVaryings
{
	float4 positionCS : SV_POSITION;
	float2 texcoord   : TEXCOORD0;
	UNITY_VERTEX_OUTPUT_STEREO
};

struct VS_OUTPUT
{
	float4 positionCS : SV_POSITION;
	float2 centerTap : TEXCOORD0;
	float4 positiveTaps[2] : TEXCOORD1;
	float4 negativeTaps[2] : TEXCOORD3;
};

PostProcessVaryings FullScreenTrianglePostProcessVertexProgram(FullScreenTrianglePostProcessAttributes input)
{
	PostProcessVaryings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
	output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
	output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
	return output;
}


#endif

#pragma warning (disable : 3205) // conversion of larger type to smaller
#pragma warning (disable : 3568) // unknown pragma ignored
#pragma warning (disable : 3571) // "pow(f,e) will not work for negative f"; however in majority of our calls to pow we know f is not negative
#pragma warning (disable : 3206) // implicit truncation of vector type

#define UNITY_PI 3.14159265359f
#define prismEpsilon = 1e-4;

	// Mean of Rec. 709 & 601 luma coefficients
#define lumacoeff        float3(0.2558, 0.6511, 0.0931)

#if defined(SHADER_API_D3D11) || defined(UNITY_COMPILER_HLSLCC) || defined(SHADER_API_PSSL) || (defined(SHADER_TARGET_SURFACE_ANALYSIS) && !defined(SHADER_TARGET_SURFACE_ANALYSIS_MOJOSHADER))
#define UNITY_SEPARATE_TEXTURE_SAMPLER

// 2D textures
#define UNITY_DECLARE_TEX2D(tex) Texture2D tex; SamplerState sampler##tex
#define UNITY_DECLARE_TEX2D_NOSAMPLER(tex) Texture2D tex
#define UNITY_DECLARE_TEX2D_NOSAMPLER_INT(tex) Texture2D<int4> tex
#define UNITY_DECLARE_TEX2D_NOSAMPLER_UINT(tex) Texture2D<uint4> tex
#define UNITY_SAMPLE_TEX2D(tex,coord) tex.Sample (sampler##tex,coord)
#define UNITY_SAMPLE_TEX2D_LOD(tex,coord,lod) tex.SampleLevel (sampler##tex,coord, lod)
#define UNITY_SAMPLE_TEX2D_SAMPLER(tex,samplertex,coord) tex.Sample (sampler##samplertex,coord)
#define UNITY_SAMPLE_TEX2D_SAMPLER_LOD(tex, samplertex, coord, lod) tex.SampleLevel (sampler##samplertex, coord, lod)

#if defined(UNITY_COMPILER_HLSLCC) && (!defined(SHADER_API_GLCORE) || defined(SHADER_API_SWITCH)) // GL Core doesn't have the _half mangling, the rest of them do. Workaround for Nintendo Switch.
#define UNITY_DECLARE_TEX2D_HALF(tex) Texture2D_half tex; SamplerState sampler##tex
#define UNITY_DECLARE_TEX2D_FLOAT(tex) Texture2D_float tex; SamplerState sampler##tex
#define UNITY_DECLARE_TEX2D_NOSAMPLER_HALF(tex) Texture2D_half tex
#define UNITY_DECLARE_TEX2D_NOSAMPLER_FLOAT(tex) Texture2D_float tex
#else
#define UNITY_DECLARE_TEX2D_HALF(tex) Texture2D tex; SamplerState sampler##tex
#define UNITY_DECLARE_TEX2D_FLOAT(tex) Texture2D tex; SamplerState sampler##tex
#define UNITY_DECLARE_TEX2D_NOSAMPLER_HALF(tex) Texture2D tex
#define UNITY_DECLARE_TEX2D_NOSAMPLER_FLOAT(tex) Texture2D tex
#endif

	// Cubemaps
#define UNITY_DECLARE_TEXCUBE(tex) TextureCube tex; SamplerState sampler##tex
#define UNITY_ARGS_TEXCUBE(tex) TextureCube tex, SamplerState sampler##tex
#define UNITY_PASS_TEXCUBE(tex) tex, sampler##tex
#define UNITY_PASS_TEXCUBE_SAMPLER(tex,samplertex) tex, sampler##samplertex
#define UNITY_PASS_TEXCUBE_SAMPLER_LOD(tex, samplertex, lod) tex, sampler##samplertex, lod
#define UNITY_DECLARE_TEXCUBE_NOSAMPLER(tex) TextureCube tex
#define UNITY_SAMPLE_TEXCUBE(tex,coord) tex.Sample (sampler##tex,coord)
#define UNITY_SAMPLE_TEXCUBE_LOD(tex,coord,lod) tex.SampleLevel (sampler##tex,coord, lod)
#define UNITY_SAMPLE_TEXCUBE_SAMPLER(tex,samplertex,coord) tex.Sample (sampler##samplertex,coord)
#define UNITY_SAMPLE_TEXCUBE_SAMPLER_LOD(tex, samplertex, coord, lod) tex.SampleLevel (sampler##samplertex, coord, lod)
// 3D textures
#define UNITY_DECLARE_TEX3D(tex) Texture3D tex; SamplerState sampler##tex
#define UNITY_DECLARE_TEX3D_NOSAMPLER(tex) Texture3D tex
#define UNITY_SAMPLE_TEX3D(tex,coord) tex.Sample (sampler##tex,coord)
#define UNITY_SAMPLE_TEX3D_LOD(tex,coord,lod) tex.SampleLevel (sampler##tex,coord, lod)
#define UNITY_SAMPLE_TEX3D_SAMPLER(tex,samplertex,coord) tex.Sample (sampler##samplertex,coord)
#define UNITY_SAMPLE_TEX3D_SAMPLER_LOD(tex, samplertex, coord, lod) tex.SampleLevel(sampler##samplertex, coord, lod)

#if defined(UNITY_COMPILER_HLSLCC) && !defined(SHADER_API_GLCORE) // GL Core doesn't have the _half mangling, the rest of them do.
#define UNITY_DECLARE_TEX3D_FLOAT(tex) Texture3D_float tex; SamplerState sampler##tex
#define UNITY_DECLARE_TEX3D_HALF(tex) Texture3D_half tex; SamplerState sampler##tex
#else
#define UNITY_DECLARE_TEX3D_FLOAT(tex) Texture3D tex; SamplerState sampler##tex
#define UNITY_DECLARE_TEX3D_HALF(tex) Texture3D tex; SamplerState sampler##tex
#endif

	// 2D arrays
#define UNITY_DECLARE_TEX2DARRAY_MS(tex) Texture2DMSArray<float> tex; SamplerState sampler##tex
#define UNITY_DECLARE_TEX2DARRAY_MS_NOSAMPLER(tex) Texture2DArray<float> tex
#define UNITY_DECLARE_TEX2DARRAY(tex) Texture2DArray tex; SamplerState sampler##tex
#define UNITY_DECLARE_TEX2DARRAY_NOSAMPLER(tex) Texture2DArray tex
#define UNITY_ARGS_TEX2DARRAY(tex) Texture2DArray tex, SamplerState sampler##tex
#define UNITY_PASS_TEX2DARRAY(tex) tex, sampler##tex
#define UNITY_SAMPLE_TEX2DARRAY(tex,coord) tex.Sample (sampler##tex,coord)
#define UNITY_SAMPLE_TEX2DARRAY_LOD(tex,coord,lod) tex.SampleLevel (sampler##tex,coord, lod)
#define UNITY_SAMPLE_TEX2DARRAY_SAMPLER(tex,samplertex,coord) tex.Sample (sampler##samplertex,coord)
#define UNITY_SAMPLE_TEX2DARRAY_SAMPLER_LOD(tex,samplertex,coord,lod) tex.SampleLevel (sampler##samplertex,coord,lod)

// Cube arrays
#define UNITY_DECLARE_TEXCUBEARRAY(tex) TextureCubeArray tex; SamplerState sampler##tex
#define UNITY_DECLARE_TEXCUBEARRAY_NOSAMPLER(tex) TextureCubeArray tex
#define UNITY_ARGS_TEXCUBEARRAY(tex) TextureCubeArray tex, SamplerState sampler##tex
#define UNITY_PASS_TEXCUBEARRAY(tex) tex, sampler##tex
#if defined(SHADER_API_PSSL)
	// round the layer index to get DX11-like behaviour (otherwise fractional indices result in mixed up cubemap faces)
#define UNITY_SAMPLE_TEXCUBEARRAY(tex,coord) tex.Sample (sampler##tex,float4((coord).xyz, round((coord).w)))
#define UNITY_SAMPLE_TEXCUBEARRAY_LOD(tex,coord,lod) tex.SampleLevel (sampler##tex,float4((coord).xyz, round((coord).w)), lod)
#define UNITY_SAMPLE_TEXCUBEARRAY_SAMPLER(tex,samplertex,coord) tex.Sample (sampler##samplertex,float4((coord).xyz, round((coord).w)))
#define UNITY_SAMPLE_TEXCUBEARRAY_SAMPLER_LOD(tex,samplertex,coord,lod) tex.SampleLevel (sampler##samplertex,float4((coord).xyz, round((coord).w)), lod)
#else
#define UNITY_SAMPLE_TEXCUBEARRAY(tex,coord) tex.Sample (sampler##tex,coord)
#define UNITY_SAMPLE_TEXCUBEARRAY_LOD(tex,coord,lod) tex.SampleLevel (sampler##tex,coord, lod)
#define UNITY_SAMPLE_TEXCUBEARRAY_SAMPLER(tex,samplertex,coord) tex.Sample (sampler##samplertex,coord)
#define UNITY_SAMPLE_TEXCUBEARRAY_SAMPLER_LOD(tex,samplertex,coord,lod) tex.SampleLevel (sampler##samplertex,coord,lod)
#endif


#else
	// DX9 style HLSL syntax; same object for texture+sampler
	// 2D textures
#define UNITY_DECLARE_TEX2D(tex) sampler2D tex
#define UNITY_DECLARE_TEX2D_HALF(tex) sampler2D_half tex
#define UNITY_DECLARE_TEX2D_FLOAT(tex) sampler2D_float tex

#define UNITY_DECLARE_TEX2D_NOSAMPLER(tex) sampler2D tex
#define UNITY_DECLARE_TEX2D_NOSAMPLER_HALF(tex) sampler2D_half tex
#define UNITY_DECLARE_TEX2D_NOSAMPLER_FLOAT(tex) sampler2D_float tex

#define UNITY_SAMPLE_TEX2D(tex,coord) tex2D (tex,coord)
#define UNITY_SAMPLE_TEX2D_SAMPLER(tex,samplertex,coord) tex2D (tex,coord)
// Cubemaps
#define UNITY_DECLARE_TEXCUBE(tex) samplerCUBE tex
#define UNITY_ARGS_TEXCUBE(tex) samplerCUBE tex
#define UNITY_PASS_TEXCUBE(tex) tex
#define UNITY_PASS_TEXCUBE_SAMPLER(tex,samplertex) tex
#define UNITY_DECLARE_TEXCUBE_NOSAMPLER(tex) samplerCUBE tex
#define UNITY_SAMPLE_TEXCUBE(tex,coord) texCUBE (tex,coord)

#define UNITY_SAMPLE_TEXCUBE_LOD(tex,coord,lod) texCUBElod (tex, half4(coord, lod))
#define UNITY_SAMPLE_TEXCUBE_SAMPLER_LOD(tex,samplertex,coord,lod) UNITY_SAMPLE_TEXCUBE_LOD(tex,coord,lod)
#define UNITY_SAMPLE_TEXCUBE_SAMPLER(tex,samplertex,coord) texCUBE (tex,coord)

// 3D textures
#define UNITY_DECLARE_TEX3D(tex) sampler3D tex
#define UNITY_DECLARE_TEX3D_NOSAMPLER(tex) sampler3D tex
#define UNITY_DECLARE_TEX3D_FLOAT(tex) sampler3D_float tex
#define UNITY_DECLARE_TEX3D_HALF(tex) sampler3D_float tex
#define UNITY_SAMPLE_TEX3D(tex,coord) tex3D (tex,coord)
#define UNITY_SAMPLE_TEX3D_LOD(tex,coord,lod) tex3D (tex,float4(coord,lod))
#define UNITY_SAMPLE_TEX3D_SAMPLER(tex,samplertex,coord) tex3D (tex,coord)
#define UNITY_SAMPLE_TEX3D_SAMPLER_LOD(tex,samplertex,coord,lod) tex3D (tex,float4(coord,lod))

// 2D array syntax for surface shader analysis
#if defined(SHADER_TARGET_SURFACE_ANALYSIS)
#define UNITY_DECLARE_TEX2DARRAY(tex) sampler2DArray tex
#define UNITY_DECLARE_TEX2DARRAY_NOSAMPLER(tex) sampler2DArray tex
#define UNITY_ARGS_TEX2DARRAY(tex) sampler2DArray tex
#define UNITY_PASS_TEX2DARRAY(tex) tex
#define UNITY_SAMPLE_TEX2DARRAY(tex,coord) tex2DArray (tex,coord)
#define UNITY_SAMPLE_TEX2DARRAY_LOD(tex,coord,lod) tex2DArraylod (tex, float4(coord,lod))
#define UNITY_SAMPLE_TEX2DARRAY_SAMPLER(tex,samplertex,coord) tex2DArray (tex,coord)
#define UNITY_SAMPLE_TEX2DARRAY_SAMPLER_LOD(tex,samplertex,coord,lod) tex2DArraylod (tex, float4(coord,lod))
#endif

// surface sh ader analysis; just pretend that 2D arrays are cubemaps
#if defined(SHADER_TARGET_SURFACE_ANALYSIS)
#define sampler2DArray samplerCUBE
#define tex2DArray texCUBE
#define tex2DArraylod texCUBElod
#endif

#endif

#define MOD3 float3(443.8975, 397.2973, 491.1871)
#define s2(a, b)				temp = a; a = min(a, b); b = max(temp, b);
#define mn3(a, b, c)			s2(a, b); s2(a, c);
#define mx3(a, b, c)			s2(b, c); s2(a, c);

#define mnmx3(a, b, c)				mx3(a, b, c); s2(a, b);                                   // 3 exchanges
#define mnmx4(a, b, c, d)			s2(a, b); s2(c, d); s2(a, c); s2(b, d);                   // 4 exchanges
#define mnmx5(a, b, c, d, e)		s2(a, b); s2(c, d); mn3(a, c, e); mx3(b, d, e);           // 6 exchanges
#define mnmx6(a, b, c, d, e, f) 	s2(a, d); s2(b, e); s2(c, f); mn3(a, b, c); mx3(d, e, f); // 7 exchanges

inline half  SafeHDR(half  c) { return min(c, 65504.0); }
inline half2 SafeHDR2(half2 c) { return min(c, 65504.0); }
inline half3 SafeHDR3(half3 c) { return min(c, 65504.0); }
inline half4 SafeHDR4(half4 c) { return min(c, 65504.0); }



// https://www.shadertoy.com/view/MtjBWz - thanks iq
float2 rndC(float2 uv) // good function
{
	uv = uv * _ScreenParams.xy + 0.5;
	float2 iuv = floor(uv);
	float2 fuv = frac(uv);

	uv = iuv + fuv * fuv * (3.0 - 2.0 * fuv); // smoothstep

	return(uv - 0.5);  // returns in same unit as input, voxels
}

inline float LuminanceSimple(float3 col)
{
	return dot(col, lumacoeff);
}


float smootherstep(float edge0, float edge1, float x)
{
	x = clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
	return x * x * x * (x * (x * 6.0 - 15.0) + 10.0);
}


half3 TransformColor(half3 skyboxValue, float Threshold) {
	return dot(max(skyboxValue.rgb - Threshold.rrr, half3(0, 0, 0)), half3(1, 1, 1)); // threshold and convert to greyscale
}

half3 ThresholdSmoothHalf(half4 col, float Threshold)
{
	//const float BEGIN_SPILL = 0.8;
	float BEGIN_SPILL = Threshold;
	const float END_SPILL = 2222.0;
	const float MAX_SPILL = 0.9; //note: <=1

	half3 mc = (float3)max(col.r, max(col.g, col.b));
	float t = MAX_SPILL * smootherstep(0.0, END_SPILL - BEGIN_SPILL, mc - BEGIN_SPILL);
	return lerp(col.rgb, mc, t);
}

float3 ThresholdSmooth(float3 col, float Threshold)
{
	//const float BEGIN_SPILL = 0.8;
	float BEGIN_SPILL = Threshold;
	const float END_SPILL = 2222.0;
	const float MAX_SPILL = 0.9; //note: <=1

	float3 mc = (float3)max(col.r, max(col.g, col.b));
	float t = MAX_SPILL * smootherstep(0.0, END_SPILL - BEGIN_SPILL, mc - BEGIN_SPILL);
	return lerp(col, mc, t);
}

inline float3 ThresholdColor(float3 col, float Threshold)
{
	float val = (col.x + col.y + col.z) / 3.0;
	return col * smootherstep(Threshold - 0.1, Threshold + 0.1, val);
}

float3 ThresholdGradual(float3 col, float Threshold)
{
	col *= saturate(LuminanceSimple(col) / Threshold);
	return col;
}

inline float2 EncodeFloatRG(float v)
{
	float2 kEncodeMul = float2(1.0, 255.0);
	float kEncodeBit = 1.0 / 255.0;
	float2 enc = kEncodeMul * v;
	enc = frac(enc);
	enc.x -= enc.y * kEncodeBit;
	return enc;
}
inline float DecodeFloatRG(float2 enc)
{
	float2 kDecodeDot = float2(1.0, 1 / 255.0);
	return dot(enc, kDecodeDot);
}

struct PRISMAttributesDefault
{
	float3 vertex : POSITION;
};

//PRISM Vertex shaders
struct PRISMVaryingsDefault
{
	float4 vertex : SV_POSITION;
	float2 texcoord : TEXCOORD0;
};


// fixed for Single Pass stereo rendering method
half3 UpsampleFilter(Texture2D tex, half4 tex_ST, float2 uv, float2 texelSize, float sampleScale, SamplerState texSampler)
{
	// 9-tap bilinear upsampler (tent filter)
	float4 d = texelSize.xyxy * float4(1.0, 1.0, -1.0, 0.0) * sampleScale;
	half3 s = (half3)0.0;

	//#if SHADER_TARGET > 30
	s = tex.Sample(texSampler, (uv - d.xy));
	s += tex.Sample(texSampler, (uv - d.wy)) * 2.0;
	s += tex.Sample(texSampler, (uv - d.zy));

	s += tex.Sample(texSampler, (uv + d.zw)) * 2.0;
	s += tex.Sample(texSampler, (uv)) * 4.0;
	s += tex.Sample(texSampler, (uv + d.xw)) * 2.0;

	s += tex.Sample(texSampler, (uv + d.zy));
	s += tex.Sample(texSampler, (uv + d.wy)) * 2.0;
	s += tex.Sample(texSampler, (uv + d.xy));
	//#endif


	return s * (1.0 / 16.0);
}

// fixed for Single Pass stereo rendering method
half3 UpsampleFilterMobile(sampler2D tex, half4 tex_ST, float2 uv, float2 texelSize, float sampleScale)
{
	// 9-tap bilinear upsampler (tent filter)
	float4 d = texelSize.xyxy * float4(1.0, 1.0, -1.0, 0.0) * sampleScale;
	half3 s = (half3)0.0;

	//#if SHADER_TARGET > 30
	s = tex2D(tex, (uv - d.xy));
	s += tex2D(tex, (uv - d.wy)) * 2.0;
	s += tex2D(tex, (uv - d.zy));

	s += tex2D(tex, (uv + d.zw)) * 2.0;
	s += tex2D(tex, (uv)) * 4.0;
	s += tex2D(tex, (uv + d.xw)) * 2.0;

	s += tex2D(tex, (uv + d.zy));
	s += tex2D(tex, (uv + d.wy)) * 2.0;
	s += tex2D(tex, (uv + d.xy));
	//#endif


	return s * (1.0 / 16.0);
}

half4 KawaseBlurMobile(sampler2D s, float2 uv, int iteration, float2 pixelSize)
{
	half2 halfPixelSize = pixelSize / 2.0;
	half2 dUV = (pixelSize.xy * float(iteration)) + halfPixelSize.xy;
	half4 cOut;
	half4 cheekySample = half4(uv.x, uv.x, uv.y, uv.y) + half4(-dUV.x, dUV.x, dUV.y, dUV.y);
	half4 cheekySample2 = half4(uv.x, uv.x, uv.y, uv.y) + half4(dUV.x, -dUV.x, -dUV.y, -dUV.y);

	cOut = tex2D(s, cheekySample.rb);
	cOut += tex2D(s, cheekySample.ga);
	cOut += tex2D(s, cheekySample.rb);
	cOut += tex2D(s, cheekySample.ga);
	cOut *= 0.25;
	return cOut;
}

float4 KawaseBlur(Texture2D s, float2 uv, int iteration, SamplerState texSampler, float2 pixelSize)
{
	float2 halfPixelSize = pixelSize / 2.0;
	float2 dUV = (pixelSize.xy * float(iteration)) + halfPixelSize.xy;
	float4 cOut;
	float4 cheekySample = float4(uv.x, uv.x, uv.y, uv.y) + float4(-dUV.x, dUV.x, dUV.y, dUV.y);
	float4 cheekySample2 = float4(uv.x, uv.x, uv.y, uv.y) + float4(dUV.x, -dUV.x, -dUV.y, -dUV.y);

	// Sample top left pixel
	cOut = s.Sample(texSampler, cheekySample.rb);
	// Sample top right pixel
	cOut += s.Sample(texSampler, cheekySample.ga);
	// Sample bottom right pixel
	cOut += s.Sample(texSampler, cheekySample2.rb);
	// Sample bottom left pixel
	cOut += s.Sample(texSampler, cheekySample2.ga);
	// Average
	cOut *= 0.25f;
	//return tex2D(s, uv);
	return cOut;
}

struct MyCustomData
{
	half3 something;
	half3 somethingElse;
};
uniform RWStructuredBuffer<MyCustomData> _MyCustomBuffer : register(u1);

float ease_linear(float x) {
	return x;
}

float ease_in_quad(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	return c * (t /= d) * t + b;
}

float ease_out_quad(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	return -c * (t /= d) * (t - 2) + b;
}

float ease_in_out_quad(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	if ((t /= d / 2) < 1) return c / 2 * t * t + b;
	return -c / 2 * ((--t) * (t - 2) - 1) + b;
}

float ease_in_cubic(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	return c * (t /= d) * t * t + b;
}

float ease_out_cubic(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	return c * ((t = t / d - 1) * t * t + 1) + b;
}

float ease_in_out_cubic(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	if ((t /= d / 2) < 1) return c / 2 * t * t * t + b;
	return c / 2 * ((t -= 2) * t * t + 2) + b;
}

float ease_in_quart(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	return c * (t /= d) * t * t * t + b;
}

float ease_out_quart(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	return -c * ((t = t / d - 1) * t * t * t - 1) + b;
}

float ease_in_out_quart(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	if ((t /= d / 2) < 1) return c / 2 * t * t * t * t + b;
	return -c / 2 * ((t -= 2) * t * t * t - 2) + b;
}

float ease_in_quint(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	return c * (t /= d) * t * t * t * t + b;
}

float ease_out_quint(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	return c * ((t = t / d - 1) * t * t * t * t + 1) + b;
}

float ease_in_out_quint(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	if ((t /= d / 2) < 1) return c / 2 * t * t * t * t * t + b;
	return c / 2 * ((t -= 2) * t * t * t * t + 2) + b;
}

float ease_in_sine(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	return -c * cos(t / d * (3.14159265359 / 2)) + c + b;
}

float ease_out_sine(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	return c * sin(t / d * (3.14159265359 / 2)) + b;
}

float ease_in_out_Sine(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	return -c / 2 * (cos(3.14159265359 * t / d) - 1) + b;
}

float ease_in_expo(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	return (t == 0) ? b : c * pow(2, 10 * (t / d - 1)) + b;
}

float ease_out_expo(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	return (t == d) ? b + c : c * (-pow(2, -10 * t / d) + 1) + b;
}

float ease_in_out_expo(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	if (t == 0) return b;
	if (t == d) return b + c;
	if ((t /= d / 2) < 1) return c / 2 * pow(2, 10 * (t - 1)) + b;
	return c / 2 * (-pow(2, -10 * --t) + 2) + b;
}

float ease_in_circ(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	return -c * (sqrt(1 - (t /= d) * t) - 1) + b;
}

float ease_out_circ(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	return c * sqrt(1 - (t = t / d - 1) * t) + b;
}

float ease_in_out_circ(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	if ((t /= d / 2) < 1) return -c / 2 * (sqrt(1 - t * t) - 1) + b;
	return c / 2 * (sqrt(1 - (t -= 2) * t) + 1) + b;
}

float ease_in_elastic(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	float s = 1.70158; float p = 0; float a = c;
	if (t == 0) return b;  if ((t /= d) == 1) return b + c;  if (p == 0) p = d * .3;
	if (a < abs(c)) { a = c; s = p / 4; }
	else s = p / (2 * 3.14159265359) * asin(c / a);
	return -(a * pow(2, 10 * (t -= 1)) * sin((t * d - s) * (2 * 3.14159265359) / p)) + b;
}

float ease_out_elastic(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	float s = 1.70158; float p = 0; float a = c;
	if (t == 0) return b;  if ((t /= d) == 1) return b + c;  if (p == 0) p = d * .3;
	if (a < abs(c)) { a = c; s = p / 4; }
	else s = p / (2 * 3.14159265359) * asin(c / a);
	return a * pow(2, -10 * t) * sin((t * d - s) * (2 * 3.14159265359) / p) + c + b;
}

float ease_in_out_elastic(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	float s = 1.70158; float p = 0; float a = c;
	if (t == 0) return b;  if ((t /= d / 2) == 2) return b + c;  if (p == 0) p = d * (.3 * 1.5);
	if (a < abs(c)) { a = c; s = p / 4; }
	else s = p / (2 * 3.14159265359) * asin(c / a);
	if (t < 1) return -.5 * (a * pow(2, 10 * (t -= 1)) * sin((t * d - s) * (2 * 3.14159265359) / p)) + b;
	return a * pow(2, -10 * (t -= 1)) * sin((t * d - s) * (2 * 3.14159265359) / p) * .5 + c + b;
}

float ease_in_back(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	float s = 1.70158;
	return c * (t /= d) * t * ((s + 1) * t - s) + b;
}

float ease_out_back(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	float s = 1.70158;
	return c * ((t = t / d - 1) * t * ((s + 1) * t + s) + 1) + b;
}

float ease_in_out_back(float x) {
	float t = x; float b = 0; float c = 1; float d = 1;
	float s = 1.70158;
	if ((t /= d / 2) < 1) return c / 2 * (t * t * (((s *= (1.525)) + 1) * t - s)) + b;
	return c / 2 * ((t -= 2) * t * (((s *= (1.525)) + 1) * t + s) + 2) + b;
}

float ease_out_bounce(float x, float t, float b, float c, float d) {
	if ((t /= d) < (1 / 2.75)) {
		return c * (7.5625 * t * t) + b;
	}
	else if (t < (2 / 2.75)) {
		return c * (7.5625 * (t -= (1.5 / 2.75)) * t + .75) + b;
	}
	else if (t < (2.5 / 2.75)) {
		return c * (7.5625 * (t -= (2.25 / 2.75)) * t + .9375) + b;
	}
	else {
		return c * (7.5625 * (t -= (2.625 / 2.75)) * t + .984375) + b;
	}
}

float ease_out_bounce(float x) {
	return ease_out_bounce(x, x, 0, 1, 1);
}

float ease_in_bounce(float x, float t, float b, float c, float d) {
	return c - ease_out_bounce(x, d - t, 0, c, d) + b;
}

float ease_in_bounce(float x) {
	return ease_in_bounce(x, x, 0, 1, 1);
}

float ease_in_out_bounce(float x, float t, float b, float c, float d) {
	if (t < d / 2) return ease_in_bounce(x, t * 2, 0, c, d) * .5 + b;
	return ease_out_bounce(x, t * 2 - d, 0, c, d) * .5 + c * .5 + b;
}

float ease_in_out_bounce(float x) {
	return ease_in_out_bounce(x, x, 0, 1, 1);
}

#define GammaCurve Fast
#define gamma 2.2
#pragma warning (disable : 3206)

// -- Misc --
sampler s0 : register(s0);
float4 p0 :  register(c0);
float2 p1 :  register(c1);

#define width  (p0[0])
#define height (p0[1])

#define px (p1[0])
#define py (p1[1])

// -- Option values --
#define None  1
#define sRGB  2
#define Power 3
#define Fast  4
#define true  5
#define false 6

#define A (0.272433)

#if GammaCurve == sRGB
float3 Gamma(float3 x) { return x < (0.0392857 / 12.9232102) ? x * 12.9232102 : 1.055 * pow(x, 1 / gamma) - 0.055; }
float3 GammaInv(float3 x) { return x < 0.0392857 ? x / 12.9232102 : pow((x + 0.055) / 1.055, gamma); }
#elif GammaCurve == Power
float3 Gamma(float3 x) { return pow(saturate(x), 1 / gamma); }
float3 GammaInv(float3 x) { return pow(saturate(x), gamma); }
#elif GammaCurve == Fast
float3 Gamma(float3 x) { return saturate(x) * rsqrt(saturate(x)); }
float3 GammaInv(float3 x) { return x * x; }
#elif GammaCurve == None
float3 Gamma(float3 x) { return x; }
float3 GammaInv(float3 x) { return x; }
#endif

#define HALF_MAX 65504.0

inline half3 SafeHDRTwo(half3 c) { return min(c, HALF_MAX); }

// -- Colour space Processing --
#define Kb 0.114
#define Kr 0.299
#define RGBtoYUV float3x3(float3(Kr, 1 - Kr - Kb, Kb), float3(-Kr, Kr + Kb - 1, 1 - Kb) / (2*(1 - Kb)), float3(1 - Kr, Kr + Kb - 1, -Kb) / (2*(1 - Kr)))
#define YUVtoRGB float3x3(float3(1, 0, 2*(1 - Kr)), float3(Kb + Kr - 1, 2*(1 - Kb)*Kb, 2*Kr*(1 - Kr)) / (Kb + Kr - 1), float3(1, 2*(1 - Kb),0))
#define RGBtoXYZ float3x3(float3(0.4124,0.3576,0.1805),float3(0.2126,0.7152,0.0722),float3(0.0193,0.1192,0.9502))
#define XYZtoRGB (625.0*float3x3(float3(67097680, -31827592, -10327488), float3(-20061906, 38837883, 859902), float3(1153856, -4225640, 21892272))/12940760409.0)
#define YUVtoXYZ mul(RGBtoXYZ,YUVtoRGB)
#define XYZtoYUV mul(RGBtoYUV,XYZtoRGB)

float3 Labf(float3 x) { return x < (6.0 * 6.0 * 6.0) / (29.0 * 29.0 * 29.0) ? (x * (29.0 * 29.0) / (3.0 * 6.0 * 6.0)) + (4.0 / 29.0) : pow(abs(x), 1.0 / 3.0); }
float3 Labfinv(float3 x) { return x < (6.0 / 29.0) ? (x - (4.0 / 29.0)) * (3.0 * 6.0 * 6.0) / (29.0 * 29.0) : x * x * x; }

float3 DLabf(float3 x) { return min((29.0 * 29.0) / (3.0 * 6.0 * 6.0), (1.0 / 3.0) / pow(x, (2.0 / 3.0))); }
float3 DLabfinv(float3 x) { return max((3.0 * 6.0 * 6.0) / (29.0 * 29.0), 3.0 * x * x); }
float3 RGBtoLab(float3 rgb) {
	float3 xyz = mul(RGBtoXYZ, rgb);
	xyz = Labf(xyz);
	return float3(1.16 * xyz.y - 0.16, 5.0 * (xyz.x - xyz.y), 2.0 * (xyz.y - xyz.z));
}

float3 LabtoRGB(float3 lab) {
	float3 xyz = (lab.x + 0.16) / 1.16 + float3(lab.y / 5.0, 0, -lab.z / 2.0);
	return saturate(mul(XYZtoRGB, Labfinv(xyz)));
}

float3 LabtoRGBHDR(float3 lab) {
	float3 xyz = (lab.x + 0.16) / 1.16 + float3(lab.y / 5.0, 0, -lab.z / 2.0);
	return SafeHDRTwo(mul(XYZtoRGB, Labfinv(xyz)));
}

float3x3 DRGBtoLab(float3 rgb) {
	float3 xyz = mul(RGBtoXYZ, rgb);
	xyz = DLabf(xyz);
	float3x3 D = { { xyz.x, 0, 0 }, { 0, xyz.y, 0 }, { 0, 0, xyz.z } };
	return mul(D, RGBtoXYZ);
}

float3x3 DLabtoRGB(float3 lab) {
	float3 xyz = (lab.x + 0.16) / 1.16 + float3(lab.y / 5.0, 0, -lab.z / 2.0);
	xyz = DLabfinv(xyz);
	float3x3 D = { { xyz.x, 0, 0 }, { 0, xyz.y, 0 }, { 0, 0, xyz.z } };
	return mul(XYZtoRGB, D);
}

float PRISMLuma(float3 rgb) {
	return dot(RGBtoYUV[0], rgb);
}

float3 SMH(float3 col, float luma, float4 shadows, float4 midtones, float4 highlights, float3 saturations)
{

	float luminance = luma;

	//Histogram split
	float4 FShadows = (0.5 + (cos(3.14159 * min(luminance, 0.5) * 2.0)) * 0.5) * shadows;
	float4 FMid = (0.5 - (cos(3.14159 * luminance * 2.0)) * 0.5) * midtones;
	float4 FHigh = (0.5 + (cos(3.14159 * max(luminance, 0.5) * 2.0)) * 0.5) * highlights;

	//Color Correct
	col = lerp(col, (float3)(luminance), (saturations)); //Desaturation
	col = col + (FShadows * FShadows.a) + (FMid * FMid.a) + (FHigh * FHigh.a); //Color Balance
	col = col * ((float3)(1.0) + FShadows.rgb) * ((float3)(1.0) + FMid.rgb) * ((float3)(1.0) + FHigh.rgb); //Color lift
	return col; //ColorGain todo exposure
}

inline float glslTanh(float val)
{
	float tmp = exp(val);
	float tanH = (tmp - 1.0 / tmp) / (tmp + 1.0 / tmp);
	return tanH;
}

inline float3 glslTanh(float3 val)
{
	float3 tmp = exp(val);
	float3 tanH = (tmp - 1.0 / tmp) / (tmp + 1.0 / tmp);
	return tanH;
}

inline float3 ApplyColorify(float3 col, float _Colourfulness, float luma)
{

	float3 colour = luma + (col - luma) * (max(_Colourfulness, -1) + 1);

	float3 diff = colour - col;

#if SHADER_API_GLCORE || SHADER_API_OPENGL || SHADER_API_GLES || SHADER_API_GLES3 || SHADER_API_GLES2 || SHADER_API_WIIU
	diff = glslTanh(diff);
#else
	diff = tanh(diff);
#endif

	return float3(col + diff);
}

float ApplyLiftInvGammaGain(const float lift, const float invGamma, const float gain, float v)
{
	// lerp gain
	float lerpV = saturate(pow(v, invGamma));
	float dst = gain * lerpV + lift * (1.0 - lerpV);
	return dst;
}



uniform float ApertureFNumber; uniform float ISO; uniform float ShutterSpeedValue; uniform float maxLuminance;

float SaturationBasedExposure()
{
	float maxLuminance = (7800.0f / 65.0f) * (ApertureFNumber * ApertureFNumber) / (ISO * ShutterSpeedValue);
	return log2(1.0f / maxLuminance);
}

float StandardOutputBasedExposure(float middleGrey = 0.18f)
{
	float lAvg = (1000.0f / 65.0f) * (ApertureFNumber * ApertureFNumber) / (ISO * ShutterSpeedValue);
	return log2(middleGrey / lAvg);
}

float Log2Exposure(in float avgLuminance, float ManualExposure)
{
	float exposure = 0.0f;

	exposure = ManualExposure;

	return exposure;
}

float LinearExposure(in float avgLuminance, float ManualExposure)
{
	return exp2(Log2Exposure(avgLuminance, ManualExposure));
}

float3 CalcExposedColor(in float3 color, in float avgLuminance, in float offset, in float ManualExposure, out float exposure)
{
	exposure = Log2Exposure(avgLuminance, ManualExposure);
	exposure += offset;
	return exp2(exposure) * color;
}