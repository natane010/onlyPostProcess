using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TK.Rendering.PostFX
{
    [System.Serializable, VolumeComponentMenu("TK/SSPR")]
    public class MobileSSPR : VolumeComponent
    {
        [Header("Settings")]
        public BoolParameter ShouldRenderSSPR = new BoolParameter(true);
        public FloatParameter HorizontalReflectionPlaneHeightWS = new FloatParameter(0.01f); 
        public ClampedFloatParameter FadeOutScreenBorderWidthVerticle = 
            new ClampedFloatParameter(0.25f, 0.01f, 1f);
        public ClampedFloatParameter FadeOutScreenBorderWidthHorizontal = 
            new ClampedFloatParameter(0.35f, 0.01f, 1f);
        public ClampedFloatParameter ScreenLRStretchIntensity = 
            new ClampedFloatParameter(4, 0f, 8f);
        public ClampedFloatParameter ScreenLRStretchThreshold = new ClampedFloatParameter(0.7f, -1f, 1f);
        [ColorUsage(true, true)]
        public ColorParameter TintColor = new ColorParameter(Color.white);

        public bool IsActive() => ShouldRenderSSPR.value;
    }
}
