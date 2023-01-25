using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TK.Rendering.PostFX
{
	public class SpotlightsShaft : ScriptableRendererFeature
	{
		[HideInInspector] private Shader volumetricSpotlightsShader;
		private Material material;
		private Pass pass;
		[Range(1, 100)]
		public int lightFarQuality = 1;
		private static readonly int LightsCountProperty = Shader.PropertyToID("_LightsCount");
		private static readonly int LightActiveLength = Shader.PropertyToID("_LightFarSamplerCount");

		private bool GetMaterial()
		{
			if (this.material != null)
			{
				return true;
			}

			if (this.volumetricSpotlightsShader == null)
			{
				this.volumetricSpotlightsShader = Shader.Find("PostFx/SpotLightShaft");
			}

			this.material = CoreUtils.CreateEngineMaterial(this.volumetricSpotlightsShader);
			return this.material != null;
		}

		public override void Create()
		{
			this.pass = new Pass(lightFarQuality);
			this.GetMaterial();
		}

		protected override void Dispose(bool disposing)
		{
			CoreUtils.Destroy(this.material);
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if (!this.GetMaterial())
			{
				return;
			}

			this.pass!.Material = this.material;
			renderer!.EnqueuePass(this.pass);
		}

		private class Pass : ScriptableRenderPass
		{
			public Material Material;
			int lightLevel;

			public Pass(int level)
			{
				this.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
				lightLevel = level;
			}

			public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
			{
				var count = SpotlightShaft.GatherActiveSpotlights(
					out _,
					out var boundsMin,
					out var boundsMax);
				if (count <= 0)
				{
					return;
				}

				this.Material!.SetInt(LightsCountProperty, renderingData.lightData.additionalLightsCount);
				this.Material!.SetInt(LightActiveLength, lightLevel);
				var cmd = CommandBufferPool.Get();
				cmd!.Clear();
				cmd.DrawMesh(
					SpotlightShaft.BoxMesh,
					Matrix4x4.TRS(boundsMin, Quaternion.identity, boundsMax - boundsMin),
					this.Material);
				context.ExecuteCommandBuffer(cmd);
				CommandBufferPool.Release(cmd);
			}
		}
	}
}
