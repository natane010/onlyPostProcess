using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TK.Rendering.PostFX
{
    public class DepthOfFieldPass : CustomPostProcessingPass<DepthOfField>
    {
        readonly static int _Input = Shader.PropertyToID("_MainTex");
        readonly static int _Aperture = Shader.PropertyToID("fAperture");
        readonly static int _Secondary = Shader.PropertyToID("_SecondaryTex");
        readonly static int _Tertiary = Shader.PropertyToID("_DoFTex");
        readonly static int _FocalLength = Shader.PropertyToID("fFocalLength");
        readonly static int _Threshold = Shader.PropertyToID("BokehDoFTreshold");


        readonly static int _DoFMaxBlur = Shader.PropertyToID("DoFMaxBlur");
        readonly static int _BokehIntensity = Shader.PropertyToID("BokehDoFIntensity");
        readonly static int _DoFRings = Shader.PropertyToID("DoFRings");
        readonly static int _ApertureEdgeCount = Shader.PropertyToID("BokehDoFEdgeCount");
        readonly static int _DoFSamplesPerRing = Shader.PropertyToID("DoFSamplesPerRing");
        readonly static int _BokehDoFSamplePerEdge = Shader.PropertyToID("BokehDoFSamplePerEdge");
        readonly static int _DofFocusDist = Shader.PropertyToID("DofFocusDist");
        readonly static int _DofSensorSize = Shader.PropertyToID("DofSensorSize");

        readonly static int _ConstantBufferName = Shader.PropertyToID("_MyCustomBuffer");

        private static readonly int _TempBlurBuffer1 = Shader.PropertyToID("_TempBlurBuffer1");

        private RenderTextureDescriptor m_IntermediateDesc;
        private RenderTargetHandle m_Intermediate;
        private RenderTargetHandle m_SourceHandle;
        private RenderTargetHandle m_intermediateHandle;
        bool m_IntermediateAllocated;

        public override void Setup(in RenderTargetIdentifier renderTargetIdentifier)
        {
            base.Setup(renderTargetIdentifier);

            m_intermediateHandle = new RenderTargetHandle();
            m_intermediateHandle.Init("_IntermediateTex");

            m_SourceHandle = new RenderTargetHandle();
            m_SourceHandle.Init("_MainTex");
        }

        public DepthOfFieldPass(RenderPassEvent renderPassEvent, Shader shader) : base(renderPassEvent, shader)
        {

        }

        protected override string RenderTag => "DepthOfField";
        protected override void BeforeRender(CommandBuffer commandBuffer, ref RenderingData renderingData)
        {
            Material.SetFloat(_Aperture, Component.aperturePri.value);
            Material.SetFloat(_BokehIntensity, Component.bokehIntensity.value);
            Material.SetFloat(_DoFMaxBlur, Component.maxBlur.value);
            Material.SetFloat(_DofFocusDist, Component.focusDistance.value);

            if (Component.enableFrontBlur.value == true)
            {
                Material.EnableKeyword("DOF_FRONTBLUR");
            }
            else
            {
                Material.DisableKeyword("DOF_FRONTBLUR");
            }

            //PhysicalCamera Ç≈Ç∏ÇÍÇÈÇÕÇ∏ÇÃïîï™ÇÉJÉÅÉâÇÃíPèÉåvéZÇ≈êßå‰
            Material.SetFloat(_FocalLength, Component.focalLength.value * 0.01f);
            Material.SetFloat(_DofSensorSize, 0.024f);

            Material.SetInt(_DoFSamplesPerRing, Component.dofSamplesPerRing.value);
            Material.SetInt(_DoFRings, Component.bokehRings.value);
            Material.SetInt(_ApertureEdgeCount, Component.apertureEdgeCount.value);
            Material.SetInt(_BokehDoFSamplePerEdge, Component.dofSamplesPerEdge.value);

            Material.DisableKeyword("DOFAF");
        }

        protected override void Render(CommandBuffer commandBuffer, ref RenderingData renderingData, RenderTargetIdentifier source, RenderTargetIdentifier dest)
        {
            m_IntermediateDesc = renderingData.cameraData.cameraTargetDescriptor;
            m_IntermediateDesc.msaaSamples = 1;
            m_IntermediateDesc.depthBufferBits = 0;

            int width = m_IntermediateDesc.width;
            int height = m_IntermediateDesc.height;
            commandBuffer.SetGlobalVector("_ScreenSize", new Vector4(width, height, 1.0f / width, 1.0f / height));

            RenderTargetIdentifier sourceIden, destinationIden;
            sourceIden = source;
            destinationIden = dest;
            dest = GetIntermediate(commandBuffer);
            ProfilingSampler profilingSampler = new ProfilingSampler(RenderTag);
            using (new ProfilingScope(commandBuffer, profilingSampler))
            {
                RenderDofVeryHigh(commandBuffer, sourceIden, destinationIden, ref renderingData);
            }
            commandBuffer.Blit(m_Intermediate.Identifier(), dest);
            if (m_IntermediateAllocated)
            {
                commandBuffer.ReleaseTemporaryRT(m_Intermediate.id);
                m_IntermediateAllocated = false;
            }

        }

        protected override bool IsActive()
        {
            return Component.IsActive;
        }
        private RenderTargetIdentifier GetIntermediate(CommandBuffer cmd)
        {
            if (!m_IntermediateAllocated)
            {
                cmd.GetTemporaryRT(m_Intermediate.id, m_IntermediateDesc);
                m_IntermediateAllocated = true;
            }
            return m_Intermediate.Identifier();
        }
        public static RenderTextureDescriptor GetTempRTDescriptor(in RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            return descriptor;
        }

        public void RenderDofVeryHigh(CommandBuffer cmd, RenderTargetIdentifier source, 
            RenderTargetIdentifier destination, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptorOne = GetTempRTDescriptor(renderingData);
            cmd.GetTemporaryRT(m_SourceHandle.id, descriptorOne);
            cmd.Blit(source, _Input);
            cmd.SetGlobalTexture(_Input, m_SourceHandle.id);

            RenderTextureDescriptor dofDescriptor = GetTempRTDescriptor(renderingData);

            int combinePass = 1;
            int prePass = 0;

            cmd.GetTemporaryRT(m_intermediateHandle.id, dofDescriptor, FilterMode.Bilinear);
            cmd.Blit(m_SourceHandle.id, m_intermediateHandle.Identifier(), Material, prePass);

            //1 = combine
            cmd.SetGlobalTexture(_Secondary, m_intermediateHandle.id);

            cmd.Blit(m_SourceHandle.id, destination, Material, combinePass);

            cmd.ReleaseTemporaryRT(m_intermediateHandle.id);
            cmd.ReleaseTemporaryRT(m_SourceHandle.id);
        }
    }
}
