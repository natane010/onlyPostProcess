using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TK.Rendering.PostFX
{
    [System.Serializable, VolumeComponentMenu("TK/ColorCalibrationVolume")]
    public class ColorScale : VolumeComponent
    {
        public BoolParameter isActivation = new BoolParameter(false);
        public ColorParameter color = new ColorParameter(Color.white);
        public ClampedIntParameter ColorStentNumber = new ClampedIntParameter(255, 0, 255);
        [ColorUsage(true, false)]
        public ColorParameter gamma = new ColorParameter(Color.white);
        [ColorUsage(true, false)]
        public ColorParameter lift = new ColorParameter(Color.white);
        [ColorUsage(true, false)]
        public ColorParameter gain = new ColorParameter(Color.black);
        [ColorUsage(true, false)]
        public ClampedFloatParameter hue = new ClampedFloatParameter(0f, 0f, 1f);
        [ColorUsage(true, false)]
        public ClampedFloatParameter sat = new ClampedFloatParameter(1f, 0f, 1f);
        [ColorUsage(true, false)]
        public ClampedFloatParameter val = new ClampedFloatParameter(1f, 0f, 1f);
        public ClampedFloatParameter temputure = new ClampedFloatParameter(0, -1f, 1f);
        public BoolParameter reversephotograph = new BoolParameter(false);
        public ClampedFloatParameter photographAmount = new ClampedFloatParameter(0f, 0f, 10f);
        public BoolParameter activeLut = new BoolParameter(false);
        public ClampedIntParameter lutDimension = new ClampedIntParameter(2, 2, 3);
        public ClampedFloatParameter lutAmount = new ClampedFloatParameter(1f, 0f, 1f);
        public TextureParameter sourceLut = new TextureParameter(null);
        public ClampedIntParameter LUTStentNumber = new ClampedIntParameter(0, 0, 255);
        
        public bool IsActive => isActivation.value;
    }
}
