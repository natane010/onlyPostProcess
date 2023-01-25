using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TK.Rendering.PostFX
{
    public class ColorScalePass : CustomPostProcessingPass<ColorScale>
    {
        private static readonly int TempBlurBuffer1 = Shader.PropertyToID("_TempBlurBuffer1");
        private static readonly int IntensityId = UnityEngine.Shader.PropertyToID("_Color");
        private static readonly int StentNumId = UnityEngine.Shader.PropertyToID("_RefColMaskNum");
        private static readonly int StentLutNumId = UnityEngine.Shader.PropertyToID("_RefLutMask");
        private static readonly int gamma = Shader.PropertyToID("_Gamma");
        private static readonly int gainI = Shader.PropertyToID("_Lift");
        private static readonly int liftI = Shader.PropertyToID("_Gain");
        private static readonly int hue = Shader.PropertyToID("_Hue");
        private static readonly int sat = Shader.PropertyToID("_Sat");
        private static readonly int val = Shader.PropertyToID("_Val");
        private static readonly int temputure = Shader.PropertyToID("_Temp");
        private static readonly int GoldenRot = Shader.PropertyToID("_GoldenRot");
        private static readonly int pAmount = Shader.PropertyToID("_pAmount");
        private static readonly int photoBool = Shader.PropertyToID("_Rev");

        #region LUT
        private Texture2D previous, current;
        private Texture2D converted2D = null;
        private Texture3D converted3D = null;
        private int previousLutDimension;
        private readonly int isLinear;
        private float lutAmount;
        private int lutDimension;
        static readonly int lutTexture2DString = Shader.PropertyToID("_LutTex2D");
        static readonly int lutTexture3DString = Shader.PropertyToID("_LutTex3D");
        static readonly int lutAmountString = Shader.PropertyToID("_LutAmount");
        static readonly int tempCopyString = Shader.PropertyToID("_TempCopy");
        private RenderTargetIdentifier tempCopy = new RenderTargetIdentifier(tempCopyString);
        #endregion

        public ColorScalePass(RenderPassEvent renderPassEvent, Shader shader) : base(renderPassEvent, shader)
        {
        }

        protected override string RenderTag => "ColorScale";


        protected override void BeforeRender(CommandBuffer commandBuffer, ref RenderingData renderingData)
        {
            Material.SetColor(IntensityId, Component.color.value);
            Material.SetInt(StentNumId, Component.ColorStentNumber.value);
            Material.SetInt(StentLutNumId, Component.LUTStentNumber.value);
            Material.SetColor(gamma, Component.gamma.value);
            Material.SetColor(gainI, Component.gain.value);
            Material.SetColor(liftI, Component.lift.value);
            Material.SetFloat(hue, Component.hue.value);
            Material.SetFloat(sat, Component.sat.value);
            Material.SetFloat(val, Component.val.value);
            Material.SetFloat(temputure, Component.temputure.value);
            if (Component.reversephotograph.value)
            {
                Material.SetFloat(photoBool, 0);
            }
            else
            {
                Material.SetFloat(photoBool, 1);
            }
            Material.SetFloat(pAmount, Component.photographAmount.value);
            if (Component.activeLut.value && Component.sourceLut != null)
            {
                lutAmount = Component.lutAmount.value;
                lutDimension = Component.lutDimension.value;
                current = (Texture2D)Component.sourceLut.value;
                isConverted();
            }
        }
        protected override void SetupRenderTexture(CommandBuffer commandBuffer, ref RenderingData renderingData)
        {
            ref var cameraData = ref renderingData.cameraData;

            var desc = new RenderTextureDescriptor(cameraData.camera.scaledPixelWidth, cameraData.camera.scaledPixelHeight);
            desc.colorFormat = cameraData.isHdrEnabled ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;

            // RTŠm•Û
            commandBuffer.GetTemporaryRT(TempColorBufferId, desc, FilterMode.Bilinear);
        }
        protected override void CopyToTempBuffer(CommandBuffer commandBuffer, ref RenderingData renderingData, RenderTargetIdentifier source, RenderTargetIdentifier dest)
        {
            commandBuffer.Blit(source, TempColorBufferId);
        }

        protected override void Render(CommandBuffer commandBuffer, ref RenderingData renderingData, RenderTargetIdentifier source, RenderTargetIdentifier dest)
        {
            ref var cameraData = ref renderingData.cameraData;
            commandBuffer.GetTemporaryRT(TempBlurBuffer1, cameraData.camera.scaledPixelWidth, cameraData.camera.scaledPixelHeight);

            if (Component.activeLut.value && Component.lutAmount.value > 0)
            {
                commandBuffer.Blit(source, TempBlurBuffer1, Material, 0);
                RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
                commandBuffer.GetTemporaryRT(tempCopyString, desc, FilterMode.Bilinear);
                commandBuffer.CopyTexture(source, tempCopy);

                Material.SetFloat(lutAmountString, lutAmount);

                if (lutDimension == 2)
                {
                    Material.SetTexture(lutTexture2DString, converted2D);
                }
                else
                {
                    Material.SetTexture(lutTexture3DString, converted3D);
                }
                commandBuffer.Blit(TempBlurBuffer1, dest, Material, 2 * (lutDimension - 2) + isLinear + 1);
            }
            else
            {
                commandBuffer.Blit(source, dest, Material, 0);
            }
            commandBuffer.ReleaseTemporaryRT(TempBlurBuffer1);
        }

        protected override bool IsActive()
        {
            return Component.IsActive;
        }
        #region LUTFunction
        private void isConverted()
        {
            if (previousLutDimension != lutDimension)
            {
                previousLutDimension = lutDimension;
                Convert(current);
                return;
            }

            if (current != previous)
            {
                previous = current;
                Convert(current);
            }
        }
        private void Convert(Texture2D source)
        {
            if (lutDimension == 2)
            {
                Convert2D(source);
            }
            else
            {
                Convert3D(source);
            }
        }
        private void Convert2D(Texture2D temp2DTex)
        {
            Color[] color = temp2DTex.GetPixels();
            Color[] newCol = new Color[65536];

            for (int i = 0; i < 16; i++)
                for (int j = 0; j < 16; j++)
                    for (int x = 0; x < 16; x++)
                        for (int y = 0; y < 16; y++)
                        {
                            float bChannel = (i + j * 16.0f) / 16;
                            int bchIndex0 = Mathf.FloorToInt(bChannel);
                            int bchIndex1 = Mathf.Min(bchIndex0 + 1, 15);
                            float lerpFactor = bChannel - bchIndex0;
                            int index = x + (15 - y) * 256;
                            Color col1 = color[index + bchIndex0 * 16];
                            Color col2 = color[index + bchIndex1 * 16];

                            newCol[x + i * 16 + y * 256 + j * 4096] =
                                Color.Lerp(col1, col2, lerpFactor);
                        }
            if (converted2D)
                Object.DestroyImmediate(converted2D);

            converted2D = new Texture2D(256, 256, TextureFormat.ARGB32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp
            };

            converted2D.SetPixels(newCol);
            converted2D.Apply();
        }

        private void Convert3D(Texture2D temp3DTex)
        {
            var color = temp3DTex.GetPixels();
            var newCol = new Color[color.Length];

            for (int i = 0; i < 16; i++)
            {
                for (int j = 0; j < 16; j++)
                {
                    for (int k = 0; k < 16; k++)
                    {
                        int val = 16 - j - 1;
                        newCol[i + (j * 16) + (k * 256)] = color[k * 16 + i + val * 256];
                    }
                }
            }
            if (converted3D)
                Object.DestroyImmediate(converted3D);
            converted3D = new Texture3D(16, 16, 16, TextureFormat.ARGB32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp
            };
            converted3D.SetPixels(newCol);
            converted3D.Apply();
        }
        #endregion
    }
}