#ifndef CABERRATION
#define CABERRATION

	#include "Common.hlsl"

	TEXTURE2D_X(_MainTex);
	SAMPLER(sampler_MainTex);
	float4 _MainTex_TexelSize;

	#include "Distortion.hlsl"
	
	float4 fragChromaticAberration (Varyings i) : SV_Target {
	    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        i.uv         = UnityStereoTransformScreenSpaceTex(i.uv);
        float4 pixel = GetDistortedColor(i.uv);
  		return pixel;
	}


#endif // CABERRATION