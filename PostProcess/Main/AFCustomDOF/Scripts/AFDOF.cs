using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TK.Rendering.PostFX
{
    [System.Serializable, VolumeComponentMenu("TK/AFDOF")]
    public class AFDOF : VolumeComponent
    {
        [AttributeUsage(AttributeTargets.Field)]
        public class SectionGroup : Attribute
        {
        }

        [AttributeUsage(AttributeTargets.Field)]
        public class SettingsGroup : Attribute
        {

            bool? expanded;

            public bool IsExpanded
            {
                get
                {
#if UNITY_EDITOR
                    if (!expanded.HasValue)
                    {
                        expanded = UnityEditor.EditorPrefs.GetBool("" + GetType().ToString(), false);
                    }
                    return expanded.Value;
#else
                    return false;
#endif
                }
                set
                {
#if UNITY_EDITOR
                    if (expanded.Value != value)
                    {
                        expanded = value;
                        UnityEditor.EditorPrefs.SetBool("" + GetType().ToString(), value);
                    }
#endif
                }
            }

        }

        public class QualitySettings : SectionGroup { }
        public class EffectSettings : SectionGroup { }
        public class Performance : SettingsGroup { }
        public class ChromaticAberration : SettingsGroup { }
        public class DepthOfField : SettingsGroup { }
        public class FinalBlur : SettingsGroup { }


        [AttributeUsage(AttributeTargets.Field)]
        public class DisplayName : Attribute
        {
            public string name;

            public DisplayName(string name)
            {
                this.name = name;
            }
        }

        [AttributeUsage(AttributeTargets.Field)]
        public class DisplayConditionEnum : Attribute
        {
            public string field;
            public int enumValueIndex;

            public DisplayConditionEnum(string field, int enumValueIndex)
            {
                this.field = field;
                this.enumValueIndex = enumValueIndex;
            }
        }


        [AttributeUsage(AttributeTargets.Field)]
        public class DisplayConditionBool : Attribute
        {
            public string field;
            public bool value;
            public string field2;
            public bool value2;

            public DisplayConditionBool(string field, bool value = true, string field2 = null, bool value2 = true)
            {
                this.field = field;
                this.value = value;
                this.field2 = field2;
                this.value2 = value2;
            }
        }


        [AttributeUsage(AttributeTargets.Field)]
        public class ToggleAllFields : Attribute
        {
        }


        [AttributeUsage(AttributeTargets.Field)]
        public class GlobalOverride : Attribute
        {
        }

        [AttributeUsage(AttributeTargets.Field)]
        public class BuildToggle : Attribute
        {
        }



        public enum DoFFocusMode
        {
            FixedDistance,
            AutoFocus,
            FollowTarget
        }

        public enum DoFBokehComposition
        {
            Integrated,
            Separated
        }

        public enum DoFCameraSettings
        {
            Low,
            High
        }



        public enum BlinkStyle
        {
            Cutscene,
            Human
        }


        [Serializable]
        public sealed class DoFFocusModeParameter : VolumeParameter<DoFFocusMode> { }

        [Serializable]
        public sealed class DoFFilterModeParameter : VolumeParameter<FilterMode> { }

        [Serializable]
        public sealed class DoFBokehCompositionParameter : VolumeParameter<DoFBokehComposition> { }

        [Serializable]
        public sealed class DoFCameraSettingsParameter : VolumeParameter<DoFCameraSettings> { }

        [Serializable]
        public sealed class LayerMaskParameter : VolumeParameter<LayerMask> { }
        [Serializable]
        public sealed class MinMaxFloatParameter : VolumeParameter<Vector2>
        {
            public float min;
            public float max;

            public MinMaxFloatParameter(Vector2 value, float min, float max, bool overrideState = false)
                : base(value, overrideState)
            {
                this.min = min;
                this.max = max;
            }
        }

        #region  settings

        [QualitySettings, DisplayName("Disable Effects"), GlobalOverride]
        public BoolParameter disabled = new BoolParameter(false, overrideState: true);

        [QualitySettings, HideInInspector]
        public ClampedFloatParameter saturate = new ClampedFloatParameter(1f, -2f, 3f);

        [QualitySettings, HideInInspector]
        public ClampedFloatParameter brightness = new ClampedFloatParameter(1.05f, 0f, 2f);

        [QualitySettings, HideInInspector]
        public ClampedFloatParameter contrast = new ClampedFloatParameter(1.02f, 0.5f, 1.5f);

        [QualitySettings, Performance, DisplayName("Downsampling"), GlobalOverride]
        public BoolParameter downsampling = new BoolParameter(false, overrideState: true);

        [QualitySettings, Performance, DisplayName("Multiplier"), GlobalOverride, DisplayConditionBool("downsampling")]
        public ClampedFloatParameter downsamplingMultiplier = new ClampedFloatParameter(1, 1, 32f);

        [QualitySettings, Performance, DisplayName("Bilinear Filtering"), GlobalOverride, DisplayConditionBool("downsampling")]
        public BoolParameter downsamplingBilinear = new BoolParameter(false);
        #endregion


        #region Chromatic Aberration

        [EffectSettings, ChromaticAberration, DisplayName("Intensity"), Min(0), DisplayConditionBool("stripChromaticAberration", false)]
        public FloatParameter chromaticAberrationIntensity = new ClampedFloatParameter(0f, 0f, 0.05f);

        [EffectSettings, ChromaticAberration, DisplayName("Smoothing")]
        public FloatParameter chromaticAberrationSmoothing = new ClampedFloatParameter(0f, 0f, 32f);

        #endregion


        #region Depth of Field

        [EffectSettings, DepthOfField, DisplayName("Enable"), ToggleAllFields, DisplayConditionBool("stripDoF", false)]
        public BoolParameter depthOfField = new BoolParameter(false);

        [Header("Focus")]
        [EffectSettings, DepthOfField, DisplayName("Focus Mode")]
        public DoFFocusModeParameter depthOfFieldFocusMode = new DoFFocusModeParameter { value = DoFFocusMode.FixedDistance };

        [EffectSettings, DepthOfField, DisplayName("Min Distance"), DisplayConditionEnum("depthOfFieldFocusMode", (int)DoFFocusMode.AutoFocus)]
        public FloatParameter depthOfFieldAutofocusMinDistance = new FloatParameter(0);

        [EffectSettings, DepthOfField, DisplayName("Max Distance"), DisplayConditionEnum("depthOfFieldFocusMode", (int)DoFFocusMode.AutoFocus)]
        public FloatParameter depthOfFieldAutofocusMaxDistance = new FloatParameter(10000);

        [EffectSettings, DepthOfField, DisplayName("Viewport Point"), DisplayConditionEnum("depthOfFieldFocusMode", (int)DoFFocusMode.AutoFocus)]
        public Vector2Parameter depthOfFieldAutofocusViewportPoint = new Vector2Parameter(new Vector2(0.5f, 0.5f));

        [EffectSettings, DepthOfField, DisplayName("Distance Shift"), DisplayConditionEnum("depthOfFieldFocusMode", (int)DoFFocusMode.AutoFocus), Tooltip("Custom distance adjustment (positive or negative)")]
        public FloatParameter depthOfFieldAutofocusDistanceShift = new FloatParameter(0);

        [EffectSettings, DepthOfField, DisplayName("Layer Mask"), DisplayConditionEnum("depthOfFieldFocusMode", (int)DoFFocusMode.AutoFocus)]
        public LayerMaskParameter depthOfFieldAutofocusLayerMask = new LayerMaskParameter { value = -1 };

        [EffectSettings, DepthOfField, DisplayName("Distance"), DisplayConditionEnum("depthOfFieldFocusMode", (int)DoFFocusMode.FixedDistance), Min(0)]
        public FloatParameter depthOfFieldDistance = new FloatParameter(10f);

        [EffectSettings, DepthOfField, DisplayName("Camera Lens Settings")]
        public DoFCameraSettingsParameter depthOfFieldCameraSettings = new DoFCameraSettingsParameter { value = DoFCameraSettings.Low };

        [EffectSettings, DepthOfField, DisplayName("Focal Length"), DisplayConditionEnum("depthOfFieldCameraSettings", 0)]
        public ClampedFloatParameter depthOfFieldFocalLength = new ClampedFloatParameter(0.050f, 0.005f, 0.5f);

        [EffectSettings, DepthOfField, DisplayName("Aperture"), Min(0), DisplayConditionEnum("depthOfFieldCameraSettings", 0)]
        public FloatParameter depthOfFieldAperture = new FloatParameter(2.8f);

        [EffectSettings, DepthOfField, DisplayName("Focal Length"), DisplayConditionEnum("depthOfFieldCameraSettings", 1)]
        [Tooltip("The distance between the lens center and the camera's sensor in mm.")]
        public ClampedFloatParameter depthOfFieldFocalLengthReal = new ClampedFloatParameter(50, 1, 300);

        [EffectSettings, DepthOfField, DisplayName("F-Stop"), DisplayConditionEnum("depthOfFieldCameraSettings", 1)]
        [Tooltip("The F-stop or F-number is the relation between the focal length and the diameter of the aperture")]
        public ClampedFloatParameter depthOfFieldFStop = new ClampedFloatParameter(2, 1, 32);

        [EffectSettings, DepthOfField, DisplayName("Image Sensor Height"), DisplayConditionEnum("depthOfFieldCameraSettings", 1)]
        [Tooltip("Represents the height of the virtual image sensor. By default, it uses a 24mm image sensor of a classic full-frame camera")]
        public ClampedFloatParameter depthOfFieldImageSensorHeight = new ClampedFloatParameter(24, 1, 48);

        [EffectSettings, DepthOfField, DisplayName("Focus Speed")]
        public ClampedFloatParameter depthOfFieldFocusSpeed = new ClampedFloatParameter(1f, 0.001f, 5f);

        [EffectSettings, DepthOfField, DisplayName("EXSettings")]
        public BoolParameter useEXSettings = new BoolParameter(false);

        [EffectSettings, DepthOfField, DisplayName("Transparency Support"), DisplayConditionBool("useEXSettings")]
        public BoolParameter depthOfFieldTransparentSupport = new BoolParameter(false);

        [EffectSettings, DepthOfField, DisplayName("Layer Mask"), DisplayConditionBool("depthOfFieldTransparentSupport")]
        public LayerMaskParameter depthOfFieldTransparentLayerMask = new LayerMaskParameter { value = 1 };

        [EffectSettings, DepthOfField, DisplayName("Double Sided"), DisplayConditionBool("depthOfFieldTransparentSupport")]
        public BoolParameter depthOfFieldTransparentDoubleSided = new BoolParameter(false);

        [EffectSettings, DepthOfField, DisplayName("Transparency Alpha Test Support"), DisplayConditionBool("useEXSettings")]
        public BoolParameter depthOfFieldAlphaTestSupport = new BoolParameter(false);

        [EffectSettings, DepthOfField, DisplayName("Layer Mask"), DisplayConditionBool("depthOfFieldAlphaTestSupport")]
        public LayerMaskParameter depthOfFieldAlphaTestLayerMask = new LayerMaskParameter { value = 1 };

        [EffectSettings, DepthOfField, DisplayName("Double Sided"), DisplayConditionBool("depthOfFieldAlphaTestSupport")]
        [Tooltip("When enabled, transparent geometry is rendered with cull off.")]
        public BoolParameter depthOfFieldAlphaTestDoubleSided = new BoolParameter(false);

        [EffectSettings, DepthOfField, DisplayName("Foreground Blur"), DisplayConditionBool("useEXSettings")]
        public BoolParameter depthOfFieldForegroundBlur = new BoolParameter(true);

        [EffectSettings, DepthOfField, DisplayName("Blur HQ"), DisplayConditionBool("useEXSettings")]
        public BoolParameter depthOfFieldForegroundBlurHQ = new BoolParameter(false);

        [EffectSettings, DepthOfField, DisplayName("Blur Spread"), DisplayConditionBool("useEXSettings")]
        public ClampedFloatParameter depthOfFieldForegroundBlurHQSpread = new ClampedFloatParameter(4, 0, 32);

        [EffectSettings, DepthOfField, DisplayName("Distance"), DisplayConditionBool("useEXSettings")]
        public FloatParameter depthOfFieldForegroundDistance = new FloatParameter(0.25f);

        [EffectSettings, DepthOfField, DisplayName("Bokeh Effect"), DisplayConditionBool("useEXSettings")]
        public BoolParameter depthOfFieldBokeh = new BoolParameter(true);

        [EffectSettings, DepthOfField, DisplayName("Composition"), DisplayConditionBool("depthOfFieldBokeh"), HideInInspector]
        [Tooltip("Specifies if the pass to compute bokeh is integrated (faster) or separated (stronger)")]
        public DoFBokehCompositionParameter depthOfFieldBokehComposition = new DoFBokehCompositionParameter { value = DoFBokehComposition.Integrated };

        [EffectSettings, DepthOfField, DisplayName("Threshold"), DisplayConditionBool("depthOfFieldBokeh")]
        public ClampedFloatParameter depthOfFieldBokehThreshold = new ClampedFloatParameter(1f, 0f, 3f);

        [EffectSettings, DepthOfField, DisplayName("Intensity"), DisplayConditionBool("depthOfFieldBokeh")]
        public ClampedFloatParameter depthOfFieldBokehIntensity = new ClampedFloatParameter(2f, 0, 8f);

        [EffectSettings, DepthOfField, DisplayName("Downsampling"), DisplayConditionBool("useEXSettings")]
        public ClampedIntParameter depthOfFieldDownsampling = new ClampedIntParameter(2, 1, 5);

        [EffectSettings, DepthOfField, DisplayName("Sample Count"), DisplayConditionBool("useEXSettings")]
        public ClampedIntParameter depthOfFieldMaxSamples = new ClampedIntParameter(6, 2, 16);

        [EffectSettings, DepthOfField, DisplayName("Max Brightness"), HideInInspector]
        public FloatParameter depthOfFieldMaxBrightness = new FloatParameter(1000f);

        [EffectSettings, DepthOfField, DisplayName("Max Depth"), DisplayConditionBool("useEXSettings")]
        public ClampedFloatParameter depthOfFieldMaxDistance = new ClampedFloatParameter(1f, 0, 1f);

        [EffectSettings, DepthOfField, DisplayName("Filter Mode"), HideInInspector]
        public DoFFilterModeParameter depthOfFieldFilterMode = new DoFFilterModeParameter { value = FilterMode.Bilinear };

        #endregion



        #region RGB Dither

        [QualitySettings, HideInInspector]
        public ClampedFloatParameter ditherIntensity = new ClampedFloatParameter(0.005f, 0, 0.02f);

        #endregion



        #region Final Blur

        [QualitySettings, FinalBlur, DisplayName("Intensity")]
        public ClampedFloatParameter blurIntensity = new ClampedFloatParameter(0f, 0f, 64f);


        [QualitySettings, FinalBlur, DisplayName("Mask"), HideInInspector]
        public TextureParameter blurMask = new TextureParameter(null);


        #endregion

        public bool IsActive() => !disabled.value;

        public bool IsTileCompatible() => true;

        public bool RequiresDepthTexture()
        {
            return depthOfField.value;
        }

        void OnValidate()
        {
            depthOfFieldDistance.value = Mathf.Max(depthOfFieldDistance.value, depthOfFieldFocalLength.value);
        }
    }
}
