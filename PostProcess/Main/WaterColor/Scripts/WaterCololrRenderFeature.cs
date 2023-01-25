using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TK.Rendering.PostFX
{
    public class WaterCololrRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            internal Shader shader;
        }

        public Settings settings = new Settings();

        private WaterColorPass _pass;

        public override void Create()
        {
            this.name = "WaterColor";
            settings.shader = Shader.Find("TK/PostFX/WaterColor");
            _pass = new WaterColorPass(settings.renderPassEvent, settings.shader);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            _pass.Setup(renderer.cameraColorTarget);
            renderer.EnqueuePass(_pass);
        }
    }
}
