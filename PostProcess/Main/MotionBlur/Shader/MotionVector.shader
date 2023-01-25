Shader "TK/PostFX/MotionVector"
{
    SubShader
    {
        Tags
        { 
            "LightMode" = "MotionVectors" 
        }


        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #pragma multi_compile_instancing

        float4x4 _NonJitteredVP;    // from ScriptableRendererFeature
        float4x4 _PreviousVP;       // from ScriptableRendererFeature

        struct MotionVertexInput 
        {
            float4 vertex : POSITION;
            float3 oldPos : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct MotionVectorData 
        {
            float4 pos : SV_POSITION;
            float4 transferPos : TEXCOORD0;
            float4 transferPosOld : TEXCOORD1;
        };

        MotionVectorData VertMotionVectors(MotionVertexInput v) 
        {
            MotionVectorData o;

            UNITY_SETUP_INSTANCE_ID(v);

            o.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)));

#if defined(UNITY_REVERSED_Z)
            o.pos.z -= unity_MotionVectorsParams.z * o.pos.w;
#else
            o.pos.z += unity_MotionVectorsParams.z * o.pos.w;
#endif

            o.transferPos = mul(_NonJitteredVP, mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)));
            o.transferPosOld = mul(_PreviousVP, mul(unity_MatrixPreviousM, unity_MotionVectorsParams.x > 0 ? float4(v.oldPos, 1) : float4(v.vertex.xyz, 1)));

            return o;
        }

        float4 FragMotionVectors(MotionVectorData i) : SV_TARGET 
        {
            float3 hPos = (i.transferPos.xyz / i.transferPos.w);
            float3 hPosOld = (i.transferPosOld.xyz / i.transferPosOld.w);

            // V is the viewport position at this pixel in the range 0 to 1.
            float2 vPos = (hPos.xy + 1.0f) / 2.0f;
            float2 vPosOld = (hPosOld.xy + 1.0f) / 2.0f;

#if UNITY_UV_STARTS_AT_TOP
            vPos.y = 1.0 - vPos.y;
            vPosOld.y = 1.0 - vPosOld.y;
#endif
            float2 uvDiff = vPos - vPosOld;
            uvDiff *= 0.5;
            // NOTE: unity_MotionVectorsParams.y is opposite of Legacy RP. 0 at ForceNoMotion.
            return lerp(0, float4(uvDiff, 0, 1), unity_MotionVectorsParams.y);
        }

        struct CamMotionVectorsInput 
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct CamMotionVectors 
        {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 ray : TEXCOORD1;
            //UNITY_VERTEX_OUTPUT_STEREO
        };

        //float4x4 unity_CameraToWorld;

        TEXTURE2D_FLOAT(_CameraDepthTexture);
        SAMPLER(sampler_CameraDepthTexture);

        /*inline float4 ComputeScreenPos(float4 pos) 
        {
            float4 o = pos * 0.5f;
            o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
            o.zw = pos.zw;
            return o;
        }*/
        // Z buffer to linear 0..1 depth
        inline float Linear01Depth(float z) 
        {
            return 1.0 / (_ZBufferParams.x * z + _ZBufferParams.y);
        }

        inline half2 CalculateMotion(float rawDepth, float2 inUV, float3 inRay) 
        {
            float depth = Linear01Depth(rawDepth);
            float3 ray = inRay * (_ProjectionParams.z / inRay.z);
            float3 vPos = ray * depth;
            float4 worldPos = mul(unity_CameraToWorld, float4(vPos, 1.0));

            float4 prevClipPos = mul(_PreviousVP, worldPos);
            float4 curClipPos = mul(_NonJitteredVP, worldPos);

            float2 prevHPos = prevClipPos.xy / prevClipPos.w;
            float2 curHPos = curClipPos.xy / curClipPos.w;

            // V is the viewport position at this pixel in the range 0 to 1.
            float2 vPosPrev = (prevHPos.xy + 1.0f) / 2.0f;
            float2 vPosCur = (curHPos.xy + 1.0f) / 2.0f;
#if UNITY_UV_STARTS_AT_TOP
            vPosPrev.y = 1.0 - vPosPrev.y;
            vPosCur.y = 1.0 - vPosCur.y;
#endif
            return vPosCur - vPosPrev;
        }

        CamMotionVectors VertMotionVectorsCamera(CamMotionVectorsInput v) 
        {
            CamMotionVectors o;
            UNITY_SETUP_INSTANCE_ID(v);
            //UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); // not support VR
            o.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)));

#ifdef UNITY_HALF_TEXEL_OFFSET
            o.pos.xy += (_ScreenParams.zw - 1.0) * float2(-1, 1) * o.pos.w;
#endif
            o.uv = ComputeScreenPos(o.pos).xy;
            // we know we are rendering a quad,
            // and the normal passed from C++ is the raw ray.
            o.ray = v.normal;
            return o;
        }

        //half4 FragMotionVectorsCamera(CamMotionVectors i) : SV_Target 
        // {
        //    float depth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, i.uv).r;
        //    return half4(CalculateMotion(depth, i.uv, i.ray), 0, 1);
        //}

        half4 FragMotionVectorsCameraWithDepth(CamMotionVectors i, out float outDepth : SV_Depth) : SV_Target 
        {
            float depth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, i.uv).r;
            outDepth = depth;
            return half4(CalculateMotion(depth, i.uv, i.ray), 0, 1);
        }
        ENDHLSL

        // 0 - Motion vectors
        Pass 
        {
            Name "Motion Vectors"

            Cull Back
            ZTest LEqual
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex VertMotionVectors
            #pragma fragment FragMotionVectors
            ENDHLSL
        }

        // 1 - Camera motion vectors
        //Pass {
        //    Name "Camera Motion Vectors"
        //    Tags{ "LightMode" = "MotionVectors" }

        //    Cull Off
        //    ZTest Always
        //    ZWrite Off

        //    HLSLPROGRAM
        //    #pragma vertex VertMotionVectorsCamera
        //    #pragma fragment FragMotionVectorsCamera
        //    ENDHLSL
        //}

        // 1 - Camera motion vectors (With depth (msaa / no render texture))
        Pass 
        {
            Name "Camera Motion Vectors"

            Cull Off
            ZTest Always
            ZWrite On

            HLSLPROGRAM
            #pragma vertex VertMotionVectorsCamera
            #pragma fragment FragMotionVectorsCameraWithDepth
            ENDHLSL
        }
    }

    Fallback "Hidden/InternalErrorShader"
}