using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TK.Rendering.PostFX
{
    [System.Serializable, VolumeComponentMenu("TK/GradientFog")]
    public class GradientFog : VolumeComponent
    {
        public TextureParameter gradientTexture = new TextureParameter(null);

        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

        public bool IsActive => intensity.value > 0f;
    }
}