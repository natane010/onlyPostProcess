using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TK.Rendering.PostFX
{
    public class StarGlowRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            internal Shader shader;
        }

        public Settings settings = new Settings();

        private StarGlowPass _pass;

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            _pass.Setup(renderer.cameraColorTarget);
            renderer.EnqueuePass(_pass);
        }

        public override void Create()
        {
            this.name = "StarGlow";
            settings.shader = Shader.Find("TK/PostFX/StarGlow");
            _pass = new StarGlowPass(settings.renderPassEvent, settings.shader);
        }
    }
}