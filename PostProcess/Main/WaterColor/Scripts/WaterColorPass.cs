using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TK.Rendering.PostFX
{
    public class WaterColorPass : CustomPostProcessingPass<WaterColor>
    {

        private static readonly int TempBlurBuffer1 = Shader.PropertyToID("_TempBlurBuffer1");
        private static readonly int TempBlurBuffer2 = Shader.PropertyToID("_TempBlurBuffer2");
        private static readonly int TempBlurBuffer3 = Shader.PropertyToID("_TempBlurBuffer3");
        private static readonly int StentNum1 = Shader.PropertyToID("_Ref");
        #region tone
        private static readonly int warm = Shader.PropertyToID("_ColorWarm");
        private static readonly int cool = Shader.PropertyToID("_ColorCool");
        private static readonly int power = Shader.PropertyToID("_TonePower");
        private static readonly int lightdir = Shader.PropertyToID("_LightDir");
        #endregion
        #region water color
        private static readonly int wobbTex = Shader.PropertyToID("_WobbTex");
        private static readonly int wobbTexScale = Shader.PropertyToID("_WobbScale");
        private static readonly int wobbPow = Shader.PropertyToID("_WobbPower");
        private static readonly int edgeSize = Shader.PropertyToID("_EdgeSize");
        private static readonly int edgePow = Shader.PropertyToID("_EdgePower");
        #endregion
        #region paper
        private static readonly int paperTex = Shader.PropertyToID("_PaperTex");
        private static readonly int paperScale = Shader.PropertyToID("_PaperScale");
        private static readonly int paperPow = Shader.PropertyToID("_PaperPower");
        #endregion
        public WaterColorPass(RenderPassEvent renderPassEvent, Shader shader) : base(renderPassEvent, shader)
        {
        }

        protected override string RenderTag => "WaterColor";


        protected override void BeforeRender(CommandBuffer commandBuffer, ref RenderingData renderingData)
        {
            Material.SetInt(StentNum1, Component.wobbStent.value);
            #region tone
            Vector3 lightDir = renderingData.cameraData.camera.transform.InverseTransformDirection
                (Component.dirLight == null ? Vector3.forward : Component.dirLight.value.normalized);
            lightDir.x *= -1f;
            lightDir.y *= -1f;
            Material.SetColor(warm, Component.warm.value);
            Material.SetColor(cool, Component.cool.value);
            Material.SetFloat(power, Component.tonePower.value);
            Material.SetVector(lightdir, lightDir);
            #endregion

            #region water color
            //Component.paperDataset = new PaperData[Component.paperLength.value];
            Material.SetTexture(wobbTex, Component.wobbTex.value);
            Material.SetFloat(wobbTexScale, Component.wobbScale.value);
            Material.SetFloat(wobbPow, Component.wobbPower.value);
            Material.SetFloat(edgeSize, Component.edgeSize.value);
            Material.SetFloat(edgePow, Component.edgePower.value);
            #endregion

            #region paper
            Material.SetTexture(paperTex, Component.paperTex.value);
            Material.SetFloat(paperScale, Component.paperScale.value);
            Material.SetFloat(paperPow, Component.paperPow.value);
            #endregion
        }
       
        protected override void Render(CommandBuffer commandBuffer, ref RenderingData renderingData, RenderTargetIdentifier source, RenderTargetIdentifier dest)
        {
            ref var cameraData = ref renderingData.cameraData;
            commandBuffer.GetTemporaryRT(TempBlurBuffer1, cameraData.camera.scaledPixelWidth, cameraData.camera.scaledPixelHeight);
            commandBuffer.GetTemporaryRT(TempBlurBuffer2, cameraData.camera.scaledPixelWidth, cameraData.camera.scaledPixelHeight);
            commandBuffer.GetTemporaryRT(TempBlurBuffer3, cameraData.camera.scaledPixelWidth, cameraData.camera.scaledPixelHeight);

            #region Tone
            if (Component.isToneActive.value)
            {
                commandBuffer.Blit(source, TempBlurBuffer1, Material, 3);
            }
            else
            {
                commandBuffer.Blit(source, TempBlurBuffer1, Material, 4);
            }
            #endregion
            #region water color
            if (Component.isPaperActive.value)
            {
                commandBuffer.Blit(TempBlurBuffer1, TempBlurBuffer2, Material, 2);

            }
            else
            {
                commandBuffer.Blit(TempBlurBuffer1, TempBlurBuffer2, Material, 4);
            }
            commandBuffer.Blit(TempBlurBuffer2, TempBlurBuffer3, Material, 1);
            commandBuffer.Blit(TempBlurBuffer3, dest, Material, 0);

            commandBuffer.ReleaseTemporaryRT(TempBlurBuffer1);
            commandBuffer.ReleaseTemporaryRT(TempBlurBuffer2);
            commandBuffer.ReleaseTemporaryRT(TempBlurBuffer3);
            #endregion
        }
        protected override bool IsActive()
        {
            return Component.IsActive;
        }
    }
}
