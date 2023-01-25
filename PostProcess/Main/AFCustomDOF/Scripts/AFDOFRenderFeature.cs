using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TK.Rendering.PostFX
{
    public class AFDOFRenderFeature : ScriptableRendererFeature
    {
        public const string SKW_DEPTH_OF_FIELD = "DEPTH_OF_FIELD";
        public const string SKW_DEPTH_OF_FIELD_TRANSPARENT = "DOF_TRANSPARENT";
        public const string SKW_TURBO = "TURBO";
        public const string SKW_CHROMATIC_ABERRATION = "CHROMATIC_ABERRATION";
        public const string SKW_SUN_FLARES_USE_DEPTH = "SF_USE_DEPTH";
        public const string SKW_CUSTOM_DEPTH_ALPHA_TEST = "PREPASS_ALPHA_TEST";

        static class ShaderParams
        {
            public static int mainTex = Shader.PropertyToID("_MainTex");
            public static int inputTex = Shader.PropertyToID("_InputTex");
            public static int colorParams = Shader.PropertyToID("_Params");
            public static int colorBoost = Shader.PropertyToID("_ColorBoost");
            public static int tintColor = Shader.PropertyToID("_TintColor");
            public static int compareTex = Shader.PropertyToID("_CompareTex");
            public static int compareParams = Shader.PropertyToID("_CompareParams");
            public static int fxColor = Shader.PropertyToID("_FXColor");
            public static int colorTemp = Shader.PropertyToID("_ColorTemp");

            public static int blurScale = Shader.PropertyToID("_BlurScale");
            public static int tempBlurRT = Shader.PropertyToID("_TempBlurRT");
            public static int tempBlurOneDirRT = Shader.PropertyToID("_TempBlurOneDir0");
            public static int tempBlurOneDirRTOriginal = Shader.PropertyToID("_TempBlurOneDir0");
            public static int tempBlurDownscaling = Shader.PropertyToID("_TempBlurDownscaling");

            public static int dofRT = Shader.PropertyToID("_DoFTex");
            public static int dofTempBlurDoFAlphaRT = Shader.PropertyToID("_TempBlurAlphaDoF");
            public static int dofTempBlurDoFTemp1RT = Shader.PropertyToID("_TempBlurPass1DoF");
            public static int dofTempBlurDoFTemp2RT = Shader.PropertyToID("_TempBlurPass2DoF");
            public static int dofBokehData = Shader.PropertyToID("_BokehData");
            public static int dofBokehData2 = Shader.PropertyToID("_BokehData2");
            public static int dofBokehData3 = Shader.PropertyToID("_BokehData3");
            public static int dofBokehRT = Shader.PropertyToID("_DofBokeh");



            public static int eaLumSrc = Shader.PropertyToID("_EALumSrc");
            public static int eaHist = Shader.PropertyToID("_EAHist");


            public static int blurRT = Shader.PropertyToID("_BlurTex");
            public static int blurMaskedRT = Shader.PropertyToID("_BlurMaskedTex");
            public static int blurMask = Shader.PropertyToID("_BlurMask");


            public static int chromaticAberrationData = Shader.PropertyToID("_ChromaticAberrationData");
            public static int chromaticTempTex = Shader.PropertyToID("_ChromaticTex");


            public static int CustomDepthAlphaCutoff = Shader.PropertyToID("_Cutoff");
            public static int CustomDepthBaseMap = Shader.PropertyToID("_BaseMap");
        }

        class AFDOFPass : ScriptableRenderPass
        {

            AFDOF volume;

            enum Pass
            {
                CopyExact = 0,
                Compare = 1,
                DOVolume = 2,

                BlurHoriz = 3,
                BlurVert = 4,

                AnamorphicFlaresResample = 5,
                AnamorphicFlaresResampleAndCombine = 6,

                DoFCoC = 7,
                DoFCoCDebug = 8,
                DoFBlur = 9,
                DoFBlurWithoutBokeh = 10,
                DoFBlurHorizontally = 11,
                DoFBlurVertically = 12,
                CopyBilinear = 13,

                DoFDebugTransparent = 14,
                ChromaticAberration = 15,

                BlurMask = 16,
                DoFBokeh = 17,
                DoFAdditive = 18,
                DoFBlurBokeh = 19
            }

            struct BloomMipData
            {
                public int rtDown, rtUp, width, height;
                public int rtDownOriginal, rtUpOriginal;
            }

            const int PYRAMID_COUNT_BLOOM = 5;
            const int PYRAMID_COUNT_EA = 9;

            Material bMat;
            ScriptableRenderer renderer;
            RenderTargetIdentifier source;
            CameraData cameraData;
            RenderTextureDescriptor sourceDesc, sourceDescHP;
            bool supportsFPTextures;
            BloomMipData[] rt, rtAF;
            int[] rtEA;
            Texture2D dirtTexture, flareTex;
            float sunFlareCurrentIntensity;
            Vector4 sunLastScrPos;
            float sunLastRot;
            float sunFlareTime;
            float dofPrevDistance, dofLastAutofocusDistance;
            Vector4 dofLastBokehData;
            RenderTexture rtEAacum, rtEAHist;
            bool requiresLuminanceComputation;
            bool usesBloomAndFlares, usesDepthOfField, usesVignetting, usesSeparateOutline;
            readonly List<string> keywords = new List<string>();
            string[] keywordsArray;
            bool setup;
            static Matrix4x4 matrix4x4identity = Matrix4x4.identity;
            bool supportsRFloatFormat;
            RenderTexture rtCapture;

            public void Setup(Shader shader, ScriptableRenderer renderer, RenderingData renderingData, RenderPassEvent renderingPassEvent)
            {

                FindVolumeComponent();
                renderPassEvent = renderingPassEvent;
                cameraData = renderingData.cameraData;
                if (setup && cameraData.camera != null) return;
                setup = true;

                CheckSceneSettings();
                AFDOFSettings.UnloadDof();

                this.renderer = renderer;
                supportsFPTextures = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf);
                supportsRFloatFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8);

                if (bMat == null)
                {
                    if (shader == null)
                    {
                        Debug.LogWarning("Could not load shader. Please make sure shader is present.");
                    }
                    else
                    {
                        bMat = CoreUtils.CreateEngineMaterial(shader);
                    }
                }
                if (rt == null || rt.Length != PYRAMID_COUNT_BLOOM + 1)
                {
                    rt = new BloomMipData[PYRAMID_COUNT_BLOOM + 1];
                }
                for (int k = 0; k < rt.Length; k++)
                {
                    rt[k].rtDown = rt[k].rtDownOriginal = Shader.PropertyToID("_BloomDownMip" + k);
                    rt[k].rtUp = rt[k].rtUpOriginal = Shader.PropertyToID("_BloomUpMip" + k);
                }

                if (rtAF == null || rtAF.Length != PYRAMID_COUNT_BLOOM + 1)
                {
                    rtAF = new BloomMipData[PYRAMID_COUNT_BLOOM + 1];
                }
                for (int k = 0; k < rtAF.Length; k++)
                {
                    rtAF[k].rtDown = rtAF[k].rtDownOriginal = Shader.PropertyToID("_AFDownMip" + k);
                    rtAF[k].rtUp = rtAF[k].rtUpOriginal = Shader.PropertyToID("_AFUpMip" + k);
                }

                // Initialize eye adaptation buffers descriptors
                if (rtEA == null || rtEA.Length != PYRAMID_COUNT_EA)
                {
                    rtEA = new int[PYRAMID_COUNT_EA];
                }
                for (int k = 0; k < rtEA.Length; k++)
                {
                    rtEA[k] = Shader.PropertyToID("_EAMip" + k);
                }

            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {

                if (bMat == null) return;

                FindVolumeComponent();

                if (volume == null || !volume.IsActive()) return;

                sourceDesc = cameraTextureDescriptor;
                sourceDesc.msaaSamples = 1;
                sourceDesc.depthBufferBits = 0;
                if (volume.downsampling.value && volume.downsamplingMultiplier.value > 1f)
                {
                    sourceDesc.width = (int)(sourceDesc.width / volume.downsamplingMultiplier.value);
                    sourceDesc.height = (int)(sourceDesc.height / volume.downsamplingMultiplier.value);
                }
                sourceDescHP = sourceDesc;
                if (supportsFPTextures)
                {
                    sourceDescHP.colorFormat = RenderTextureFormat.ARGBHalf;
                }
                UpdateMaterialProperties();

            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (bMat == null)
                {
                    Debug.LogError("material not initialized.");
                    return;
                }

                Camera cam = cameraData.camera;
                if (volume == null || cam == null || !volume.IsActive()) return;

                source = renderer.cameraColorTarget;
                var cmd = CommandBufferPool.Get("DOF");



                RestoreRTBufferIds();

                if (requiresLuminanceComputation)
                {
                    DoEyeAdaptation(cmd);
                }

                if (usesDepthOfField)
                {
                    DoDoF(cmd);
                }

                bool usesChromaticAberrationAsPost = volume.chromaticAberrationIntensity.value > 0 && volume.depthOfField.value;
                bool usesFinalBlur = volume.blurIntensity.value > 0;

                bool useBilinearFiltering = volume.downsampling.value && volume.downsamplingMultiplier.value > 1f && volume.downsamplingBilinear.value;
                int copyPass = useBilinearFiltering ? (int)Pass.CopyBilinear : (int)Pass.CopyExact;

                cmd.GetTemporaryRT(ShaderParams.inputTex, sourceDesc, (!volume.downsampling.value || (volume.downsamplingMultiplier.value > 1f && !volume.downsamplingBilinear.value)) ? FilterMode.Point : FilterMode.Bilinear);


                if (usesChromaticAberrationAsPost)
                {
                    FullScreenBlit(cmd, source, ShaderParams.inputTex, bMat, (int)Pass.DOVolume);
                    FullScreenBlit(cmd, ShaderParams.inputTex, source, bMat, (int)Pass.ChromaticAberration);
                }
                else
                {
                    FullScreenBlit(cmd, source, ShaderParams.inputTex, bMat, copyPass);
                    FullScreenBlit(cmd, ShaderParams.inputTex, source, bMat, (int)Pass.DOVolume);
                }
                if (usesFinalBlur)
                {
                    int blurSource = ApplyFinalBlur(cmd, source);
                    FullScreenBlit(cmd, blurSource, source, bMat, (int)Pass.CopyBilinear);
                }

                cmd.ReleaseTemporaryRT(ShaderParams.inputTex);


                context.ExecuteCommandBuffer(cmd);

                CommandBufferPool.Release(cmd);

            }


            void FullScreenBlit(CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination, Material material, int passIndex)
            {
                destination = new RenderTargetIdentifier(destination, 0, CubemapFace.Unknown, -1);
                cmd.SetRenderTarget(destination);
                cmd.SetGlobalTexture(ShaderParams.mainTex, source);
                cmd.DrawMesh(RenderingUtils.fullscreenMesh, matrix4x4identity, material, 0, passIndex);
            }

            public override void FrameCleanup(CommandBuffer cmd)
            {
            }

            public void Cleanup()
            {
                CoreUtils.Destroy(bMat);
#if UNITY_EDITOR
                if (rtCapture != null)
                {
                    rtCapture.Release();
                }
#endif
            }

            void FindVolumeComponent()
            {
                if (volume == null)
                {
                    volume = VolumeManager.instance.stack.GetComponent<AFDOF>();
                }
            }


            void RestoreRTBufferIds()
            {
                // Restore temorary rt ids
                for (int k = 0; k < rt.Length; k++)
                {
                    rt[k].rtDown = rt[k].rtDownOriginal;
                    rt[k].rtUp = rt[k].rtUpOriginal;
                }
                for (int k = 0; k < rtAF.Length; k++)
                {
                    rtAF[k].rtDown = rtAF[k].rtDownOriginal;
                    rtAF[k].rtUp = rtAF[k].rtUpOriginal;
                }
                ShaderParams.tempBlurOneDirRT = ShaderParams.tempBlurOneDirRTOriginal;
            }

            int ApplyFinalBlur(CommandBuffer cmd, RenderTargetIdentifier source)
            {

                int size;
                RenderTextureDescriptor rtBlurDesc = sourceDescHP;

                float blurIntensity = volume.blurIntensity.value;
                if (blurIntensity < 1f)
                {
                    size = (int)Mathf.Lerp(rtBlurDesc.width, 512, blurIntensity);
                }
                else
                {
                    size = (int)(512 / blurIntensity);
                }
                float aspectRatio = (float)sourceDesc.height / sourceDesc.width;
                rtBlurDesc.width = size;
                rtBlurDesc.height = Mathf.Max(1, (int)(size * aspectRatio));
                cmd.GetTemporaryRT(ShaderParams.blurRT, rtBlurDesc, FilterMode.Bilinear);

                float ratio = (float)sourceDesc.width / size;
                float blurScale = blurIntensity > 1f ? 1f : blurIntensity;

                cmd.GetTemporaryRT(ShaderParams.tempBlurDownscaling, rtBlurDesc, FilterMode.Bilinear);
                cmd.SetGlobalFloat(ShaderParams.blurScale, blurScale * ratio);
                FullScreenBlit(cmd, source, ShaderParams.tempBlurDownscaling, bMat, (int)Pass.BlurHoriz);
                cmd.SetGlobalFloat(ShaderParams.blurScale, blurScale);
                FullScreenBlit(cmd, ShaderParams.tempBlurDownscaling, ShaderParams.blurRT, bMat, (int)Pass.BlurVert);
                cmd.ReleaseTemporaryRT(ShaderParams.tempBlurDownscaling);

                BlurThis(cmd, rtBlurDesc, ShaderParams.blurRT, rtBlurDesc.width, rtBlurDesc.height, bMat, blurScale);
                BlurThis(cmd, rtBlurDesc, ShaderParams.blurRT, rtBlurDesc.width, rtBlurDesc.height, bMat, blurScale);
                BlurThis(cmd, rtBlurDesc, ShaderParams.blurRT, rtBlurDesc.width, rtBlurDesc.height, bMat, blurScale);
                if (volume.blurMask.value != null)
                {
                    cmd.GetTemporaryRT(ShaderParams.blurMaskedRT, sourceDesc);
                    FullScreenBlit(cmd, source, ShaderParams.blurMaskedRT, bMat, (int)Pass.BlurMask);
                    return ShaderParams.blurMaskedRT;
                }
                else
                {
                    return ShaderParams.blurRT;
                }
            }


            void BlendOneOne(CommandBuffer cmd, int source, ref int destination, ref int tempBuffer)
            {
                FullScreenBlit(cmd, source, tempBuffer, bMat, (int)Pass.AnamorphicFlaresResampleAndCombine);
                int tmp = destination;
                destination = tempBuffer;
                tempBuffer = tmp;
            }

            void BlurThis(CommandBuffer cmd, RenderTextureDescriptor desc, int rt, int width, int height, Material blurMat, float blurScale = 1f)
            {
                desc.width = width;
                desc.height = height;
                cmd.GetTemporaryRT(ShaderParams.tempBlurRT, desc, FilterMode.Bilinear);
                cmd.SetGlobalFloat(ShaderParams.blurScale, blurScale);
                FullScreenBlit(cmd, rt, ShaderParams.tempBlurRT, blurMat, (int)Pass.BlurHoriz);
                FullScreenBlit(cmd, ShaderParams.tempBlurRT, rt, blurMat, (int)Pass.BlurVert);
                cmd.ReleaseTemporaryRT(ShaderParams.tempBlurRT);
            }


            void DoDoF(CommandBuffer cmd)
            {

                Camera cam = cameraData.camera;
                if (cam.cameraType != CameraType.Game)
                {
                    bMat.DisableKeyword(SKW_DEPTH_OF_FIELD);
                    return;
                }

                UpdateDepthOfFieldData(cmd);

                AFDOFSettings.dofTransparentLayerMask = volume.depthOfFieldTransparentLayerMask.value;
                AFDOFSettings.dofTransparentDoubleSided = volume.depthOfFieldTransparentDoubleSided.value;

                int width = cam.pixelWidth / volume.depthOfFieldDownsampling.value;
                int height = cam.pixelHeight / volume.depthOfFieldDownsampling.value;
                RenderTextureDescriptor dofDesc = sourceDescHP;
                dofDesc.width = width;
                dofDesc.height = height;
                dofDesc.colorFormat = RenderTextureFormat.ARGBHalf;
                cmd.GetTemporaryRT(ShaderParams.dofRT, dofDesc, FilterMode.Bilinear);
                FullScreenBlit(cmd, source, ShaderParams.dofRT, bMat, (int)Pass.DoFCoC);

                if (volume.depthOfFieldForegroundBlur.value && volume.depthOfFieldForegroundBlurHQ.value)
                {
                    BlurThisAlpha(cmd, dofDesc, ShaderParams.dofRT, volume.depthOfFieldForegroundBlurHQSpread.value);
                }

                if (volume.depthOfFieldBokehComposition.value == AFDOF.DoFBokehComposition.Integrated || !volume.depthOfFieldBokeh.value)
                {
                    Pass pass = volume.depthOfFieldBokeh.value ? Pass.DoFBlur : Pass.DoFBlurWithoutBokeh;
                    BlurThisDoF(cmd, dofDesc, ShaderParams.dofRT, (int)pass);
                }
                else
                {
                    BlurThisDoF(cmd, dofDesc, ShaderParams.dofRT, (int)Pass.DoFBlurWithoutBokeh);

                    cmd.GetTemporaryRT(ShaderParams.dofBokehRT, dofDesc, FilterMode.Bilinear);
                    FullScreenBlit(cmd, source, ShaderParams.dofBokehRT, bMat, (int)Pass.DoFBokeh);
                    BlurThisDoF(cmd, dofDesc, ShaderParams.dofBokehRT, (int)Pass.DoFBlurBokeh);
                    FullScreenBlit(cmd, ShaderParams.dofBokehRT, ShaderParams.dofRT, bMat, (int)Pass.DoFAdditive);
                    cmd.ReleaseTemporaryRT(ShaderParams.dofBokehRT);
                }


                cmd.SetGlobalTexture(ShaderParams.dofRT, ShaderParams.dofRT);
            }

            void BlurThisDoF(CommandBuffer cmd, RenderTextureDescriptor dofDesc, int rt, int renderPass)
            {
                cmd.GetTemporaryRT(ShaderParams.dofTempBlurDoFTemp1RT, dofDesc, volume.depthOfFieldFilterMode.value);
                cmd.GetTemporaryRT(ShaderParams.dofTempBlurDoFTemp2RT, dofDesc, volume.depthOfFieldFilterMode.value);

                UpdateDepthOfFieldBlurData(cmd, new Vector2(0.44721f, -0.89443f));
                FullScreenBlit(cmd, rt, ShaderParams.dofTempBlurDoFTemp1RT, bMat, renderPass);

                UpdateDepthOfFieldBlurData(cmd, new Vector2(-1f, 0f));
                FullScreenBlit(cmd, ShaderParams.dofTempBlurDoFTemp1RT, ShaderParams.dofTempBlurDoFTemp2RT, bMat, renderPass);

                UpdateDepthOfFieldBlurData(cmd, new Vector2(0.44721f, 0.89443f));
                FullScreenBlit(cmd, ShaderParams.dofTempBlurDoFTemp2RT, rt, bMat, renderPass);

                cmd.ReleaseTemporaryRT(ShaderParams.dofTempBlurDoFTemp2RT);
                cmd.ReleaseTemporaryRT(ShaderParams.dofTempBlurDoFTemp1RT);
            }


            void BlurThisAlpha(CommandBuffer cmd, RenderTextureDescriptor dofDesc, int rt, float blurScale = 1f)
            {
                cmd.GetTemporaryRT(ShaderParams.dofTempBlurDoFAlphaRT, dofDesc, FilterMode.Bilinear);
                cmd.SetGlobalFloat(ShaderParams.blurScale, blurScale);
                FullScreenBlit(cmd, rt, ShaderParams.dofTempBlurDoFAlphaRT, bMat, (int)Pass.DoFBlurHorizontally);
                FullScreenBlit(cmd, ShaderParams.dofTempBlurDoFAlphaRT, rt, bMat, (int)Pass.DoFBlurVertically);
                cmd.ReleaseTemporaryRT(ShaderParams.dofTempBlurDoFAlphaRT);
            }

            void UpdateDepthOfFieldBlurData(CommandBuffer cmd, Vector2 blurDir)
            {
                float downsamplingRatio = 1f / (float)volume.depthOfFieldDownsampling.value;
                blurDir *= downsamplingRatio;
                dofLastBokehData.z = blurDir.x;
                dofLastBokehData.w = blurDir.y;
                cmd.SetGlobalVector(ShaderParams.dofBokehData, dofLastBokehData);
            }

            void DoEyeAdaptation(CommandBuffer cmd)
            {

                int sizeEA = (int)Mathf.Pow(2, rtEA.Length);

                RenderTextureDescriptor eaDesc = sourceDescHP;
                for (int k = 0; k < rtEA.Length; k++)
                {
                    eaDesc.width = eaDesc.height = sizeEA;
                    cmd.GetTemporaryRT(rtEA[k], eaDesc, FilterMode.Bilinear);
                    sizeEA /= 2;
                }

                FullScreenBlit(cmd, source, rtEA[0], bMat, (int)Pass.CopyBilinear);

                int lumRT = rtEA.Length - 1;
                cmd.SetGlobalTexture(ShaderParams.eaLumSrc, rtEA[lumRT]);
                if (rtEAacum == null)
                {
                    RenderTextureDescriptor rtEASmallDesc = sourceDescHP;
                    rtEASmallDesc.width = rtEASmallDesc.height = 2;
                    rtEAacum = new RenderTexture(rtEASmallDesc);
                    rtEAacum.Create();
                    FullScreenBlit(cmd, rtEA[lumRT], rtEAacum, bMat, (int)Pass.CopyExact);
                    rtEAHist = new RenderTexture(rtEASmallDesc);
                    rtEAHist.Create();
                    FullScreenBlit(cmd, rtEAacum, rtEAHist, bMat, (int)Pass.CopyExact);
                }
                cmd.SetGlobalTexture(ShaderParams.eaHist, rtEAHist);
            }


            Vector3 camPrevForward, camPrevPos;
            float currSens;

            void UpdateMaterialProperties()
            {

                Camera cam = cameraData.camera;
                if (cam == null) return;

                CheckCameraDepthTextureMode(cam);

                keywords.Clear();

                bool isOrtho = cam.orthographic;
                bool linearColorSpace = QualitySettings.activeColorSpace == ColorSpace.Linear;

                float contrast = linearColorSpace ? 1.0f + (volume.contrast.value - 1.0f) / 2.2f : volume.contrast.value;
                bMat.SetVector(ShaderParams.colorBoost, new Vector4(volume.brightness.value, contrast, volume.saturate.value, volume.downsamplingMultiplier.value > 1f ? 0 : volume.ditherIntensity.value));

                // DoF
                usesDepthOfField = false;
                AFDOFSettings.dofTransparentSupport = false;
                AFDOFSettings.dofAlphaTestSupport = false;
                if (volume.depthOfField.value)
                {
                    usesDepthOfField = true;
                    bool transparentSupport = volume.depthOfFieldTransparentSupport.value && volume.depthOfFieldTransparentLayerMask.value > 0;
                    bool alphaTestSupport = volume.depthOfFieldAlphaTestSupport.value && volume.depthOfFieldAlphaTestLayerMask.value > 0;
                    if ((transparentSupport || alphaTestSupport))
                    {
                        keywords.Add(SKW_DEPTH_OF_FIELD_TRANSPARENT);
                        AFDOFSettings.dofTransparentSupport = transparentSupport;
                        if (alphaTestSupport)
                        {
                            AFDOFSettings.dofAlphaTestSupport = true;
                            AFDOFSettings.dofAlphaTestLayerMask = volume.depthOfFieldAlphaTestLayerMask.value;
                            AFDOFSettings.dofAlphaTestDoubleSided = volume.depthOfFieldAlphaTestDoubleSided.value;
                        }
                    }
                    else
                    {
                        keywords.Add(SKW_DEPTH_OF_FIELD);
                    }
                }


                keywords.Add(SKW_TURBO);
                if (volume.chromaticAberrationIntensity.value > 0f)
                {
                    bMat.SetVector(ShaderParams.chromaticAberrationData, new Vector4(volume.chromaticAberrationIntensity.value, volume.chromaticAberrationSmoothing.value, 0, 0));
                    if (!volume.depthOfField.value)
                    {
                        keywords.Add(SKW_CHROMATIC_ABERRATION);
                    }
                }

                if (volume.blurIntensity.value > 0 && volume.blurMask.value != null)
                {
                    bMat.SetTexture(ShaderParams.blurMask, volume.blurMask.value);
                }

                int keywordsCount = keywords.Count;
                if (keywordsArray == null || keywordsArray.Length < keywordsCount)
                {
                    keywordsArray = new string[keywordsCount];
                }
                for (int k = 0; k < keywordsArray.Length; k++)
                {
                    if (k < keywordsCount)
                    {
                        keywordsArray[k] = keywords[k];
                    }
                    else
                    {
                        keywordsArray[k] = "";
                    }
                }
                bMat.shaderKeywords = keywordsArray;
            }


            void UpdateDepthOfFieldData(CommandBuffer cmd)
            {
                if (!CheckSceneSettings()) return;
                Camera cam = cameraData.camera;
                float d = volume.depthOfFieldDistance.value;
                switch ((int)volume.depthOfFieldFocusMode.value)
                {
                    case (int)AFDOF.DoFFocusMode.AutoFocus:
                        UpdateDoFAutofocusDistance(cam);
                        d = dofLastAutofocusDistance > 0 ? dofLastAutofocusDistance : cam.farClipPlane;
                        AFDOFSettings.depthOfFieldCurrentFocalPointDistance = dofLastAutofocusDistance;
                        break;
                    case (int)AFDOF.DoFFocusMode.FollowTarget:
                        if (sceneSettings.depthOfFieldTarget != null)
                        {
                            Vector3 spos = cam.WorldToScreenPoint(sceneSettings.depthOfFieldTarget.position);
                            if (spos.z < 0)
                            {
                                d = cam.farClipPlane;
                            }
                            else
                            {
                                d = Vector3.Distance(cam.transform.position, sceneSettings.depthOfFieldTarget.position);
                            }
                        }
                        break;
                }

                if (sceneSettings.OnBeforeFocus != null)
                {
                    d = sceneSettings.OnBeforeFocus(d);
                }
                dofPrevDistance = Mathf.Lerp(dofPrevDistance, d, Application.isPlaying ? volume.depthOfFieldFocusSpeed.value * Time.unscaledDeltaTime * 30f : 1f);
                float dofCoc;
                if (volume.depthOfFieldCameraSettings.value == AFDOF.DoFCameraSettings.High)
                {
                    float focalLength = volume.depthOfFieldFocalLengthReal.value;
                    float aperture = (focalLength / volume.depthOfFieldFStop.value);
                    dofCoc = aperture * (focalLength / Mathf.Max(dofPrevDistance * 1000f - focalLength, 0.001f)) * (1f / volume.depthOfFieldImageSensorHeight.value) * cam.pixelHeight;
                }
                else
                {
                    dofCoc = volume.depthOfFieldAperture.value * (volume.depthOfFieldFocalLength.value / Mathf.Max(dofPrevDistance - volume.depthOfFieldFocalLength.value, 0.001f)) * (1f / 0.024f);
                }
                dofLastBokehData = new Vector4(dofPrevDistance, dofCoc, 0, 0);
                cmd.SetGlobalVector(ShaderParams.dofBokehData, dofLastBokehData);
                bMat.SetVector(ShaderParams.dofBokehData2, new Vector4(volume.depthOfFieldForegroundBlur.value ? volume.depthOfFieldForegroundDistance.value : cam.farClipPlane, volume.depthOfFieldMaxSamples.value, volume.depthOfFieldBokehThreshold.value, volume.depthOfFieldBokehIntensity.value * volume.depthOfFieldBokehIntensity.value));
                bMat.SetVector(ShaderParams.dofBokehData3, new Vector3(volume.depthOfFieldMaxBrightness.value, volume.depthOfFieldMaxDistance.value * (cam.farClipPlane + 1f), 0));
            }


            void UpdateDoFAutofocusDistance(Camera cam)
            {
                Vector3 p = volume.depthOfFieldAutofocusViewportPoint.value;
                p.z = 10f;
                Ray r = cam.ViewportPointToRay(p);
                RaycastHit hit;
                if (Physics.Raycast(r, out hit, cam.farClipPlane, volume.depthOfFieldAutofocusLayerMask.value))
                {
                    float distance = Vector3.Distance(cam.transform.position, hit.point);
                    distance += volume.depthOfFieldAutofocusDistanceShift.value;
                    dofLastAutofocusDistance = Mathf.Clamp(distance, volume.depthOfFieldAutofocusMinDistance.value, volume.depthOfFieldAutofocusMaxDistance.value);
                }
                else
                {
                    dofLastAutofocusDistance = cam.farClipPlane;
                }
            }

            AFDOFSettings sceneSettings;



            bool CheckSceneSettings()
            {
                sceneSettings = AFDOFSettings.instance;
                return sceneSettings != null;
            }

            void CheckCameraDepthTextureMode(Camera cam)
            {
                if (volume.RequiresDepthTexture())
                {
                    cam.depthTextureMode |= DepthTextureMode.Depth;
                }
            }

        }


        class AFDOFTransparentMaskPass : ScriptableRenderPass
        {

            readonly List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();
            static readonly List<Renderer> cutOutRenderers = new List<Renderer>();

            const string DOF_TRANSPARENT_DEPTH_RT = "_DoFTransparentDepth";
            static int m_CullPropertyId = Shader.PropertyToID("_Cull");
            const string m_ProfilerTag = "CustomDepthPrePass";
            const string m_DepthOnlyShader = "Hidden/DepthOnly";

            RenderTargetHandle m_Depth;
            Material depthOnlyMaterial, depthOnlyMaterialCutOff;
            int currentAlphaCutoutLayerMask = -999;
            Material[] depthOverrideMaterials;

            public AFDOFTransparentMaskPass()
            {
                m_Depth.Init(DOF_TRANSPARENT_DEPTH_RT);
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
                m_ShaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
                m_ShaderTagIdList.Add(new ShaderTagId("UniversalForward"));
                m_ShaderTagIdList.Add(new ShaderTagId("LightweightForward"));
            }


            public void FindAlphaClippingRenderers()
            {
                cutOutRenderers.Clear();
                currentAlphaCutoutLayerMask = AFDOFSettings.dofAlphaTestLayerMask;
                Renderer[] rr = FindObjectsOfType<Renderer>();
                for (int r = 0; r < rr.Length; r++)
                {
                    if (((1 << rr[r].gameObject.layer) & currentAlphaCutoutLayerMask) != 0)
                    {
                        cutOutRenderers.Add(rr[r]);
                    }
                }
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                RenderTextureDescriptor depthDesc = cameraTextureDescriptor;
                depthDesc.colorFormat = RenderTextureFormat.Depth;
                depthDesc.depthBufferBits = 24;
                depthDesc.msaaSamples = 1;
                cmd.GetTemporaryRT(m_Depth.id, depthDesc, FilterMode.Point);
                cmd.SetGlobalTexture(DOF_TRANSPARENT_DEPTH_RT, m_Depth.Identifier());
                ConfigureTarget(m_Depth.Identifier());
                ConfigureClear(ClearFlag.All, Color.black);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {

                CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
                cmd.Clear();

                if (AFDOFSettings.dofAlphaTestSupport)
                {
                    if (AFDOFSettings.dofAlphaTestLayerMask != 0)
                    {
                        if (AFDOFSettings.dofAlphaTestLayerMask != currentAlphaCutoutLayerMask)
                        {
                            FindAlphaClippingRenderers();
                        }
                        if (depthOnlyMaterialCutOff == null)
                        {
                            Shader depthOnlyCutOff = Shader.Find(m_DepthOnlyShader);
                            depthOnlyMaterialCutOff = new Material(depthOnlyCutOff);
                        }
                        int renderersCount = cutOutRenderers.Count;
                        if (depthOverrideMaterials == null || depthOverrideMaterials.Length < renderersCount)
                        {
                            depthOverrideMaterials = new Material[renderersCount];
                        }
                        for (int k = 0; k < renderersCount; k++)
                        {
                            Renderer renderer = cutOutRenderers[k];
                            if (renderer != null && renderer.isVisible)
                            {
                                Material mat = renderer.sharedMaterial;
                                if (mat != null)
                                {
                                    if (depthOverrideMaterials[k] == null)
                                    {
                                        depthOverrideMaterials[k] = Instantiate(depthOnlyMaterialCutOff);
                                        depthOverrideMaterials[k].EnableKeyword(SKW_CUSTOM_DEPTH_ALPHA_TEST);
                                    }
                                    Material overrideMaterial = depthOverrideMaterials[k];

                                    if (mat.HasProperty(ShaderParams.CustomDepthAlphaCutoff))
                                    {
                                        overrideMaterial.SetFloat(ShaderParams.CustomDepthAlphaCutoff, mat.GetFloat(ShaderParams.CustomDepthAlphaCutoff));
                                    }
                                    else
                                    {
                                        overrideMaterial.SetFloat(ShaderParams.CustomDepthAlphaCutoff, 0.5f);
                                    }
                                    if (mat.HasProperty(ShaderParams.CustomDepthBaseMap))
                                    {
                                        overrideMaterial.SetTexture(ShaderParams.mainTex, mat.GetTexture(ShaderParams.CustomDepthBaseMap));
                                    }
                                    else if (mat.HasProperty(ShaderParams.mainTex))
                                    {
                                        overrideMaterial.SetTexture(ShaderParams.mainTex, mat.GetTexture(ShaderParams.mainTex));
                                    }
                                    overrideMaterial.SetInt(m_CullPropertyId, AFDOFSettings.dofAlphaTestDoubleSided ? (int)CullMode.Off : (int)CullMode.Back);

                                    cmd.DrawRenderer(renderer, overrideMaterial);
                                }
                            }
                        }

                    }
                }

                if (AFDOFSettings.dofTransparentSupport)
                {
                    if (depthOnlyMaterial == null)
                    {
                        depthOnlyMaterial = new Material(Shader.Find(m_DepthOnlyShader));
                    }
                    depthOnlyMaterial.SetInt(m_CullPropertyId, AFDOFSettings.dofTransparentDoubleSided ? (int)CullMode.Off : (int)CullMode.Back);

                    SortingCriteria sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
                    var drawingSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortingCriteria);
                    drawingSettings.perObjectData = PerObjectData.None;
                    drawingSettings.overrideMaterial = depthOnlyMaterial;
                    var filter = new FilteringSettings(RenderQueueRange.transparent) { layerMask = AFDOFSettings.dofTransparentLayerMask };
                    context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filter);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void FrameCleanup(CommandBuffer cmd)
            {
                if (cmd == null) return;
                cmd.ReleaseTemporaryRT(m_Depth.id);
            }
        }

        [SerializeField, HideInInspector]
        Shader shader;
        AFDOFPass m_RenderPass;
        AFDOFTransparentMaskPass m_DoFTransparentMaskPass;

        [Tooltip("Note: this option is ignored if Direct Write To Camera option in volume inspector is enabled.")]
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        public static bool installed;

#if UNITY_EDITOR
        public static CameraType captureCameraType = CameraType.SceneView;
        public static bool requestScreenCapture;
#endif


        void OnDisable()
        {
            if (m_RenderPass != null)
            {
                m_RenderPass.Cleanup();
            }
            installed = false;
        }


        public override void Create()
        {
            name = "DOF";
            m_RenderPass = new AFDOFPass();
            m_DoFTransparentMaskPass = new AFDOFTransparentMaskPass();
            shader = Shader.Find("TK/PostFX/AFDOFShader");
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            installed = true;

            if (renderingData.cameraData.postProcessEnabled)
            {
                if (AFDOFSettings.dofTransparentSupport || AFDOFSettings.dofAlphaTestSupport)
                {
                    renderer.EnqueuePass(m_DoFTransparentMaskPass);
                }
                m_RenderPass.Setup(shader, renderer, renderingData, renderPassEvent);
                renderer.EnqueuePass(m_RenderPass);
            }
        }
    }
}
