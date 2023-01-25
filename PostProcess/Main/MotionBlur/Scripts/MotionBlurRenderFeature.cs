using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TK.Rendering.PostFX
{
    public class MotionBlurRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            [HideInInspector] public Shader shader;
            [HideInInspector] public Shader motionVectorShader;
        }
        [System.Serializable]
        public class MotionVectorSettings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRendering;
            internal Shader shader;
            public LayerMask layerMask;
        }

        public Settings settings = new Settings();
        public MotionVectorSettings _vectorsettings = new MotionVectorSettings();

        private MotionBlurPass _pass;
        private MotionVectorPass _vectorPass;

        public override void Create()
        {
            this.name = "MotionBlur";
            settings.shader = Shader.Find("TK/PostFX/MotionBlur");
            _vectorsettings.shader = Shader.Find("TK/PostFX/MotionVector");
            SupportedRenderingFeatures.active.motionVectors = true;
            UniversalRenderPipeline.asset.supportsCameraDepthTexture = true;
            _vectorPass = new MotionVectorPass(_vectorsettings.renderPassEvent, _vectorsettings.shader, _vectorsettings.layerMask);
            _pass = new MotionBlurPass(settings.renderPassEvent, settings.shader);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_vectorPass);
            _pass.Setup(renderer.cameraColorTarget);
            renderer.EnqueuePass(_pass);
        }
    }
}
