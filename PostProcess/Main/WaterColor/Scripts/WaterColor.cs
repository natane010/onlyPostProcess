using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TK.Rendering.PostFX
{
    [Serializable, VolumeComponentMenu("TK/WaterColor")]
    public class WaterColor : VolumeComponent
    {
        public BoolParameter isActivation = new BoolParameter(false);

        public ClampedIntParameter wobbStent = new ClampedIntParameter(255, 0, 255);

        public BoolParameter isToneActive = new BoolParameter(true);
        public ColorParameter warm = new ColorParameter(Color.black);
        public ColorParameter cool = new ColorParameter(Color.black);
        public FloatParameter tonePower = new FloatParameter(1f);
        public Vector4Parameter dirLight = new Vector4Parameter(Vector4.zero);

        public BoolParameter isPaperActive = new BoolParameter(true);
        public Texture2DParameter paperTex = new Texture2DParameter(null);
        public FloatParameter paperScale = new FloatParameter(1f);
        public FloatParameter paperPow = new FloatParameter(1f);

        public Texture2DParameter wobbTex = new Texture2DParameter(null);
        public FloatParameter wobbScale = new FloatParameter(1f);
        public ClampedFloatParameter wobbPower = new ClampedFloatParameter(0.01f, 0, 0.05f);
        public FloatParameter edgeSize  = new FloatParameter(1f);
        public FloatParameter edgePower = new FloatParameter(3f);
        public bool IsActive => isActivation.value;
    }

}
