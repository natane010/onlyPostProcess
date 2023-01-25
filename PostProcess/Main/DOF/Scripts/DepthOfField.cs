using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TK.Rendering.PostFX
{
    [System.Serializable, VolumeComponentMenu("TK/DepthOfField")]
    public class DepthOfField : VolumeComponent
    {
        public BoolParameter isActivation = new BoolParameter(false);

        [Header("�i��")]
        public ClampedFloatParameter aperturePri = new ClampedFloatParameter(1, 0.8f, 32);
        [Header("Focus�L��")]
        public ClampedFloatParameter focalLength = new ClampedFloatParameter(50f, 8f, 500f);
        [Header("�O�{�P���������ۂ�")]
        public BoolParameter enableFrontBlur = new BoolParameter(false);
        [Header("�ő�{�P")]
        public ClampedFloatParameter maxBlur = new ClampedFloatParameter(0.7f, 0.0001f, 2f);
        [Header("�t�H�[�J�X�ʒu")]
        public ClampedFloatParameter focusDistance = new ClampedFloatParameter(0.3f, 0.3f, 100f);
        [Header("�{�P���x")]
        public ClampedFloatParameter bokehIntensity = new ClampedFloatParameter(0.7f, 0.0001f, 2f);
        [Header("�{�P��]�����O")]
        public ClampedIntParameter bokehRings = new ClampedIntParameter(5, 1, 8);
        [Header("�{�P�̃G�b�W��")]
        public ClampedIntParameter apertureEdgeCount = new ClampedIntParameter(5, 1, 8);
        [Header("�{�P�����O�̃T���v����")]
        public ClampedIntParameter dofSamplesPerRing = new ClampedIntParameter(4, 1, 8);
        [Header("�{�P�G�b�W�̃T���v����")]
        public ClampedIntParameter dofSamplesPerEdge = new ClampedIntParameter(2, 1, 8);

        public bool IsActive => isActivation.value;
    }
}
