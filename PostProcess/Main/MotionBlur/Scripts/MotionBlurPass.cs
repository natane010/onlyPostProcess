using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TK.Rendering.PostFX
{
    public class MotionVectorPass : CustomPostProcessingPass<MotionBlur>
    {
        private const string PASS_NAME = "newMotionVector";
        private const string PRE_PASS_NAME = "Copy Depth";

        private ShaderTagId SHADER_TAG_FORWARD = new ShaderTagId("UniversalForward");
        private static readonly int MOTION_TEXTURE = Shader.PropertyToID("_CameraMotionVectorsTexture");
        private static readonly int PROP_VPMATRIX = Shader.PropertyToID("_NonJitteredVP");
        private static readonly int PROP_PREV_VPMATRIX = Shader.PropertyToID("_PreviousVP");
        private Matrix4x4 previousVP = Matrix4x4.identity;

        DrawingSettings drawingSettings;
        FilteringSettings filteringSettings;
        RenderStateBlock renderStateBlock;
        LayerMask targetLayerMask;

        public MotionVectorPass(RenderPassEvent renderPassEvent, Shader shader, LayerMask layerMask) : base(renderPassEvent, shader) 
        {
            targetLayerMask = layerMask;
        }

        protected override string RenderTag => "MotionVector";
        protected override void BeforeRender(CommandBuffer commandBuffer, ref RenderingData renderingData)
        {
            
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (Material == null || !renderingData.cameraData.postProcessEnabled)
            {
                return;
            }

            var volumeStack = VolumeManager.instance.stack;
            Component = volumeStack.GetComponent<MotionBlur>();
            if (Component == null || !Component.active || !IsActive())
            {
                return;
            }

            var commandBuffer = CommandBufferPool.Get(RenderTag);

#if UNITY_EDITOR || DEBUG
            if (renderingData.cameraData.isSceneViewCamera || !Application.isPlaying)
                return;
            if (renderingData.cameraData.camera.cameraType == CameraType.Preview)
                return;
            if (Material == null)
                return;
#endif
            ProfilingSampler profilingSampler = new ProfilingSampler(PASS_NAME);
            using (new ProfilingScope(commandBuffer, profilingSampler))
            {
#if UNITY_EDITOR || DEBUG
                context.ExecuteCommandBuffer(commandBuffer);
#endif
                commandBuffer.Clear();

                var camera = renderingData.cameraData.camera;
                camera.depthTextureMode |= DepthTextureMode.MotionVectors | DepthTextureMode.Depth;

#if UNITY_EDITOR || DEBUG
                CommandBuffer preCmd = CommandBufferPool.Get(PRE_PASS_NAME);
                preCmd.Clear();
#else
                CommandBuffer preCmd = CommandBufferPool.Get(PRE_PASS_NAME);
                preCmd.Clear();
#endif
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                preCmd.GetTemporaryRT(MOTION_TEXTURE, descriptor.width, descriptor.height, 32, FilterMode.Point, RenderTextureFormat.RGHalf);

                this.Blit(preCmd, BuiltinRenderTextureType.None, MOTION_TEXTURE, this.Material, 1);
                context.ExecuteCommandBuffer(preCmd);
#if UNITY_EDITOR || DEBUG
                CommandBufferPool.Release(preCmd);
#endif

                var proj = camera.nonJitteredProjectionMatrix;
                var view = camera.worldToCameraMatrix;
                var viewProj = proj * view;
                this.Material.SetMatrix(PROP_VPMATRIX, viewProj);
                this.Material.SetMatrix(PROP_PREV_VPMATRIX, this.previousVP);
                this.previousVP = viewProj;

                drawingSettings = this.CreateDrawingSettings(SHADER_TAG_FORWARD, ref renderingData, SortingCriteria.CommonOpaque);
                drawingSettings.overrideMaterial = this.Material;
                drawingSettings.overrideMaterialPassIndex = 0;
                drawingSettings.perObjectData |= PerObjectData.MotionVectors;
                filteringSettings = new FilteringSettings(RenderQueueRange.opaque, targetLayerMask);
                renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);
            }

#if UNITY_EDITOR || DEBUG
            // for FrameDebugger
            context.ExecuteCommandBuffer(commandBuffer);
#endif
            CommandBufferPool.Release(commandBuffer);
        }
        protected override bool IsActive()
        {
            return Component.IsActive;
        }
    }
    public class MotionBlurPass : CustomPostProcessingPass<MotionBlur>
    {
        private enum Pass
        {
            VelocitySetup,
            TileMax1,
            TileMax2,
            TileMaxV,
            NeighborMax,
            Reconstruction
        }

        const string PASS_NAME = "newMotionBlur";
        private Mesh triangle;
        private bool isSupportedFloatBuffer = false;

        RenderTargetIdentifier colorIdentifier;

        private static readonly int MAIN_TEX = Shader.PropertyToID("_MainTex");
        private static readonly int TEMP_COLOR_TEXTURE = Shader.PropertyToID("Temp Color Buffer");
        private static readonly int TEMP_DEPTH_TEXTURE = Shader.PropertyToID("Temp Depth Buffer");
        private static readonly int MOTION_TEXTURE = Shader.PropertyToID("_CameraMotionVectorsTexture");
        private static readonly int VelocityScale = Shader.PropertyToID("_VelocityScale");
        private static readonly int MaxBlurRadius = Shader.PropertyToID("_MaxBlurRadius");
        private static readonly int RcpMaxBlurRadius = Shader.PropertyToID("_RcpMaxBlurRadius");
        private static readonly int VelocityTex = Shader.PropertyToID("_VelocityTex");
        private static readonly int Tile2RT = Shader.PropertyToID("_Tile2RT");
        private static readonly int Tile4RT = Shader.PropertyToID("_Tile4RT");
        private static readonly int Tile8RT = Shader.PropertyToID("_Tile8RT");
        private static readonly int TileMaxOffs = Shader.PropertyToID("_TileMaxOffs");
        private static readonly int TileMaxLoop = Shader.PropertyToID("_TileMaxLoop");
        private static readonly int TileVRT = Shader.PropertyToID("_TileVRT");
        private static readonly int NeighborMaxTex = Shader.PropertyToID("_NeighborMaxTex");
        private static readonly int LoopCount = Shader.PropertyToID("_LoopCount");

        public override void Setup(in RenderTargetIdentifier renderTargetIdentifier)
        {
            colorIdentifier = renderTargetIdentifier;
        }

        public MotionBlurPass(RenderPassEvent renderPassEvent, Shader shader) : base(renderPassEvent, shader) 
        {
            this.triangle = new Mesh();
            this.triangle.name = "Fullscreen Triangle";
            this.triangle.SetVertices(new[] {
                    new Vector3(-1f, -1f, 0f),
                    new Vector3(-1f,  3f, 0f),
                    new Vector3( 3f, -1f, 0f)
                    });
            this.triangle.SetIndices(new[] { 0, 1, 2 }, MeshTopology.Triangles, 0, false);
            this.triangle.UploadMeshData(true);
        }

        protected override string RenderTag => "MotionBlur";
        protected override void BeforeRender(CommandBuffer commandBuffer, ref RenderingData renderingData)
        {

        }

        protected override void Render(CommandBuffer commandBuffer, ref RenderingData renderingData, RenderTargetIdentifier source, RenderTargetIdentifier dest)
        {
#if UNITY_EDITOR || DEBUG
            if (renderingData.cameraData.isSceneViewCamera || !Application.isPlaying)
                return;
            if (renderingData.cameraData.camera.cameraType == CameraType.Preview)
                return;
            if (Material == null)
                return;
#endif
            ProfilingSampler profilingSampler = new ProfilingSampler(PASS_NAME);
            using (new ProfilingScope(commandBuffer, profilingSampler))
            {
                commandBuffer.Clear();
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                int width = descriptor.width;
                int height = descriptor.height;
                ConfigureTarget(colorIdentifier, depthAttachment);

                commandBuffer.GetTemporaryRT(TEMP_COLOR_TEXTURE, width, height, 0, FilterMode.Bilinear, descriptor.colorFormat);
                commandBuffer.CopyTexture(colorIdentifier, new RenderTargetIdentifier(TEMP_COLOR_TEXTURE));

                commandBuffer.SetGlobalTexture("_MainTex", this.colorAttachment);
                commandBuffer.Blit(this.colorAttachment, TEMP_COLOR_TEXTURE);

                float shutterAngle = Component.BlurAmount.value * 1000;
                int sampleCount = (int)Component.Quality.value;

                const float kMaxBlurRadius = 5f;
                var vectorRTFormat = RenderTextureFormat.RGHalf;
                var packedRTFormat = this.isSupportedFloatBuffer
                    ? RenderTextureFormat.ARGB2101010
                    : RenderTextureFormat.ARGB32;

                int maxBlurPixels = (int)(kMaxBlurRadius * height / 100);

                int tileSize = ((maxBlurPixels - 1) / 8 + 1) * 8;

                var velocityScale = shutterAngle / 360f;
                Material.SetFloat(VelocityScale, velocityScale);
                Material.SetFloat(MaxBlurRadius, maxBlurPixels);
                Material.SetFloat(RcpMaxBlurRadius, 1f / maxBlurPixels);

                int vbuffer = VelocityTex;
                commandBuffer.GetTemporaryRT(vbuffer, width, height, 0, FilterMode.Point,
                    packedRTFormat, RenderTextureReadWrite.Linear);
                this.BlitFullscreenTriangle(commandBuffer, BuiltinRenderTextureType.None, vbuffer, (int)Pass.VelocitySetup);

                int tile2 = Tile2RT;
                commandBuffer.GetTemporaryRT(tile2, width / 2, height / 2, 0, FilterMode.Point,
                    vectorRTFormat, RenderTextureReadWrite.Linear);
                this.BlitFullscreenTriangle(commandBuffer, vbuffer, tile2, (int)Pass.TileMax1);

                int tile4 = Tile4RT;
                commandBuffer.GetTemporaryRT(tile4, width / 4, height / 4, 0, FilterMode.Point,
                    vectorRTFormat, RenderTextureReadWrite.Linear);
                this.BlitFullscreenTriangle(commandBuffer, tile2, tile4, (int)Pass.TileMax2);
                commandBuffer.ReleaseTemporaryRT(tile2);

                int tile8 = Tile8RT;
                commandBuffer.GetTemporaryRT(tile8, width / 8, height / 8, 0, FilterMode.Point,
                    vectorRTFormat, RenderTextureReadWrite.Linear);
                this.BlitFullscreenTriangle(commandBuffer, tile4, tile8, (int)Pass.TileMax2);
                commandBuffer.ReleaseTemporaryRT(tile4);

                var tileMaxOffs = Vector2.one * (tileSize / 8f - 1f) * -0.5f;
                Material.SetVector(TileMaxOffs, tileMaxOffs);
                Material.SetFloat(TileMaxLoop, (int)(tileSize / 8f));

                int tile = TileVRT;
                commandBuffer.GetTemporaryRT(tile, width / tileSize, height / tileSize, 0,
                    FilterMode.Point, vectorRTFormat, RenderTextureReadWrite.Linear);
                this.BlitFullscreenTriangle(commandBuffer, tile8, tile, (int)Pass.TileMaxV);
                commandBuffer.ReleaseTemporaryRT(tile8);

                int neighborMax = NeighborMaxTex;
                int neighborMaxWidth = width / tileSize;
                int neighborMaxHeight = height / tileSize;
                commandBuffer.GetTemporaryRT(neighborMax, neighborMaxWidth, neighborMaxHeight, 0,
                    FilterMode.Point, vectorRTFormat, RenderTextureReadWrite.Linear);
                this.BlitFullscreenTriangle(commandBuffer, tile, neighborMax, (int)Pass.NeighborMax);
                commandBuffer.ReleaseTemporaryRT(tile);

                Material.SetFloat(LoopCount, Mathf.Clamp(sampleCount / 2, 1, 64));
                this.BlitFullscreenTriangle(commandBuffer, TEMP_COLOR_TEXTURE, this.colorAttachment, (int)Pass.Reconstruction);

                commandBuffer.ReleaseTemporaryRT(vbuffer);
                commandBuffer.ReleaseTemporaryRT(neighborMax);

                commandBuffer.ReleaseTemporaryRT(TEMP_COLOR_TEXTURE);

                commandBuffer.ReleaseTemporaryRT(MOTION_TEXTURE);
                commandBuffer.SetRenderTarget(this.colorAttachment, this.depthAttachment);
            }
        }
        private void BlitFullscreenTriangle(CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination, int pass)
        {
            cmd.SetGlobalTexture(MAIN_TEX, source);
            RenderBufferLoadAction loadAction = RenderBufferLoadAction.DontCare;
            cmd.SetRenderTarget(destination, loadAction, RenderBufferStoreAction.Store);
            cmd.DrawMesh(this.triangle, Matrix4x4.identity, Material, 0, pass);
        }

        protected override bool IsActive()
        {
            return Component.IsActive;
        }
    }
}
