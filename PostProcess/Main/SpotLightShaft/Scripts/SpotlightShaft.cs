using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TK.Rendering.PostFX
{
    [RequireComponent(typeof(Light))]
    [ExecuteAlways]
    public class SpotlightShaft : MonoBehaviour
    {
		public const int Capacity = 128;

		private static readonly HashSet<Light> Spotlights = new HashSet<Light>();
		private static NativeArray<SpotlightInfo> spotlightInfos;
		private static NativeArray<float3> boundsMinMax;

		public static Mesh BoxMesh { get; private set; }

		private new Light light;

		private void OnEnable()
		{
			AllocateResources();
			this.light = this.GetComponent<Light>();
			this.light!.type = LightType.Spot;
			Spotlights!.Add(this.light);
		}

		private void OnDisable()
		{
			Spotlights!.Remove(this.light);
			if (Spotlights.Count == 0)
			{
				DisposeResources();
			}
		}

		public static int GatherActiveSpotlights(
			out NativeArray<SpotlightInfo> spotlights,
			out float3 boundsMin,
			out float3 boundsMax)
		{
			var count = 0;
			foreach (var light in Spotlights!.Where(l => (l != null) && l.isActiveAndEnabled && (l.type == LightType.Spot)).Take(Capacity))
			{
				var t = light!.transform;
				spotlightInfos[count] = new SpotlightInfo
				{
					Position = (Vector4)t.position,
					Y = (Vector4)(t.up * (light.range * math.tan(math.radians(light.spotAngle * 0.5f)))),
					Z = (Vector4)(t.forward * light.range),
					Intensity = math.float4((Vector3)(Vector4)light.color, 1.0f) * light.intensity
				};
				count++;
			}

			if (count > 0)
			{
				new CalculateBoundsJob
				{
					Count = count,
					Infos = spotlightInfos,
					MinMax = boundsMinMax
				}.Schedule().Complete();
				boundsMin = boundsMinMax[0];
				boundsMax = boundsMinMax[1];
			}
			else
			{
				boundsMin = float3.zero;
				boundsMax = float3.zero;
			}

			spotlights = spotlightInfos;
			return count;
		}

		private static void AllocateResources()
		{
			if (!spotlightInfos.IsCreated)
			{
				spotlightInfos = new NativeArray<SpotlightInfo>(Capacity, Allocator.Persistent);
			}

			if (!boundsMinMax.IsCreated)
			{
				boundsMinMax = new NativeArray<float3>(2, Allocator.Persistent);
			}

			if (BoxMesh == null)
			{
				BoxMesh = CoreUtils.CreateCubeMesh(Vector3.zero, Vector3.one);
				BoxMesh!.hideFlags = HideFlags.HideAndDontSave;
			}
		}

		private static void DisposeNativeArray<T>(ref NativeArray<T> nativeArray) where T : struct
		{
			if (nativeArray.IsCreated)
			{
				nativeArray.Dispose();
			}
		}

		private static void DisposeResources()
		{
			DisposeNativeArray(ref spotlightInfos);
			DisposeNativeArray(ref boundsMinMax);
			CoreUtils.Destroy(BoxMesh);
		}

		[BurstCompile]
		private struct CalculateBoundsJob : IJob
		{
			public int Count;
			[ReadOnly] public NativeArray<SpotlightInfo> Infos;
			[WriteOnly] public NativeArray<float3> MinMax;

			public void Execute()
			{
				var boundsMin = math.float3(float.PositiveInfinity);
				var boundsMax = math.float3(float.NegativeInfinity);
				for (var i = 0; i < this.Count; i++)
				{
					var info = this.Infos[i];
					var y = info.Y.xyz;
					var z = info.Z.xyz;
					var m = math.float3x3(math.cross(y, z) / math.length(z), y, z);
					for (var sx = -1; sx <= 1; sx += 2)
					{
						for (var sy = -1; sy <= 1; sy += 2)
						{
							for (var sz = 0; sz <= 1; sz++)
							{
								var p = info.Position.xyz + math.mul(m, math.float3(sx, sy, sz));
								boundsMin = math.min(boundsMin, p);
								boundsMax = math.max(boundsMax, p);
							}
						}
					}
				}

				this.MinMax[0] = boundsMin;
				this.MinMax[1] = boundsMax;
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct SpotlightInfo
		{
			public float4 Position;
			public float4 Y;
			public float4 Z;
			public float4 Intensity;
		}
	}
}
