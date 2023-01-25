using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TK.Rendering.PostFX
{
    public class MobileSSPRRenderFeature : ScriptableRendererFeature
    {
        public static MobileSSPRRenderFeature instance;

        [System.Serializable]
        public class SSPRSettings
        {
            [Header("Performance Settings")]
            [Tooltip("512より下にするとパフォーマンスが上がります"), Range(128, 1024)]
            public int RT_height = 512;
            [Tooltip("HDR＝＞品質向上")]
            public bool UseHDR = true;
            [Tooltip("FillHoleの修正＝＞品質向上")]
            public bool ApplyFillHoleFix = true;
            [Tooltip("ちらつき補正")]
            public bool ShouldRemoveFlickerFinalControl = true;
            [Tooltip("デバッグ時以外有効にしてください")]
            public bool EnablePerPlatformAutoSafeGuard = true;
        }
        public SSPRSettings sSPRSettings = new SSPRSettings();
        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            public ComputeShader computeShader;
        }
        public Settings settings = new Settings();


        private MobileSSPRPass _pass;

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_pass);
        }

        public override void Create()
        {
            this.name = "SSPR";
            instance = this;
            _pass = new MobileSSPRPass(settings.renderPassEvent, settings.computeShader, sSPRSettings);
        }
    }
}
