Shader "PostFx/SpotLightShaft"
{
	Properties
	{
	}
	SubShader
	{
		Pass
		{
			Name "VolumetricSpotlights"

			Cull Front
			ZWrite Off
			ZTest Always
			Blend One One, Zero One

			HLSLPROGRAM
			#define DIRECTIONAL_EPSILON 0.0009765625
			#define SPOTLIGHT_CAPACITY 32
			#define MAX_STEPS 512
			#define DENSITY 8.0

			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ADDITIONAL_LIGHTS
			#pragma multi_compile_fragment _ADDITIONAL_LIGHT_SHADOWS
			# include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			# include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			struct Varyings
			{
				float4 positionHCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float4 positionNDCXYWAndDepth : TEXCOORD1;
			};

			Varyings vert(float4 positionOS : POSITION)
			{
				Varyings OUT;
				OUT.positionWS = TransformObjectToWorld(positionOS.xyz);
				OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
				float4 ndc = OUT.positionHCS * 0.5;
				OUT.positionNDCXYWAndDepth.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
				OUT.positionNDCXYWAndDepth.z = -dot(GetWorldToViewMatrix()[2], float4(OUT.positionWS, 1.0));
				OUT.positionNDCXYWAndDepth.w = OUT.positionHCS.w;
				return OUT;
			}

			TEXTURE2D(_CameraDepthTexture);
			SAMPLER(sampler_CameraDepthTexture);
			int _LightsCount;
			int _LightFarSamplerCount;

			float3 worldToObjectPos(float3 worldPos) { return mul(unity_WorldToObject, float4(worldPos, 1.0)).xyz; }
			float3 worldToObjectVec(float3 worldVec) { return mul((float3x3)unity_WorldToObject, worldVec); }
			float3 objectToWorldVec(float3 localVec) { return mul((float3x3)unity_ObjectToWorld, localVec); }
			float3 getCameraPositionWS() { return UNITY_MATRIX_I_V._14_24_34; }
			float3 getCameraDirectionWS() { return -UNITY_MATRIX_V._31_32_33; }
			bool isPerspective() { return any(UNITY_MATRIX_P._41_42_43); }

			float3 getCameraOriginWS(float3 positionWS)
			{
				return isPerspective()
					? getCameraPositionWS()
					: positionWS + dot(getCameraPositionWS() - positionWS, getCameraDirectionWS()) * getCameraDirectionWS();
			}

			float3 getWorldBoundsCenter() { return unity_ObjectToWorld._14_24_34 + unity_ObjectToWorld._11_22_33 * 0.5; }

			float getBoundsDepthWS(float3 cameraDirectionWS)
			{
				float3 cornerVectorWS1 = objectToWorldVec(float3(0.5, 0.5, 0.5));
				float3 cornerVectorWS2 = objectToWorldVec(float3(-0.5, 0.5, 0.5));
				float3 cornerVectorWS3 = objectToWorldVec(float3(0.5, -0.5, 0.5));
				float3 cornerVectorWS4 = objectToWorldVec(float3(-0.5, -0.5, 0.5));
				float2 lengths1 = abs(float2(dot(cornerVectorWS1, cameraDirectionWS), dot(cornerVectorWS2, cameraDirectionWS)));
				float2 lengths2 = abs(float2(dot(cornerVectorWS3, cameraDirectionWS), dot(cornerVectorWS4, cameraDirectionWS)));
				float2 lengths = max(lengths1, lengths2);
				return max(lengths.x, lengths.y) * 2.0;
			}

			float2 getBoundsNearFarWS(float boundsDepthWS)
			{
				float center = isPerspective()
					? distance(getWorldBoundsCenter(), getCameraPositionWS())
					: dot(getWorldBoundsCenter() - getCameraPositionWS(), getCameraDirectionWS());
				return float2(-0.5, 0.5) * boundsDepthWS + center;
			}

			float3 getBoundsFacesOS(float3 positionOS, float3 viewDirectionOS, float faceOffset)
			{
				float3 signs = sign(viewDirectionOS);
				return -(signs * (positionOS - 0.5) + faceOffset) / (abs(viewDirectionOS) + (1.0 - abs(signs)) * DIRECTIONAL_EPSILON);
			}

			float getBoundsFrontFaceOS(float3 positionOS, float3 viewDirectionOS)
			{
				float3 lengths = getBoundsFacesOS(positionOS, viewDirectionOS, 0.5);
				return max(max(max(lengths.x, lengths.y), lengths.z), 0.0);
			}

			float sampleOpaqueZ(float2 uv)
			{
				float rawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
				return isPerspective()
					? LinearEyeDepth(rawDepth, _ZBufferParams)
					: -dot(unity_CameraInvProjection._33_34, float2(_ProjectionParams.x * (rawDepth * 2.0 - 1.0), 1.0));
			}

			float2 inverseLerp(float2 a, float2 b, float2 value) { return (value - a) / (b - a); }
			float random(float2 st) { return frac(sin(dot(st, float2(12.9898, 78.233))) * 43758.5453123); }

			float4 frag(Varyings IN) : SV_Target
			{
				float2 positionSS = IN.positionNDCXYWAndDepth.xy / IN.positionNDCXYWAndDepth.w;
				bool isPerspectiveProjection = isPerspective();
				float3 viewDirectionWS = isPerspectiveProjection ? -GetWorldSpaceNormalizeViewDir(IN.positionWS) : getCameraDirectionWS();
				float3 viewDirectionOS = worldToObjectVec(viewDirectionWS);
				float peripheralFactor = 1.0 / dot(getCameraDirectionWS(), viewDirectionWS);
				float3 cameraOriginWS = getCameraOriginWS(IN.positionWS);
				float3 cameraOriginOS = worldToObjectPos(cameraOriginWS);
				float frontFaceWS = dot(objectToWorldVec(getBoundsFrontFaceOS(cameraOriginOS, viewDirectionOS) * viewDirectionOS), viewDirectionWS);
				float backFaceWS = dot(IN.positionWS - cameraOriginWS, viewDirectionWS);
				float opaqueWS = sampleOpaqueZ(positionSS) * peripheralFactor;
				float enterWS = max(frontFaceWS, 0.0);
				float exitWS = min(backFaceWS, opaqueWS) * 50000;
				clip(exitWS - enterWS);
				float boundsDepthWS = getBoundsDepthWS(getCameraDirectionWS());
				float stepLength = boundsDepthWS / MAX_STEPS;
				float2 boundsNearFarWS = getBoundsNearFarWS(boundsDepthWS);
				int2 stepEnterExit = (int2)clamp(ceil(inverseLerp(boundsNearFarWS.x, boundsNearFarWS.y, float2(enterWS * 0.01, exitWS)) * MAX_STEPS), 0, MAX_STEPS) * _LightFarSamplerCount;
				float3 integratedColor = 0.0;
				float jitter = stepLength * random(positionSS);
				for (int i = stepEnterExit.x; i < stepEnterExit.y; i++)
				{
					float3 p = cameraOriginWS + viewDirectionWS * ((i + jitter) * stepLength);
					for (int lightIndex = 0; lightIndex < _LightsCount; lightIndex++)
					{
						Light light = GetAdditionalPerObjectLight(lightIndex, p);
						float shadowAttenuation = AdditionalLightRealtimeShadow(lightIndex, p, light.direction);
						integratedColor += light.color * (light.distanceAttenuation * shadowAttenuation);
					}
				}
				return float4(integratedColor * DENSITY / (boundsDepthWS * MAX_STEPS), 1.0);
			}
			ENDHLSL
		}
	}
}
