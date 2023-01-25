using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TK.Rendering.PostFX
{
    public class FlareRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            internal Shader shader;
        }
        
        public Settings settings = new Settings();

        private FlarePass _pass;

        public override void Create()
        {
            this.name = "Flare";
            settings.shader = Shader.Find("TK/PostFX/Flare");
            _pass = new FlarePass(settings.renderPassEvent, settings.shader);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            _pass.Setup(renderer.cameraColorTarget);
            renderer.EnqueuePass(_pass);
        }
        
    }
}