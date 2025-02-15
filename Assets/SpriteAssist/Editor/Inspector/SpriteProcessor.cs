﻿using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpriteAssist
{
    public class SpriteProcessor : IDisposable
    {
        private readonly SpriteImportData _importData;
        private readonly SpritePreview _preview;

        private static bool _isOpenMeshSettings = true;
        private static bool _isOpenMeshPrefab = true;
        private static bool _isOpenScaleAndPivot;
        private static bool _isOpenTagsAndLayers;

        private string _originalUserData;
        private SpriteConfigData _configData;
        private MeshCreatorBase _meshCreator;

        private bool _isDataChanged = false;
        private Object[] _targets;

        public bool IsTextureImporterMode { get; set; }

        public bool IsExtendedByEditorWindow { get; set; }

        public bool IsUIEnabled
        {
            get { return !EditorWindow.HasOpenInstances<SpriteAssistEditorWindow>() || IsExtendedByEditorWindow; }
        }

        public bool IsEditorWindow
        {
            get { return EditorWindow.HasOpenInstances<SpriteAssistEditorWindow>() && IsExtendedByEditorWindow; }
        }

        public SpriteProcessor(Sprite sprite, string assetPath)
        {
            _importData = new SpriteImportData(sprite, assetPath);
            _originalUserData = _importData.textureImporter.userData;
            _configData = SpriteConfigData.GetData(_originalUserData);
            _meshCreator = MeshCreatorBase.GetInstnace(_configData);
            _preview = new SpritePreview(_meshCreator.GetMeshWireframes());

            Undo.undoRedoPerformed += UndoReimport;
        }

        public void OnInspectorGUI()
        {
            _targets = Selection.objects;

            ShowCommonUI();

            if (IsUIEnabled)
            {
                ShowEnabledUI();
            }
            else
            {
                ShowDisabledUI();
            }
        }

        private void ShowCommonUI()
        {
            if (!IsEditorWindow && GUILayout.Button("Open with SpriteAssist EditorWindow"))
            {
                ShowSaveOrRevertUI();
                EditorWindow.GetWindow<SpriteAssistEditorWindow>();
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PrefixLabel("Source Texture");
                    EditorGUILayout.ObjectField(_importData.sprite.texture, typeof(Texture2D), false);
                }
        }

        private void ShowDisabledUI()
        {
            EditorGUILayout.HelpBox("SpriteAssist EditorWindow is already open.", MessageType.Info);
        }

        private void ShowEnabledUI()
        {
            using (var checkChangedMode = new EditorGUI.ChangeCheckScope())
            {
                _configData.mode = (SpriteConfigData.Mode) EditorGUILayout.EnumPopup("SpriteAssist Mode", _configData.mode);
                EditorGUILayout.Space();

                if (checkChangedMode.changed)
                {
                    _meshCreator = MeshCreatorBase.GetInstnace(_configData);
                    _preview.SetWireframes(_meshCreator.GetMeshWireframes());
                }

                _isDataChanged |= checkChangedMode.changed;
            }

            if (!IsExtendedByEditorWindow)
            {
                EditorGUI.indentLevel++;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    _isOpenMeshSettings = EditorGUILayout.Foldout(_isOpenMeshSettings, "Mesh Settings");
                }

                if (_isOpenMeshSettings)
                {
                    using (var checkChangedMeshSettings = new EditorGUI.ChangeCheckScope())
                    {
                        if (_configData.mode.HasFlag(SpriteConfigData.Mode.TransparentMesh))
                        {
                            EditorGUILayout.LabelField("Transparent Mesh");
                            using (new EditorGUI.IndentLevelScope())
                            {
                                _configData.transparentDetail = EditorGUILayout.Slider("Detail", _configData.transparentDetail, 0.001f, 1f);
                                _configData.transparentAlphaTolerance = (byte)EditorGUILayout.Slider("Alpha Tolerance", _configData.transparentAlphaTolerance, 0, 254);
                                _configData.detectHoles = EditorGUILayout.Toggle("Detect Holes", _configData.detectHoles);
                                EditorGUILayout.Space();
                            }
                        }

                        if (_configData.mode.HasFlag(SpriteConfigData.Mode.OpaqueMesh))
                        {
                            EditorGUILayout.LabelField("Opaque Mesh");
                            using (new EditorGUI.IndentLevelScope())
                            {
                                _configData.opaqueDetail = EditorGUILayout.Slider("Detail", _configData.opaqueDetail, 0.001f, 1f);
                                _configData.opaqueAlphaTolerance = (byte)EditorGUILayout.Slider("Alpha Tolerance", _configData.opaqueAlphaTolerance, 0, 254);
                                using (new EditorGUI.DisabledScope(true))
                                {
                                    //force true
                                    EditorGUILayout.Toggle("Detect Holes (forced)", true);
                                }

                                EditorGUILayout.Space();
                            }
                        }

                        if (_configData.mode.HasFlag(SpriteConfigData.Mode.TransparentMesh) || _configData.mode.HasFlag(SpriteConfigData.Mode.OpaqueMesh))
                        {
                            _configData.edgeSmoothing = EditorGUILayout.Slider("Edge Smoothing", _configData.edgeSmoothing, 0f, 1f);
                            _configData.useNonZero = EditorGUILayout.Toggle("Non-zero Winding", _configData.useNonZero);
                            EditorGUILayout.Space();
                        }

                        if (_configData.mode == SpriteConfigData.Mode.UnityDefault)
                        {
                            using (new EditorGUILayout.VerticalScope(new GUIStyle { margin = new RectOffset(5, 5, 0, 5) }))
                                EditorGUILayout.HelpBox("Select other mode to use SpriteAssist.", MessageType.Info);
                        }

                        if (_configData.mode == SpriteConfigData.Mode.Complex)
                        {
                            using (new EditorGUILayout.VerticalScope(new GUIStyle { margin = new RectOffset(5, 5, 0, 5) }))
                                EditorGUILayout.HelpBox("Complex mode dose not override original sprite mesh.\nComplex mode only affects Mesh Prefab.", MessageType.Info);
                        }

                        _isDataChanged |= checkChangedMeshSettings.changed;
                    }
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope("box"))
                {
                    _isOpenMeshPrefab = EditorGUILayout.Foldout(_isOpenMeshPrefab, "Mesh Prefab");
                }

                if (_isOpenMeshPrefab)
                {
                    using (var checkChangedMeshPrefab = new EditorGUI.ChangeCheckScope())
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.ObjectField("Prefab", _importData.MeshPrefab, typeof(GameObject), false);
                            string buttonText = _importData.HasMeshPrefab ? "Remove" : "Create";
                            if (GUILayout.Button(buttonText, GUILayout.Width(60)))
                            {
                                Apply(true, _importData.HasMeshPrefab);
                                return;
                            }
                        }

                        if (!_importData.HasMeshPrefab)
                        {
                            Shader transparentShader = ShaderUtil.FindTransparentShader(_configData.transparentShaderName);
                            Shader opaqueShader = ShaderUtil.FindOpaqueShader(_configData.opaqueShaderName);
                            transparentShader = (Shader)EditorGUILayout.ObjectField("Transparent Shader", transparentShader, typeof(Shader), false);
                            opaqueShader = (Shader)EditorGUILayout.ObjectField("Opaque Shader", opaqueShader, typeof(Shader), false);
                            _configData.transparentShaderName = transparentShader == null ? null : transparentShader.name;
                            _configData.opaqueShaderName = opaqueShader == null ? null : opaqueShader.name;
                        }

                        _configData.thickness = EditorGUILayout.FloatField("Thickness", _configData.thickness);
                        _configData.thickness = Mathf.Max(0, _configData.thickness);
                        EditorGUILayout.Space();

                        _isDataChanged |= checkChangedMeshPrefab.changed;
                    }

                    _isOpenScaleAndPivot = EditorGUILayout.Foldout(_isOpenScaleAndPivot, "Scale and pivot");

                    if (_isOpenScaleAndPivot)
                    {
                        using (var checkChangedScaleAndPivot = new EditorGUI.ChangeCheckScope())
                        using (new EditorGUI.IndentLevelScope())
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.Space(16);

                                if (IsTextureImporterMode && _importData.textureImporter.textureType == TextureImporterType.Sprite &&
                                    GUILayout.Button("Copy from Sprite"))
                                {
                                    Apply(false, false, true);
                                    return;
                                }
                            }

                            if (IsTextureImporterMode)
                            {
                                EditorGUIUtility.wideMode = true;
                                _importData.textureImporter.spritePixelsPerUnit = EditorGUILayout.FloatField("Texture pixels per unit", _importData.textureImporter.spritePixelsPerUnit);
                                _importData.textureImporter.spritePivot = EditorGUILayout.Vector2Field("Texture pivot", _importData.textureImporter.spritePivot);
                            }
                            else
                            {
                                EditorGUIUtility.wideMode = true;
                                bool wasEnabled = GUI.enabled;
                                GUI.enabled = false;
                                EditorGUILayout.FloatField("Sprite pixels per unit", _importData.sprite.pixelsPerUnit);
                                EditorGUILayout.Vector2Field("Sprite pivot", _importData.sprite.GetNormalizedPivot());
                                EditorGUILayout.Vector2Field("Sprite pivot", _importData.sprite.GetNormalizedPivot());
                                GUI.enabled = wasEnabled;
                            }

                            _isDataChanged |= checkChangedScaleAndPivot.changed;
                        }
                    }

                    _isOpenTagsAndLayers = EditorGUILayout.Foldout(_isOpenTagsAndLayers, "Tags and Layers");

                    if (_isOpenTagsAndLayers)
                    {
                        using (var checkChangedTagsAndLayers = new EditorGUI.ChangeCheckScope())
                        using (new EditorGUI.IndentLevelScope())
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                _configData.overrideTag = EditorGUILayout.Toggle(_configData.overrideTag, GUILayout.Width(45));
                                GUILayout.Space(-30);

                                using (new EditorGUI.DisabledGroupScope(!_configData.overrideTag))
                                {
                                    if (_configData.overrideTag)
                                    {
                                        _configData.tag = EditorGUILayout.TagField("Tag", _configData.tag);
                                    }
                                    else
                                    {
                                        EditorGUILayout.TagField("Tag", SpriteAssistSettings.Settings.defaultTag);
                                    }
                                }
                            }

                            using (new EditorGUILayout.HorizontalScope())
                            {
                                _configData.overrideLayer = EditorGUILayout.Toggle(_configData.overrideLayer, GUILayout.Width(45));
                                GUILayout.Space(-30);

                                using (new EditorGUI.DisabledGroupScope(!_configData.overrideLayer))
                                {
                                    if (_configData.overrideLayer)
                                    {
                                        _configData.layer = EditorGUILayout.LayerField("Layer", _configData.layer);
                                    }
                                    else
                                    {
                                        EditorGUILayout.LayerField("Layer", SpriteAssistSettings.Settings.defaultLayer);
                                    }
                                }
                            }

                            using (new EditorGUILayout.HorizontalScope())
                            {
                                _configData.overrideSortingLayer = EditorGUILayout.Toggle(_configData.overrideSortingLayer, GUILayout.Width(45));
                                GUILayout.Space(-30);

                                using (new EditorGUI.DisabledGroupScope(!_configData.overrideSortingLayer))
                                {
                                    if (_configData.overrideSortingLayer)
                                    {
                                        int index = Array.FindIndex(SortingLayer.layers, layer => layer.id == _configData.sortingLayerId);
                                        index = EditorGUILayout.Popup("Sorting Layer", index, (from layer in SortingLayer.layers select layer.name).ToArray());
                                        _configData.sortingLayerId = SortingLayer.layers[index].id;
                                        _configData.sortingOrder = EditorGUILayout.IntField( _configData.sortingOrder, GUILayout.Width(60));
                                    }
                                    else
                                    {
                                        int index = Array.FindIndex(SortingLayer.layers, layer => layer.id == SpriteAssistSettings.Settings.defaultSortingLayerId);
                                        EditorGUILayout.Popup("Sorting Layer", index, (from layer in SortingLayer.layers select layer.name).ToArray());
                                        EditorGUILayout.IntField(SpriteAssistSettings.Settings.defaultSortingOrder, GUILayout.Width(60));
                                    }
                                }
                            }

                            _isDataChanged |= checkChangedTagsAndLayers.changed;
                        }
                    }

                    EditorGUILayout.Space();

                    if (_configData != null && _configData.IsOverriden && _configData.mode == SpriteConfigData.Mode.Complex)
                    {
                        if (_importData.MeshPrefab == null)
                        {
                            using (new EditorGUILayout.VerticalScope(new GUIStyle { margin = new RectOffset(5, 0, 5, 5) }))
                                EditorGUILayout.HelpBox("To use complex mode must be created Mesh Prefab.", MessageType.Warning);
                        }
                    }
                }
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            using (new EditorGUI.DisabledScope(!_isDataChanged))
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Revert", GUILayout.Width(50)))
                {
                    Revert();
                }

                if (GUILayout.Button("Apply", GUILayout.Width(50)))
                {
                    Apply();
                }
            }

            if (!_importData.IsTightMesh)
            {
                EditorGUILayout.HelpBox("Mesh Type is not Tight Mesh. Change texture setting.", MessageType.Warning);
            }

            EditorGUILayout.Space();

            if (_isDataChanged)
            {
                Undo.RegisterCompleteObjectUndo(_importData.textureImporter, "SpriteAssist Texture");

                _importData.textureImporter.userData = JsonUtility.ToJson(_configData);
            }
        }

        public void OnPreviewGUI(Rect rect, Sprite sprite, TextureInfo textureInfo)
        {
            if (!IsUIEnabled)
            {
                return;
            }

            //skip 'rect (0, 0, 1, 1)' issue
            if (rect.width <= 1 || rect.height <= 1)
            {
                return;
            }

            //for multiple preview
            bool hasMultipleTargets = _targets.Length > 1;
            _preview.Show(rect, sprite, textureInfo, _configData, hasMultipleTargets);
        }

        public void Dispose()
        {
            _preview.Dispose();
            Undo.undoRedoPerformed -= UndoReimport;

            ShowSaveOrRevertUI();
        }

        private void ShowSaveOrRevertUI()
        {
            if (_isDataChanged)
            {
                if (EditorUtility.DisplayDialog("Unapplied import settings", $"Unapplied import settings for '{_importData.assetPath}'", "Apply", "Revert"))
                {
                    Apply();
                }
                else
                {
                    Revert();
                }
            }
        }

        private void Revert()
        {
            _configData = SpriteConfigData.GetData(_originalUserData);
            _meshCreator = MeshCreatorBase.GetInstnace(_configData);
            _preview.SetWireframes(_meshCreator.GetMeshWireframes());
            _importData.textureImporter.userData = _originalUserData;
            _isDataChanged = false;
        }

        private void Apply(bool withMeshPrefabCreation = false, bool hasMeshPrefab = false, bool withCopyFromSprite = false)
        {
            Undo.RegisterCompleteObjectUndo(_targets, "SpriteAssist Texture");

            _originalUserData = JsonUtility.ToJson(_configData);

            for (var i = 0; i < _targets.Length; i++)
            {
                var selectedTarget = _targets[i];

                //override target
                if (selectedTarget is GameObject gameObject)
                {
                    if (gameObject.TryGetComponent<SpriteRenderer>(out var spriteRenderer))
                    {
                        selectedTarget = spriteRenderer.sprite.texture;
                    }
                    else if (gameObject.TryGetComponent<MeshRenderer>(out var meshRenderer))
                    {
                        selectedTarget = meshRenderer.sharedMaterial.mainTexture;
                    }
                }

                string assetPath = AssetDatabase.GetAssetPath(selectedTarget);
                TextureImporter textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (textureImporter == null)
                {
                    continue;
                }

                if (withCopyFromSprite)
                {
                    Sprite rootSprite = AssetDatabase.LoadAllAssetsAtPath(assetPath).FirstOrDefault(obj => obj is Sprite) as Sprite;
                    if (rootSprite != null)
                    {
                        textureImporter.spritePixelsPerUnit = rootSprite.pixelsPerUnit;
                        textureImporter.spritePivot = rootSprite.GetNormalizedPivot();
                    }
                }

                Sprite sprite = null;

                switch (selectedTarget)
                {
                    case Sprite value:
                        sprite = value;
                        break;

                    case Texture2D texture:
                        sprite = SpriteUtil.CreateDummySprite(texture, textureImporter.spritePivot, textureImporter.spritePixelsPerUnit);
                        break;

                    default:
                        continue;
                }

                SpriteImportData importData = new SpriteImportData(sprite, textureImporter, assetPath);
                importData.textureImporter.userData = _originalUserData;

                EditorUtility.SetDirty(importData.textureImporter);
                AssetDatabase.WriteImportSettingsIfDirty(importData.textureImporter.assetPath);
                AssetDatabase.ImportAsset(importData.textureImporter.assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.DontDownloadFromCacheServer);

                if (withMeshPrefabCreation)
                {
                    if (hasMeshPrefab)
                    {
                        importData.RemoveExternalPrefab();
                    }
                    else
                    {
                        importData.RemoveExternalPrefab();
                        TextureInfo textureInfo = new TextureInfo(importData.assetPath, importData.sprite);
                        GameObject prefab = _meshCreator.CreateExternalObject(importData.sprite, textureInfo, _configData);
                        importData.SetPrefabAsExternalObject(prefab);
                    }
                }
                else
                {
                    //update mesh prefab
                    if (importData.HasMeshPrefab)
                    {
                        PrefabUtil.CleanUpSubAssets(importData.MeshPrefab);
                        TextureInfo textureInfo = new TextureInfo(importData.assetPath, sprite);
                        string oldPrefabPath = AssetDatabase.GetAssetPath(importData.MeshPrefab);
                        GameObject prefab = _meshCreator.CreateExternalObject(sprite, textureInfo, _configData, oldPrefabPath);
                        importData.RemapExternalObject(prefab);
                    }
                }
            }

            _isDataChanged = false;
        }

        private void UndoReimport()
        {
            _configData = SpriteConfigData.GetData(_importData.textureImporter.userData);
            _isDataChanged = true;

            if (_targets == null)
                return;

            foreach (var t in _targets)
            {
                string path = AssetDatabase.GetAssetPath(t);

                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.DontDownloadFromCacheServer);
                }
            }
        }
    }
}
