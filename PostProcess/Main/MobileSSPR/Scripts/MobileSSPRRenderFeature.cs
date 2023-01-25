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
            [Tooltip("512��艺�ɂ���ƃp�t�H�[�}���X���オ��܂�"), Range(128, 1024)]
            public int RT_height = 512;
            [Tooltip("HDR�����i������")]
            public bool UseHDR = true;
            [Tooltip("FillHole�̏C�������i������")]
            public bool ApplyFillHoleFix = true;
            [Tooltip("������␳")]
            public bool ShouldRemoveFlickerFinalControl = true;
            [Tooltip("�f�o�b�O���ȊO�L���ɂ��Ă�������")]
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
