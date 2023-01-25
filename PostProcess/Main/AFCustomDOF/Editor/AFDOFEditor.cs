using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.Rendering;

namespace TK.Rendering.PostFX
{
    [VolumeComponentEditor(typeof(AFDOF))]
    public class AFDOFEditor : VolumeComponentEditor
    {
        AFDOF volume;
        GUIStyle sectionGroupStyle, foldoutStyle, blackBack;
        PropertyFetcher<AFDOF> propertyFetcher;
        
        class SectionContents
        {
            public Dictionary<AFDOF.SettingsGroup, List<MemberInfo>> groups
                = new Dictionary<AFDOF.SettingsGroup, List<MemberInfo>>();
            public List<MemberInfo> singleField = new List<MemberInfo>();
        }

        Dictionary<AFDOF.SectionGroup, SectionContents> sections
            = new Dictionary<AFDOF.SectionGroup, SectionContents>();
        Dictionary<AFDOF.SettingsGroup, List<MemberInfo>> groupedFields
            = new Dictionary<AFDOF.SettingsGroup, List<MemberInfo>>();
        public override void OnEnable()
        {
            base.OnEnable();
            blackBack = new GUIStyle();
            volume = (AFDOF)target;
            propertyFetcher = new PropertyFetcher<AFDOF>(serializedObject);
            var settings = volume.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(t => t.FieldType.IsSubclassOf(typeof(VolumeParameter)))
                .Where(t => (t.IsPublic && t.GetCustomAttributes(typeof(NonSerializedAttribute), false).Length == 0) ||
                                        (t.GetCustomAttributes(typeof(SerializeField), false).Length > 0))
                .Where(t => t.GetCustomAttributes(typeof(HideInInspector), false).Length == 0)
                .Where(t => t.GetCustomAttributes(typeof(AFDOF.SectionGroup), false).Any());
            foreach (var setting in settings)
            {
                SectionContents sectionContents = null;

                foreach (var section in setting.GetCustomAttributes(typeof(AFDOF.SectionGroup)) 
                    as IEnumerable<AFDOF.SectionGroup>)
                {
                    if (!sections.TryGetValue(section, out sectionContents))
                    {
                        sectionContents = sections[section] = new SectionContents();
                    }

                    bool isGrouped = false;
                    foreach (var settingGroup in setting.GetCustomAttributes(typeof(AFDOF.SettingsGroup)) 
                        as IEnumerable<AFDOF.SettingsGroup>)
                    {
                        if (!groupedFields.ContainsKey(settingGroup))
                        {
                            sectionContents.groups[settingGroup] = groupedFields[settingGroup] = 
                                new List<MemberInfo>();
                        }
                        groupedFields[settingGroup].Add(setting);
                        isGrouped = true;
                    }

                    if (!isGrouped)
                    {
                        sectionContents.singleField.Add(setting);
                    }
                }
            }
        }

        public override void OnInspectorGUI()
        {
            AFDOFSettings.ManageBuildOptimizationStatus(false);

            serializedObject.Update();

            SetStyles();

            EditorGUILayout.BeginVertical();
            {
                GUILayout.BeginHorizontal(blackBack);
                GUILayout.EndHorizontal();

                Camera cam = Camera.main;
                if (cam != null)
                {
                    UniversalAdditionalCameraData data = cam.GetComponent<UniversalAdditionalCameraData>();
                    if (data != null && !data.renderPostProcessing)
                    {
                        EditorGUILayout.HelpBox("Post Processing option is disabled in the camera.", MessageType.Warning);
                        if (GUILayout.Button("Go to Camera"))
                        {
                            Selection.activeObject = cam;
                        }
                        EditorGUILayout.Separator();
                    }
                }

                UniversalRenderPipelineAsset pipe = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (pipe == null)
                {
                    EditorGUILayout.HelpBox("Universal Rendering Pipeline asset is not set in 'Project Settings / Graphics' !", MessageType.Error);
                    EditorGUILayout.Separator();
                    GUI.enabled = false;
                }
                else if (!AFDOFRenderFeature.installed)
                {
                    EditorGUILayout.HelpBox("Render Feature must be added to the rendering pipeline renderer.", MessageType.Error);
                    if (GUILayout.Button("Go to Universal Rendering Pipeline Asset"))
                    {
                        Selection.activeObject = pipe;
                    }
                    EditorGUILayout.Separator();
                    GUI.enabled = false;
                }
                else if (volume.RequiresDepthTexture())
                {
                    if (!pipe.supportsCameraDepthTexture)
                    {
                        EditorGUILayout.HelpBox("Depth Texture option may be required for certain effects. Check Universal Rendering Pipeline asset!", MessageType.Warning);
                        if (GUILayout.Button("Go to Universal Rendering Pipeline Asset"))
                        {
                            Selection.activeObject = pipe;
                        }
                        EditorGUILayout.Separator();
                    }
                }

                foreach (var section in sections)
                {
                    bool printSectionHeader = true;

                    foreach (var field in section.Value.singleField)
                    {
                        var parameter = Unpack(propertyFetcher.Find(field.Name));
                        var displayName = parameter.displayName;
                        if (field.GetCustomAttribute(typeof(AFDOF.DisplayName)) is AFDOF.DisplayName displayNameAttrib)
                        {
                            displayName = displayNameAttrib.name;
                        }
                        bool indent;
                        if (!IsVisible(field, out indent)) continue;

                        if (printSectionHeader)
                        {
                            GUILayout.Space(0.0f);
                            Rect rect = GUILayoutUtility.GetRect(16f, 0f, sectionGroupStyle);
                            //GUI.Box(rect, ObjectNames.NicifyVariableName(section.Key.GetType().Name), sectionGroupStyle);
                            printSectionHeader = false;
                        }

                        DrawPropertyField(parameter, field, indent);

                        if (volume.disabled.value) GUI.enabled = false;
                    }
                    GUILayout.Space(6.0f);

                    // grouped properties
                    foreach (var group in section.Value.groups)
                    {
                        AFDOF.SettingsGroup settingsGroup = group.Key;
                        string groupName = ObjectNames.NicifyVariableName(settingsGroup.GetType().Name);
                        bool printGroupFoldout = true;
                        bool firstField = true;
                        bool groupHasContent = false;

                        foreach (var field in group.Value)
                        {
                            var parameter = Unpack(propertyFetcher.Find(field.Name));
                            bool indent;
                            if (!IsVisible(field, out indent))
                            {
                                if (firstField) break;
                                continue;
                            }

                            firstField = false;
                            if (printSectionHeader)
                            {
                                GUILayout.Space(0.0f);
                                Rect rect = GUILayoutUtility.GetRect(16f, 0f, sectionGroupStyle);
                                printSectionHeader = false;
                            }

                            if (printGroupFoldout)
                            {
                                printGroupFoldout = false;
                                settingsGroup.IsExpanded = EditorGUILayout.Foldout(settingsGroup.IsExpanded, groupName, true, foldoutStyle);
                                if (!settingsGroup.IsExpanded)
                                    break;
                            }

                            DrawPropertyField(parameter, field, indent);
                            groupHasContent = true;

                            if (parameter.value.propertyType == SerializedPropertyType.Boolean)
                            {
                                if (!parameter.value.boolValue)
                                {
                                    var hasToggleSectionBegin = field.GetCustomAttributes(typeof(AFDOF.ToggleAllFields)).Any();
                                    if (hasToggleSectionBegin) break;
                                }
                            }
                            else if (field.Name.Equals("depthOfFieldFocusMode"))
                            {
                                if (AFDOFSettings.instance != null && AFDOFSettings.instance.depthOfFieldTarget == null)
                                {
                                    SerializedProperty prop = serializedObject.FindProperty(field.Name);
                                    if (prop != null)
                                    {
                                        var value = prop.FindPropertyRelative("m_Value");
                                        if (value != null && value.enumValueIndex == (int)AFDOF.DoFFocusMode.FollowTarget)
                                        {
                                            Debug.Log("Plese Assign target");
                                        }
                                    }
                                }
                            }
                        }
                        if (groupHasContent)
                        {
                            GUILayout.Space(6.0f);
                        }
                    }
                }
            }
            EditorGUILayout.EndVertical();

            if (serializedObject.ApplyModifiedProperties())
            {
                AFDOFSettings.ManageBuildOptimizationStatus(true);
            }
        }

        bool IsVisible(MemberInfo field, out bool indent)
        {
            indent = false;
            if (field.GetCustomAttribute(typeof(AFDOF.DisplayConditionEnum)) is AFDOF.DisplayConditionEnum enumCondition)
            {
                SerializedProperty condProp = propertyFetcher.Find(enumCondition.field);
                if (condProp != null)
                {
                    var value = condProp.FindPropertyRelative("m_Value");
                    if (value != null && value.enumValueIndex != enumCondition.enumValueIndex)
                    {
                        return false;
                    }
                    indent = true;
                    return true;
                }
            }
            if (field.GetCustomAttribute(typeof(AFDOF.DisplayConditionBool)) is AFDOF.DisplayConditionBool boolCondition)
            {
                SerializedProperty condProp = propertyFetcher.Find(boolCondition.field);
                if (condProp != null)
                {
                    var value = condProp.FindPropertyRelative("m_Value");
                    if (value != null)
                    {
                        if (value.boolValue != boolCondition.value)
                        {
                            return false;
                        }
                        indent = value.boolValue;
                    }
                }
                SerializedProperty condProp2 = propertyFetcher.Find(boolCondition.field2);
                if (condProp2 != null)
                {
                    var value2 = condProp2.FindPropertyRelative("m_Value");
                    if (value2 != null)
                    {
                        if (value2.boolValue != boolCondition.value2)
                        {
                            return false;
                        }
                        indent = indent || value2.boolValue;
                    }
                }
            }
            return true;
        }

        void DrawPropertyField(SerializedDataParameter property, MemberInfo field, bool indent)
        {

            if (indent)
            {
                EditorGUI.indentLevel++;
            }

            var displayName = property.displayName;
            if (field.GetCustomAttribute(typeof(AFDOF.DisplayName)) is AFDOF.DisplayName displayNameAttrib)
            {
                displayName = displayNameAttrib.name;
            }

            if (property.value.propertyType == SerializedPropertyType.Boolean)
            {

                if (field.GetCustomAttribute(typeof(AFDOF.GlobalOverride)) != null)
                {

                    BoolParameter pr = property.GetObjectRef<BoolParameter>();
                    bool prev = pr.value;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var overrideRect = GUILayoutUtility.GetRect(17f, 17f, GUILayout.ExpandWidth(false));
                        overrideRect.yMin += 4f;
                        bool value = GUI.Toggle(overrideRect, prev, GUIContent.none);

                        string tooltip = null;
                        if (field.GetCustomAttribute(typeof(TooltipAttribute)) is TooltipAttribute tooltipAttribute)
                        {
                            tooltip = tooltipAttribute.tooltip;
                        }

                        using (new EditorGUI.DisabledScope(!prev))
                        {
                            EditorGUILayout.LabelField(new GUIContent(displayName, tooltip));
                        }

                        if (value != prev)
                        {
                            pr.value = value;
                            SerializedProperty prop = serializedObject.FindProperty(field.Name);
                            if (prop != null)
                            {
                                var boolProp = prop.FindPropertyRelative("m_Value");
                                if (boolProp != null)
                                {
                                    boolProp.boolValue = value;
                                }
                                if (value)
                                {
                                    var overrideProp = prop.FindPropertyRelative("m_OverrideState");
                                    if (overrideProp != null)
                                    {
                                        overrideProp.boolValue = true;
                                    }
                                }
                            }
                            if (field.GetCustomAttribute(typeof(AFDOF.BuildToggle)) != null)
                            {
                                AFDOFSettings.SetStripShaderKeywords(volume);
                            }
                        }
                    }

                }
                else
                {
                    PropertyField(property, new GUIContent(displayName));
                }
            }
            else
            {
                PropertyField(property, new GUIContent(displayName));
            }

            if (indent)
            {
                EditorGUI.indentLevel--;
            }

        }

        void SetStyles()
        {

            // section header style
            Color titleColor = EditorGUIUtility.isProSkin ? new Color(0.52f, 0.66f, 0.9f) : new Color(0.12f, 0.16f, 0.4f);
            GUIStyle skurikenModuleTitleStyle = "ShurikenModuleTitle";
            sectionGroupStyle = new GUIStyle(skurikenModuleTitleStyle);
            sectionGroupStyle.contentOffset = new Vector2(5f, -2f);
            sectionGroupStyle.normal.textColor = titleColor;
            sectionGroupStyle.fixedHeight = 22;
            sectionGroupStyle.fontStyle = FontStyle.Bold;

            // foldout style
            //Color foldoutColor = EditorGUIUtility.isProSkin ? new Color(0.52f, 0.66f, 0.9f) : new Color(0.12f, 0.16f, 0.4f);
            foldoutStyle = new GUIStyle(EditorStyles.foldout);
            foldoutStyle.margin = new RectOffset(6, 0, 0, 0);

        }

        [VolumeParameterDrawer(typeof(AFDOF.MinMaxFloatParameter))]
        public class MaxFloatParameterDrawer : VolumeParameterDrawer
        {
            public override bool OnGUI(SerializedDataParameter parameter, GUIContent title)
            {
                if (parameter.value.propertyType == SerializedPropertyType.Vector2)
                {
                    var o = parameter.GetObjectRef<AFDOF.MinMaxFloatParameter>();
                    var range = o.value;
                    float x = range.x;
                    float y = range.y;

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.MinMaxSlider(title, ref x, ref y, o.min, o.max);
                    x = EditorGUILayout.FloatField(x, GUILayout.Width(40));
                    y = EditorGUILayout.FloatField(y, GUILayout.Width(40));
                    EditorGUILayout.EndHorizontal();
                    if (EditorGUI.EndChangeCheck())
                    {
                        range.x = x;
                        range.y = y;
                        o.SetValue(new AFDOF.MinMaxFloatParameter(range, o.min, o.max));
                    }
                    return true;
                }
                else
                {
                    EditorGUILayout.PropertyField(parameter.value);
                    return false;
                }
            }
        }
    }
}
