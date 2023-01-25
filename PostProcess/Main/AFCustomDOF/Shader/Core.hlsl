#ifndef CORE_FX
#define CORE_FX

#pragma warning (disable : 3571)

    TEXTURE2D_X(_MainTex);
    SAMPLER(sampler_MainTex);
    float4 _MainTex_TexelSize;

    #include "ACESFitted.hlsl"
    #include "ColorTemp.hlsl"
    #include "Distortion.hlsl"
    #include "Common.hlsl"

	// Copyright 2020-2021 Ramiro Oliva (Kronnect) - All Rights Reserved.
    float4 _CompareParams;
    TEXTURE2D_X(_CompareTex);
    TEXTURE2D_X(_BloomTex);
    TEXTURE2D_X(_ScreenLum);
    TEXTURE2D(_OverlayTex);
    TEXTURE2D(_LUTTex);
    TEXTURE3D(_LUT3DTex);
    TEXTURE2D_X(_EAHist);
    TEXTURE2D_X(_EALumSrc);
    TEXTURE2D(_BlueNoise);
    float4 _BlueNoise_TexelSize;
    TEXTURE2D_X(_BlurTex);
    TEXTURE2D(_BlurMask);

	float4 _Params;
	float4 _Sharpen;
	float4 _Bloom;
    float4 _Dirt;    // x = brightness based, y = intensity, z = threshold, w = bloom contribution    
    float4 _FXColor;
    float4 _ColorBoost;
    float4 _TintColor;
    float4 _Purkinje;
    float4 _EyeAdaptation;
    float4 _BokehData;
    float4 _BokehData2;
    float4 _Outline;
    float3 _ColorTemp;
    float4 _NightVision;
    float4 _LUTTex_TexelSize;
    float2 _LUT3DParams;


    #if DOF_TRANSPARENT || DEPTH_OF_FIELD
        TEXTURE2D_X(_DoFTex);
        float4 _DoFTex_TexelSize;
    #endif
    #if DOF_TRANSPARENT
        TEXTURE2D_X(_DoFTransparentDepth);
        //TEXTURE2D_X(_DofExclusionTexture;
    #endif

 	struct PVaryings 
    {
    	float4 positionCS : SV_POSITION;
    	float2 uv  : TEXCOORD0;
        VERTEX_CROSS_UV_DATA
        UNITY_VERTEX_OUTPUT_STEREO
	};


    PVaryings PVert(Attributes input) {
        PVaryings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = input.positionOS;
        output.positionCS.y *= _ProjectionParams.x;
        output.uv = input.uv.xy;
        VERTEX_OUTPUT_CROSS_UV(output)
    	return output;
	}


	float getRandom(float2 uv) {
		return frac(sin(_Time.y + dot(uv, float2(12.9898, 78.233)))* 43758.5453);
	}


    float getCoc(PVaryings i) {
    #if DOF_TRANSPARENT
        float depthTex = GET_CUSTOM_DEPTH_01(_DoFTransparentDepth, i.uv);
        //float exclusionDepth = DecodeFloatRGBA(SAMPLE_TEXTURE2D_LOD(_DofExclusionTexture, sampler_DoFExclusionTexture, i.uv, 0));
        float depth  = GET_SCENE_DEPTH_01(i.uv);
        depth = min(depth, depthTex);
        //if (exclusionDepth < depth) return 0;
        depth *= _ProjectionParams.z;
    #else
        float depth  = GET_SCENE_DEPTH_EYE(i.uv);
    #endif
        float xd     = abs(depth - _BokehData.x) - _BokehData2.x * (depth < _BokehData.x);
        return 0.5 * _BokehData.y * xd/depth;   // radius of CoC
    }

	void PPass(PVaryings i, inout float3 rgbM) {

        float2 uv = i.uv;
        FRAG_SETUP_CROSS_UV(i)

		float  depthS     = GET_SCENE_DEPTH_01(uvS);
		float  depthW     = GET_SCENE_DEPTH_01(uvW);
		float  depthE     = GET_SCENE_DEPTH_01(uvE);		
		float  depthN     = GET_SCENE_DEPTH_01(uvN);
		float  lumaM      = getLuma(rgbM);

		// daltonize
        #if COLOR_TWEAKS
		float3 rgb0       = 1.0.xxx - saturate(rgbM.rgb);
		       rgbM.r    *= 1.0 + rgbM.r * rgb0.g * rgb0.b * _Params.y;
			   rgbM.g    *= 1.0 + rgbM.g * rgb0.r * rgb0.b * _Params.y;
			   rgbM.b    *= 1.0 + rgbM.b * rgb0.r * rgb0.g * _Params.y;	
			   rgbM      *= lumaM / (getLuma(rgbM) + 0.0001);
        #endif

		// sharpen
		float  maxDepth   = max(depthN, depthS);
		       maxDepth   = max(maxDepth, depthW);
		       maxDepth   = max(maxDepth, depthE);
		float  minDepth   = min(depthN, depthS);
		       minDepth   = min(minDepth, depthW);
		       minDepth   = min(minDepth, depthE);
		float  dDepth     = maxDepth - minDepth + 0.000001;

#if TURBO
        const float  lumaDepth  = 1.0;
#else
		float  lumaDepth  = saturate( _Sharpen.y / dDepth);
#endif

		float3 rgbS       = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uvS).rgb;
	    float3 rgbW       = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uvW).rgb;
	    float3 rgbE       = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uvE).rgb;
	    float3 rgbN       = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uvN).rgb;
	    
    	float  lumaN      = getLuma(rgbN);
    	float  lumaE      = getLuma(rgbE);
    	float  lumaW      = getLuma(rgbW);
    	float  lumaS      = getLuma(rgbS);
		
    	float  maxLuma    = max(lumaN,lumaS);
    	       maxLuma    = max(maxLuma, lumaW);
#if !TURBO
    	       maxLuma    = max(maxLuma, lumaE);
#endif
	    float  minLuma    = min(lumaN,lumaS);
	           minLuma    = min(minLuma, lumaW);
#if !TURBO
	           minLuma    = min(minLuma, lumaE);
#endif
               minLuma   -= 0.000001;
	    float  lumaPower  = 2.0 * lumaM - minLuma - maxLuma;
		float  lumaAtten  = saturate(_Sharpen.w / (maxLuma - minLuma));
#if TURBO
        const float depthClamp = 1.0;
#else
		float  depthClamp = abs(depthW - _Params.z) < _Params.w;
#endif


        #if DEPTH_OF_FIELD || DOF_TRANSPARENT
            float4 dofPix     = SAMPLE_TEXTURE2D_X(_DoFTex, sampler_LinearClamp, uv);
            #if UNITY_COLORSPACE_GAMMA
               dofPix.rgb = LINEAR_TO_GAMMA(dofPix.rgb);
            #endif
            if (_DoFTex_TexelSize.z < _MainTex_TexelSize.z) {
                float  CoC = getCoc(i) / COC_BASE;
                dofPix.a   = lerp(CoC, dofPix.a, _DoFTex_TexelSize.z / _MainTex_TexelSize.z);
            }
            rgbM = lerp(rgbM, dofPix.rgb, saturate(dofPix.a * COC_BASE));
        #endif


		#if UNITY_COLORSPACE_GAMMA && (EYE_ADAPTATION)
	    	rgbM = GAMMA_TO_LINEAR(rgbM);
		#endif

	}

	float4 Frag (PVaryings i) : SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        i.uv     = UnityStereoTransformScreenSpaceTex(i.uv);

        #if CHROMATIC_ABERRATION
            float4 pixel = GetDistortedColor(i.uv);
        #else
       		float4 pixel = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, i.uv);
        #endif

   		PPass(i, pixel.rgb);
   		return pixel;
	}


    float4 FragCompare (PVaryings i) : SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        i.uv     = UnityStereoTransformScreenSpaceTex(i.uv);

        // separator line + antialias
        float2 dd     = i.uv - 0.5.xx;
        float  co     = dot(_CompareParams.xy, dd);
        float  dist   = distance( _CompareParams.xy * co, dd );
        float4 aa     = saturate( (_CompareParams.w - dist) / abs(_MainTex_TexelSize.y) );

        float  sameSide = (_CompareParams.z > -5);
        float2 pixelUV = lerp(i.uv, float2(i.uv.x + _CompareParams.z, i.uv.y), sameSide);
        float2 pixelNiceUV = lerp(i.uv, float2(i.uv.x - 0.5 + _CompareParams.z, i.uv.y), sameSide);
        float4 pixel  = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, pixelUV);
        float4 pixelNice = SAMPLE_TEXTURE2D_X(_CompareTex, sampler_MainTex, pixelNiceUV);
        
        float2 cp     = float2(_CompareParams.y, -_CompareParams.x);
        float t       = dot(dd, cp) > 0;
        pixel         = lerp(pixel, pixelNice, t);
        return pixel + aa;
    }


	half4 FragCopy (PVaryings i) : SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        float2 uv     = UnityStereoTransformScreenSpaceTex(i.uv);
        #if defined(USE_BILINEAR)
            return SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv);
        #else
		    return SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, uv);
        #endif
	}


	half4 FragCopyWithMask (PVaryings i) : SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        float2 uv       = UnityStereoTransformScreenSpaceTex(i.uv);
        half4 mask      = SAMPLE_TEXTURE2D(_BlurMask, sampler_LinearClamp, uv);
        half4 srcPixel  = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv);
        half4 blurPixel = SAMPLE_TEXTURE2D_X(_BlurTex, sampler_LinearClamp, uv);
        return lerp(srcPixel, blurPixel, mask.a);
	}


#endif // CORE_FX