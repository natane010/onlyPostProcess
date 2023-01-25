using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TK.Rendering.PostFX
{
    public class MobileSSPRPass : ScriptableRenderPass
    {
        static readonly int _SSPR_ColorRT_pid = Shader.PropertyToID("_MobileSSPR_ColorRT");
        static readonly int _SSPR_PackedDataRT_pid = Shader.PropertyToID("_MobileSSPR_PackedDataRT");
        static readonly int _SSPR_PosWSyRT_pid = Shader.PropertyToID("_MobileSSPR_PosWSyRT");
        RenderTargetIdentifier _SSPR_ColorRT_rti = new RenderTargetIdentifier(_SSPR_ColorRT_pid);
        RenderTargetIdentifier _SSPR_PackedDataRT_rti = new RenderTargetIdentifier(_SSPR_PackedDataRT_pid);
        RenderTargetIdentifier _SSPR_PosWSyRT_rti = new RenderTargetIdentifier(_SSPR_PosWSyRT_pid);

        ShaderTagId lightMode_SSPR_sti = new ShaderTagId("MobileSSPR");

        const int SHADER_NUMTHREAD_X = 8; 
        const int SHADER_NUMTHREAD_Y = 8;
        MobileSSPRRenderFeature.SSPRSettings settings;
        ComputeShader cs;
        MobileSSPR m_SSPR;

        public MobileSSPRPass(RenderPassEvent renderPassEvent,ComputeShader computeShader ,MobileSSPRRenderFeature.SSPRSettings settings)
        {
            this.settings = settings;
            this.renderPassEvent = renderPassEvent;

            //cs = (ComputeShader)Resources.Load("MobileSSPRComputeShader");
            cs = computeShader;
        }
        int GetRTHeight()
        {
            return Mathf.CeilToInt(settings.RT_height / (float)SHADER_NUMTHREAD_Y) * SHADER_NUMTHREAD_Y;
        }
        int GetRTWidth()
        {
            float aspect = (float)Screen.width / Screen.height;
            return Mathf.CeilToInt(GetRTHeight() * aspect / (float)SHADER_NUMTHREAD_X) * SHADER_NUMTHREAD_X;
        }
        bool ShouldUseSinglePassUnsafeAllowFlickeringDirectResolve()
        {
            if (settings.EnablePerPlatformAutoSafeGuard)
            {
                //if RInt RT is not supported, use mobile path
                if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RInt))
                    return true;

                //tested Metal(even on a Mac) can't use InterlockedMin().
                //so if metal, use mobile path
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal)
                    return true;
#if UNITY_EDITOR
                //PC(DirectX) can use RenderTextureFormat.RInt + InterlockedMin() without any problem, use Non-Mobile path.
                //Non-Mobile path will NOT produce any flickering
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12)
                    return false;
#elif UNITY_ANDROID
                //- samsung galaxy A70(Adreno612) will fail if use RenderTextureFormat.RInt + InterlockedMin() in compute shader
                //- but Lenovo S5(Adreno506) is correct, WTF???
                //because behavior is different between android devices, we assume all android are not safe to use RenderTextureFormat.RInt + InterlockedMin() in compute shader
                //so android always go mobile path
                return true;
#endif
            }
            //let user decide if we still don't know the correct answer
            return !settings.ShouldRemoveFlickerFinalControl;
        }
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            RenderTextureDescriptor rtd = new RenderTextureDescriptor(GetRTWidth(), GetRTHeight(), RenderTextureFormat.Default, 0, 0);

            rtd.sRGB = false; //don't need gamma correction when sampling these RTs, it is linear data already because it will be filled by screen's linear data
            rtd.enableRandomWrite = true; //using RWTexture2D in compute shader need to turn on this

            //color RT
            bool shouldUseHDRColorRT = settings.UseHDR;
            if (cameraTextureDescriptor.colorFormat == RenderTextureFormat.ARGB32)
                shouldUseHDRColorRT = false;// if there are no HDR info to reflect anyway, no need a HDR colorRT
            rtd.colorFormat = shouldUseHDRColorRT ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32; //we need alpha! (usually LDR is enough, ignore HDR is acceptable for reflection)
            cmd.GetTemporaryRT(_SSPR_ColorRT_pid, rtd);

            //PackedData RT
            if (ShouldUseSinglePassUnsafeAllowFlickeringDirectResolve())
            {
                //use unsafe method if mobile
                //posWSy RT (will use this RT for posWSy compare test, just like the concept of regular depth buffer)
                rtd.colorFormat = RenderTextureFormat.RFloat;
                cmd.GetTemporaryRT(_SSPR_PosWSyRT_pid, rtd);
            }
            else
            {
                //use 100% correct method if console/PC
                rtd.colorFormat = RenderTextureFormat.RInt;
                cmd.GetTemporaryRT(_SSPR_PackedDataRT_pid, rtd);
            }
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var volumeStack = VolumeManager.instance.stack;
            m_SSPR = volumeStack.GetComponent<MobileSSPR>();
            if (m_SSPR == null || !m_SSPR.active || !m_SSPR.IsActive())
            {
                return;
            }

            CommandBuffer cb = CommandBufferPool.Get("SSPR");

            int dispatchThreadGroupXCount = GetRTWidth() / SHADER_NUMTHREAD_X; //divide by shader's numthreads.x
            int dispatchThreadGroupYCount = GetRTHeight() / SHADER_NUMTHREAD_Y; //divide by shader's numthreads.y
            int dispatchThreadGroupZCount = 1; //divide by shader's numthreads.z

            if (m_SSPR.ShouldRenderSSPR.value)
            {
                cb.SetComputeVectorParam(cs, Shader.PropertyToID("_RTSize"), new Vector2(GetRTWidth(), GetRTHeight()));
                cb.SetComputeFloatParam(cs, Shader.PropertyToID("_HorizontalPlaneHeightWS"), m_SSPR.HorizontalReflectionPlaneHeightWS.value);

                cb.SetComputeFloatParam(cs, Shader.PropertyToID("_FadeOutScreenBorderWidthVerticle"), m_SSPR.FadeOutScreenBorderWidthVerticle.value);
                cb.SetComputeFloatParam(cs, Shader.PropertyToID("_FadeOutScreenBorderWidthHorizontal"), m_SSPR.FadeOutScreenBorderWidthHorizontal.value);
                cb.SetComputeVectorParam(cs, Shader.PropertyToID("_CameraDirection"), renderingData.cameraData.camera.transform.forward);
                cb.SetComputeFloatParam(cs, Shader.PropertyToID("_ScreenLRStretchIntensity"), m_SSPR.ScreenLRStretchIntensity.value);
                cb.SetComputeFloatParam(cs, Shader.PropertyToID("_ScreenLRStretchThreshold"), m_SSPR.ScreenLRStretchThreshold.value);
                cb.SetComputeVectorParam(cs, Shader.PropertyToID("_FinalTintColor"), m_SSPR.TintColor.value);

                //we found that on metal, UNITY_MATRIX_VP is not correct, so we will pass our own VP matrix to compute shader
                Camera camera = renderingData.cameraData.camera;
                Matrix4x4 VP = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix;
                cb.SetComputeMatrixParam(cs, "_VPMatrix", VP);

                if (ShouldUseSinglePassUnsafeAllowFlickeringDirectResolve())
                {
                    ////////////////////////////////////////////////
                    //Mobile Path (Android GLES / Metal)
                    ////////////////////////////////////////////////

                    //kernel MobilePathsinglePassColorRTDirectResolve
                    int kernel_MobilePathSinglePassColorRTDirectResolve = cs.FindKernel("MobilePathSinglePassColorRTDirectResolve");
                    cb.SetComputeTextureParam(cs, kernel_MobilePathSinglePassColorRTDirectResolve, "ColorRT", _SSPR_ColorRT_rti);
                    cb.SetComputeTextureParam(cs, kernel_MobilePathSinglePassColorRTDirectResolve, "PosWSyRT", _SSPR_PosWSyRT_rti);
                    cb.SetComputeTextureParam(cs, kernel_MobilePathSinglePassColorRTDirectResolve, "_CameraOpaqueTexture", new RenderTargetIdentifier("_CameraOpaqueTexture"));
                    cb.SetComputeTextureParam(cs, kernel_MobilePathSinglePassColorRTDirectResolve, "_CameraDepthTexture", new RenderTargetIdentifier("_CameraDepthTexture"));
                    cb.DispatchCompute(cs, kernel_MobilePathSinglePassColorRTDirectResolve, dispatchThreadGroupXCount, dispatchThreadGroupYCount, dispatchThreadGroupZCount);

                }
                else
                {
                    ////////////////////////////////////////////////
                    //Non-Mobile Path (PC/console)
                    ////////////////////////////////////////////////

                    //kernel NonMobilePathClear
                    int kernel_NonMobilePathClear = cs.FindKernel("NonMobilePathClear");
                    cb.SetComputeTextureParam(cs, kernel_NonMobilePathClear, "HashRT", _SSPR_PackedDataRT_rti);
                    cb.SetComputeTextureParam(cs, kernel_NonMobilePathClear, "ColorRT", _SSPR_ColorRT_rti);
                    cb.DispatchCompute(cs, kernel_NonMobilePathClear, dispatchThreadGroupXCount, dispatchThreadGroupYCount, dispatchThreadGroupZCount);

                    //kernel NonMobilePathRenderHashRT
                    int kernel_NonMobilePathRenderHashRT = cs.FindKernel("NonMobilePathRenderHashRT");
                    cb.SetComputeTextureParam(cs, kernel_NonMobilePathRenderHashRT, "HashRT", _SSPR_PackedDataRT_rti);
                    cb.SetComputeTextureParam(cs, kernel_NonMobilePathRenderHashRT, "_CameraDepthTexture", new RenderTargetIdentifier("_CameraDepthTexture"));

                    cb.DispatchCompute(cs, kernel_NonMobilePathRenderHashRT, dispatchThreadGroupXCount, dispatchThreadGroupYCount, dispatchThreadGroupZCount);

                    //resolve to ColorRT
                    int kernel_NonMobilePathResolveColorRT = cs.FindKernel("NonMobilePathResolveColorRT");
                    cb.SetComputeTextureParam(cs, kernel_NonMobilePathResolveColorRT, "_CameraOpaqueTexture", new RenderTargetIdentifier("_CameraOpaqueTexture"));
                    cb.SetComputeTextureParam(cs, kernel_NonMobilePathResolveColorRT, "ColorRT", _SSPR_ColorRT_rti);
                    cb.SetComputeTextureParam(cs, kernel_NonMobilePathResolveColorRT, "HashRT", _SSPR_PackedDataRT_rti);
                    cb.DispatchCompute(cs, kernel_NonMobilePathResolveColorRT, dispatchThreadGroupXCount, dispatchThreadGroupYCount, dispatchThreadGroupZCount);
                }

                //optional shared pass to improve result only: fill RT hole
                if (settings.ApplyFillHoleFix)
                {
                    int kernel_FillHoles = cs.FindKernel("FillHoles");
                    cb.SetComputeTextureParam(cs, kernel_FillHoles, "ColorRT", _SSPR_ColorRT_rti);
                    cb.SetComputeTextureParam(cs, kernel_FillHoles, "PackedDataRT", _SSPR_PackedDataRT_rti);
                    cb.DispatchCompute(cs, kernel_FillHoles, Mathf.CeilToInt(dispatchThreadGroupXCount / 2f), Mathf.CeilToInt(dispatchThreadGroupYCount / 2f), dispatchThreadGroupZCount);
                }

                //send out to global, for user's shader to sample reflection result RT (_MobileSSPR_ColorRT)
                //where _MobileSSPR_ColorRT's rgb is reflection color, a is reflection usage 0~1 for user's shader to lerp with fallback reflection probe's rgb
                cb.SetGlobalTexture(_SSPR_ColorRT_pid, _SSPR_ColorRT_rti);
                cb.EnableShaderKeyword("_MobileSSPR");
            }
            else
            {
                //allow user to skip SSPR related code if disabled
                cb.DisableShaderKeyword("_MobileSSPR");
            }

            context.ExecuteCommandBuffer(cb);
            CommandBufferPool.Release(cb);

            //======================================================================
            //draw objects(e.g. reflective wet ground plane) with lightmode "MobileSSPR", which will sample _MobileSSPR_ColorRT
            DrawingSettings drawingSettings = CreateDrawingSettings(lightMode_SSPR_sti, ref renderingData, SortingCriteria.CommonOpaque);
            FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.all);
            context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
        }
        public override void FrameCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(_SSPR_ColorRT_pid);

            if (ShouldUseSinglePassUnsafeAllowFlickeringDirectResolve())
                cmd.ReleaseTemporaryRT(_SSPR_PosWSyRT_pid);
            else
                cmd.ReleaseTemporaryRT(_SSPR_PackedDataRT_pid);
        }
    }
}
