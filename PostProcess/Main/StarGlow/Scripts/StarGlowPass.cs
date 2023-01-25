using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TK.Rendering.PostFX
{
    public class StarGlowPass : CustomPostProcessingPass<StarGlow>
    {
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int TempTargetId = Shader.PropertyToID("_TempTargetStarGlow");
        private static readonly int BlurTex1Id = Shader.PropertyToID("_BlurGlowTex1");
        private static readonly int BlurTex2Id = Shader.PropertyToID("_BlurGlowTex2");
        private static readonly int CompositeTargetId = Shader.PropertyToID("_CompositeTarget");

        private static readonly int ParameterId = Shader.PropertyToID("_Parameter");
        private static readonly int CompositeTexId = Shader.PropertyToID("_CompositeTex");
        private static readonly int IterationId = Shader.PropertyToID("_Iteration");
        private static readonly int OffsetId = Shader.PropertyToID("_Offset");

        private static int blurTex1, blurTex2;

        protected override string RenderTag => "StarGlow";
        public StarGlowPass(RenderPassEvent renderPassEvent, Shader shader) : base(renderPassEvent, shader) 
        { }
        protected override void BeforeRender(CommandBuffer commandBuffer, ref RenderingData renderingData)
        {
            blurTex1 = BlurTex1Id;
            blurTex2 = BlurTex2Id;
        }

        protected override void Render(CommandBuffer commandBuffer, ref RenderingData renderingData, RenderTargetIdentifier source, RenderTargetIdentifier dest)
        {
            ref var cameraData = ref renderingData.cameraData;
            var width = cameraData.camera.scaledPixelWidth;
            var height = cameraData.camera.scaledPixelHeight;

            GetTremporaryRT(commandBuffer, width, height, TempTargetId);
            GetTremporaryRT(commandBuffer, width, height, blurTex1);
            GetTremporaryRT(commandBuffer, width, height, blurTex2);
            GetTremporaryRT(commandBuffer, width, height, CompositeTexId);

            commandBuffer.SetGlobalVector(ParameterId,
                 new Vector4(Component.Threshold.value, Component.Intensity.value, Component.Attenuation.value, 1f));

            commandBuffer.SetGlobalTexture(MainTexId, source);

            commandBuffer.Blit(source, TempTargetId, Material, 0);

            var angle = 360f / Component.StreakCount.value;

            for (int i = 1; i <= Component.StreakCount.value; i++)
            {
                var offset = (Quaternion.AngleAxis(angle * i + Component.Angle.value, Vector3.forward) * Vector2.down).normalized;
                commandBuffer.SetGlobalVector(OffsetId, new Vector2(offset.x, offset.y));
                commandBuffer.SetGlobalInt(IterationId, 1);

                commandBuffer.Blit(TempTargetId, blurTex1, Material, 1);

                for (int j = 2; j <= Component.Iteration.value; j++)
                {
                    commandBuffer.SetGlobalInt(IterationId, 1);
                    commandBuffer.Blit(blurTex1, blurTex2, Material, 1);

                    // swap
                    var temp = blurTex1;
                    blurTex1 = blurTex2;
                    blurTex2 = temp;
                }

                commandBuffer.Blit(blurTex2, CompositeTexId, Material, 2);
            }

            //commandBuffer.SetGlobalTexture(CompositeTexId, dest);
            commandBuffer.Blit(source, dest, Material, 3);


            commandBuffer.ReleaseTemporaryRT(TempTargetId);
            commandBuffer.ReleaseTemporaryRT(BlurTex1Id);
            commandBuffer.ReleaseTemporaryRT(BlurTex2Id);
            commandBuffer.ReleaseTemporaryRT(CompositeTexId);
        }

        private void GetTremporaryRT(CommandBuffer command, int width, int height, int destination)
        {
            command.GetTemporaryRT(destination,
                width / Component.Divide.value, height / Component.Divide.value, 0,
                FilterMode.Point,
                RenderTextureFormat.Default);
        }
        protected override bool IsActive()
        {
            return Component.IsActive();
        }
    }
}