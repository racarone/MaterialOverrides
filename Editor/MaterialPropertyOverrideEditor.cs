﻿using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering;
using Object = UnityEngine.Object;

namespace MaterialOverrides
{
    [CustomEditor(typeof(MaterialPropertyOverride))]
    class MaterialPropertyOverrideEditor : Editor
    {
        MaterialPropertyOverride m_Target;

        SerializedProperty m_ShaderOverrides;
        SerializedProperty m_MaterialOverrides;
        SerializedProperty m_Renderers;

        Dictionary<ShaderPropertyOverrideList, ShaderPropertyOverrideListEditor> m_ShaderOverrideEditors = new Dictionary<ShaderPropertyOverrideList, ShaderPropertyOverrideListEditor>();
        EditorPrefBool m_ShaderOverridesFoldout;
        EditorPrefBool m_MaterialOverridesFoldout;

        void OnEnable()
        {
            m_ShaderOverrides = serializedObject.FindProperty("m_ShaderOverrides");
            m_MaterialOverrides = serializedObject.FindProperty("m_MaterialOverrides");
            m_Renderers = serializedObject.FindProperty("m_Renderers");

            m_Target = (MaterialPropertyOverride) target;
            m_Target.PopulateOverrides();
            RefreshEditors();

            m_ShaderOverridesFoldout = new EditorPrefBool($"{typeof(MaterialPropertyOverrideEditor)}:ShaderOverridesFoldout", true);
            m_MaterialOverridesFoldout = new EditorPrefBool($"{typeof(MaterialPropertyOverrideEditor)}:MaterialOverridesFoldout", true);

            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        void OnDisable()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        void OnHierarchyChanged()
        {
            if (!m_Target)
                return;

            m_Target.PopulateOverrides();
            RefreshEditors();
        }

        void RefreshEditors()
        {
            m_ShaderOverrideEditors.Clear();
            
            RefreshShaderEditors();
            RefreshMaterialEditors();
        }

        void RefreshShaderEditors()
        {
            for (int i = 0, count = m_ShaderOverrides.arraySize; i < count; i++)
            {
                var property = m_ShaderOverrides.GetArrayElementAtIndex(i);
                var overrideList = (ShaderPropertyOverrideList) property.objectReferenceValue;

                if (!m_ShaderOverrideEditors.TryGetValue(overrideList, out var editor))
                {
                    editor = new ShaderPropertyOverrideListEditor(overrideList, property, this);
                    m_ShaderOverrideEditors.Add(overrideList, editor);
                }
            }
        }

        void RefreshMaterialEditors()
        {
            for (int i = 0, count = m_MaterialOverrides.arraySize; i < count; i++)
            {
                var property = m_MaterialOverrides.GetArrayElementAtIndex(i);
                var overrideList = (MaterialPropertyOverrideList) property.objectReferenceValue;

                if (!m_ShaderOverrideEditors.TryGetValue(overrideList, out var editor))
                {
                    editor = new ShaderPropertyOverrideListEditor(overrideList, property, this);
                    m_ShaderOverrideEditors.Add(overrideList, editor);
                }
            }
        }

        public override void OnInspectorGUI()
        {
            if (!m_Target)
                return;

            serializedObject.Update();

            if (m_Target.renderers.Count == 0)
            {
                EditorGUILayout.HelpBox($"{typeof(MaterialPropertyOverride)} contains no renderers.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();

            // Find null renderers
            var oldRenderers = new List<Renderer>();
            foreach (var renderer in m_Target.renderers)
            {
                if (renderer == null)
                    continue;

                oldRenderers.Add(renderer);
            }

            // Remove null renderers
            m_Renderers.ClearArray();
            foreach (var renderer in oldRenderers)
            {
                m_Renderers.arraySize += 1;
                m_Renderers.GetArrayElementAtIndex(m_Renderers.arraySize - 1).objectReferenceValue = (Object) renderer;
            }

            EditorGUI.BeginChangeCheck();

            CoreEditorUtils.DrawSplitter();
            m_ShaderOverridesFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(m_ShaderOverridesFoldout.value, "Shader Overrides");
            if (m_ShaderOverridesFoldout.value)
            {
                var targetShaders = m_Target.shaders;
                
                for (int i = 0, count = targetShaders.Count; i < count; i++)
                {
                    if (!m_Target.TryGetOverride(targetShaders[i], out var propertyOverrideList))
                        continue;

                    if (!m_ShaderOverrideEditors.TryGetValue(propertyOverrideList, out var editor))
                        continue;

                    // Draw material header
                    CoreEditorUtils.DrawSplitter();
                    bool displayContent = CoreEditorUtils.DrawHeaderToggle(
                        targetShaders[i].name,
                        editor.baseProperty,
                        editor.active,
                        null,
                        () => editor.showHidden,
                        () => editor.showHidden = !editor.showHidden
                    );

                    if (displayContent)
                    {
                        using (new EditorGUI.DisabledScope(!editor.active.boolValue))
                        {
                            editor.OnInspectorGUI();
                        }
                    }
                }

                if (m_Target.shaders.Count == 0)
                    EditorGUILayout.HelpBox($"{typeof(MaterialPropertyOverride)} contains no shaders.", MessageType.Info);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            CoreEditorUtils.DrawSplitter();
            m_MaterialOverridesFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(m_MaterialOverridesFoldout.value, "Material Overrides");
            if (m_MaterialOverridesFoldout.value)
            {
                var targetMaterials = m_Target.materials;
                
                // Draw materials
                for (int i = 0, count = targetMaterials.Count; i < count; i++)
                {
                    if (!m_Target.TryGetOverride(targetMaterials[i], out var propertyOverrideList))
                        continue;

                    if (!m_ShaderOverrideEditors.TryGetValue(propertyOverrideList, out var editor))
                        continue;

                    // Draw material header
                    CoreEditorUtils.DrawSplitter();
                    bool displayContent = CoreEditorUtils.DrawHeaderToggle(
                        targetMaterials[i].name,
                        editor.baseProperty,
                        editor.active,
                        null,
                        () => editor.showHidden,
                        () => editor.showHidden = !editor.showHidden
                    );

                    if (displayContent)
                    {
                        using (new EditorGUI.DisabledScope(!editor.active.boolValue))
                        {
                            editor.OnInspectorGUI();
                        }
                    }
                }

                if (m_Target.materials.Count == 0)
                    EditorGUILayout.HelpBox($"{typeof(MaterialPropertyOverride)} contains no materials.", MessageType.Info);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            serializedObject.ApplyModifiedProperties();

            if (EditorGUI.EndChangeCheck())
            {
                m_Target.ApplyOverrides();
            }
        }
    }
}