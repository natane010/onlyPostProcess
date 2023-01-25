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

        [Header("絞り")]
        public ClampedFloatParameter aperturePri = new ClampedFloatParameter(1, 0.8f, 32);
        [Header("Focus広さ")]
        public ClampedFloatParameter focalLength = new ClampedFloatParameter(50f, 8f, 500f);
        [Header("前ボケを消すか否か")]
        public BoolParameter enableFrontBlur = new BoolParameter(false);
        [Header("最大ボケ")]
        public ClampedFloatParameter maxBlur = new ClampedFloatParameter(0.7f, 0.0001f, 2f);
        [Header("フォーカス位置")]
        public ClampedFloatParameter focusDistance = new ClampedFloatParameter(0.3f, 0.3f, 100f);
        [Header("ボケ強度")]
        public ClampedFloatParameter bokehIntensity = new ClampedFloatParameter(0.7f, 0.0001f, 2f);
        [Header("ボケ回転リング")]
        public ClampedIntParameter bokehRings = new ClampedIntParameter(5, 1, 8);
        [Header("ボケのエッジ数")]
        public ClampedIntParameter apertureEdgeCount = new ClampedIntParameter(5, 1, 8);
        [Header("ボケリングのサンプル数")]
        public ClampedIntParameter dofSamplesPerRing = new ClampedIntParameter(4, 1, 8);
        [Header("ボケエッジのサンプル数")]
        public ClampedIntParameter dofSamplesPerEdge = new ClampedIntParameter(2, 1, 8);

        public bool IsActive => isActivation.value;
    }
}
