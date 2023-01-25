using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace TK.Rendering.PostFX
{

    public delegate float OnBeforeFocusEvent(float currentFocusDistance);

    [ExecuteInEditMode]
    public class AFDOFSettings : MonoBehaviour
    {
        [Header("Scene Settings")]
        public Transform depthOfFieldTarget;

        public OnBeforeFocusEvent OnBeforeFocus;

        [NonSerialized]
        public static float depthOfFieldCurrentFocalPointDistance;

        [NonSerialized]
        public static bool dofTransparentSupport;

        [NonSerialized]
        public static int dofTransparentLayerMask;

        [NonSerialized]
        public static bool dofTransparentDoubleSided;

        [NonSerialized]
        public static bool dofAlphaTestSupport;

        [NonSerialized]
        public static int dofAlphaTestLayerMask;

        [NonSerialized]
        public static bool dofAlphaTestDoubleSided;

        static AFDOFSettings _instance;
        static Volume _DofVolume;
        static AFDOF _Dof;

        public static void UnloadDof()
        {
            _instance = null;
            _DofVolume = null;
            _Dof = null;
        }
        public static AFDOFSettings instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<AFDOFSettings>();
                    if (_instance == null)
                    {
                        _DofVolume = FindDoFVolume();
                        if (_DofVolume == null)
                        {
                            return null;
                        }
                        GameObject go = _DofVolume.gameObject;
                        _instance = go.GetComponent<AFDOFSettings>();
                        if (_instance == null)
                        {
                            _instance = go.AddComponent<AFDOFSettings>();
                        }
                    }
                }
                return _instance;
            }
        }
        static Volume FindDoFVolume()
        {
            Volume[] vols = FindObjectsOfType<Volume>();
            foreach (Volume volume in vols)
            {
                if (volume.sharedProfile != null && volume.sharedProfile.Has<AFDOF>())
                {
                    _DofVolume = volume;
                    return volume;
                }
            }
            return null;
        }
        public static AFDOF sharedSettings
        {
            get
            {
                if (_Dof != null) return _Dof;
                if (_DofVolume == null) FindDoFVolume();
                if (_DofVolume == null) return null;

                bool foundEffectSettings = _DofVolume.sharedProfile.TryGet(out _Dof);
                if (!foundEffectSettings)
                {
                    Debug.Log("Cant load  settings");
                    return null;
                }
                return _Dof;
            }
        }
        public static AFDOF settings
        {
            get
            {
                if (_Dof != null) return _Dof;
                if (_DofVolume == null) FindDoFVolume();
                if (_DofVolume == null) return null;

                bool foundEffectSettings = _DofVolume.profile.TryGet(out _Dof);
                if (!foundEffectSettings)
                {
                    Debug.Log("Cant load  settings");
                    return null;
                }
                return _Dof;
            }
        }
        void OnEnable()
        {
#if UNITY_EDITOR
            ManageBuildOptimizationStatus(true);
#endif
        }
#if UNITY_EDITOR
        static bool wasBuildOptActive;
        public static void ManageBuildOptimizationStatus(bool force)
        {
            AFDOF volumeDOF = sharedSettings;
            if (volumeDOF == null) return;

            if (!volumeDOF.active && (wasBuildOptActive || force))
            {
                StripKeywords();
            }
            else if (volumeDOF.active && (!wasBuildOptActive || force))
            {
                SetStripShaderKeywords(volumeDOF);
            }
            wasBuildOptActive = volumeDOF.active;
        }

        const string PLAYER_PREF_KEYNAME = "StripKeywordSet";


        public static void StripKeywords()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(AFDOFRenderFeature.SKW_DEPTH_OF_FIELD);
            sb.Append(AFDOFRenderFeature.SKW_DEPTH_OF_FIELD_TRANSPARENT);
            sb.Append(AFDOFRenderFeature.SKW_TURBO);
            sb.Append(AFDOFRenderFeature.SKW_CHROMATIC_ABERRATION);
            PlayerPrefs.SetString(PLAYER_PREF_KEYNAME, sb.ToString());
        }

        public static void SetStripShaderKeywords(AFDOF volumeDOF)
        {

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            if ((!volumeDOF.depthOfField.value))
            {
                sb.Append(AFDOFRenderFeature.SKW_DEPTH_OF_FIELD);
            }
            if ((!(volumeDOF.depthOfFieldTransparentSupport.value || volumeDOF.depthOfFieldAlphaTestSupport.value)))
            {
                sb.Append(AFDOFRenderFeature.SKW_DEPTH_OF_FIELD_TRANSPARENT);
            }
            if ((volumeDOF.chromaticAberrationIntensity.value <= 0))
            {
                sb.Append(AFDOFRenderFeature.SKW_CHROMATIC_ABERRATION);
            }

            PlayerPrefs.SetString(PLAYER_PREF_KEYNAME, sb.ToString());
        }
#endif
    }
}
