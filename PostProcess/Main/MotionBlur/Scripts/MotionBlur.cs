using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TK.Rendering.PostFX
{
    public enum MotionBlurQuality
    {
        Low = 4,
        Standard = 12,
        Middle = 16,
        High = 32,
    }
    [System.Serializable]
    public sealed class MotionBlurQualityParameter : VolumeParameter<MotionBlurQuality> { }

    [System.Serializable, VolumeComponentMenu("TK/MotionBlur")]
    public sealed class MotionBlur : VolumeComponent
    {
        public BoolParameter isActivation = new BoolParameter(false);

        [Tooltip("���x")]
        public ClampedFloatParameter BlurAmount = new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("�N�I���e�B")]
        public MotionBlurQualityParameter Quality =
            new MotionBlurQualityParameter { value = MotionBlurQuality.Standard };


        public bool IsActive => isActivation.value;
    }
}