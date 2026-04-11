using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TextCore.LowLevel;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TMPFontProjectScannerWindow : EditorWindow
{
    private static readonly MethodInfo SetAtlasTextureReadableMethod =
        Type.GetType("UnityEditor.TextCore.LowLevel.FontEngineEditorUtilities, UnityEditor")
            ?.GetMethod("SetAtlasTextureIsReadable", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

    private const string DefaultScanRoot = "Assets";
    private const string DefaultExportPath = "Assets/Arts/Font/Generated/ProjectCharacters.txt";
    private const string DefaultExcludedPaths = "Assets/TextMesh Pro\nAssets/Plugins\nAssets/Arts/Font/Generated";

    [Serializable]
    private sealed class ScanResult
    {
        public uint[] codePoints = Array.Empty<uint>();
        public string characterString = string.Empty;
        public string missingCharacterString = string.Empty;
        public int scannedScenes;
        public int scannedPrefabs;
        public int scannedScriptableObjects;
        public int scannedTextAssets;
        public int scannedScripts;
        public int scannedStringFields;
    }

    [SerializeField] private DefaultAsset scanRootFolder;
    [SerializeField] private TMP_FontAsset targetFontAsset;
    [SerializeField] private string exportCharacterFilePath = DefaultExportPath;
    [SerializeField] private string additionalCharacters = string.Empty;
    [SerializeField] private string excludedPaths = DefaultExcludedPaths;
    [SerializeField] private bool includeScenes = true;
    [SerializeField] private bool includePrefabs = true;
    [SerializeField] private bool includeScriptableObjects = true;
    [SerializeField] private bool includeTextAssets = true;
    [SerializeField] private bool includeCSharpStringLiterals;
    [SerializeField] private bool clearExistingGlyphs = true;
    [SerializeField] private bool keepFontStaticAfterUpdate = true;
    [SerializeField] private bool enableMultiAtlas;
    [SerializeField] private bool includeFontFeatures;

    private readonly StringBuilder logBuilder = new StringBuilder(2048);
    private ScanResult lastScanResult;
    private Vector2 scrollPosition;

    [MenuItem("Tools/TextMeshPro/Project Font Scanner")]
    private static void ShowWindow()
    {
        TMPFontProjectScannerWindow window = GetWindow<TMPFontProjectScannerWindow>();
        window.titleContent = new GUIContent("TMP Font Scanner");
        window.minSize = new Vector2(520f, 620f);
    }

    private void OnEnable()
    {
        if (scanRootFolder == null)
        {
            scanRootFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultScanRoot);
        }

        if (targetFontAsset == null)
        {
            targetFontAsset = Selection.activeObject as TMP_FontAsset;

            if (targetFontAsset == null && TMP_Settings.instance != null)
            {
                targetFontAsset = TMP_Settings.defaultFontAsset;
            }
        }

        if (string.IsNullOrWhiteSpace(exportCharacterFilePath))
        {
            exportCharacterFilePath = DefaultExportPath;
        }

        if (string.IsNullOrWhiteSpace(excludedPaths))
        {
            excludedPaths = DefaultExcludedPaths;
        }
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Scan Settings", EditorStyles.boldLabel);
        scanRootFolder = (DefaultAsset)EditorGUILayout.ObjectField("Scan Root", scanRootFolder, typeof(DefaultAsset), false);
        targetFontAsset = (TMP_FontAsset)EditorGUILayout.ObjectField("Target Font Asset", targetFontAsset, typeof(TMP_FontAsset), false);
        exportCharacterFilePath = EditorGUILayout.TextField("Export Character File", exportCharacterFilePath);

        EditorGUILayout.Space();
        includeScenes = EditorGUILayout.ToggleLeft("Include scenes", includeScenes);
        includePrefabs = EditorGUILayout.ToggleLeft("Include prefabs", includePrefabs);
        includeScriptableObjects = EditorGUILayout.ToggleLeft("Include ScriptableObject assets", includeScriptableObjects);
        includeTextAssets = EditorGUILayout.ToggleLeft("Include TextAssets (.txt/.json/.csv/...)", includeTextAssets);
        includeCSharpStringLiterals = EditorGUILayout.ToggleLeft("Include C# string literals under Assets/Scripts", includeCSharpStringLiterals);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Update Settings", EditorStyles.boldLabel);
        clearExistingGlyphs = EditorGUILayout.ToggleLeft("Clear existing glyphs before rebuild", clearExistingGlyphs);
        keepFontStaticAfterUpdate = EditorGUILayout.ToggleLeft("Switch target font back to Static after update", keepFontStaticAfterUpdate);
        enableMultiAtlas = EditorGUILayout.ToggleLeft("Enable multi atlas on rebuild", enableMultiAtlas);
        includeFontFeatures = EditorGUILayout.ToggleLeft("Rebuild kerning / font features", includeFontFeatures);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Additional Characters", EditorStyles.boldLabel);
        additionalCharacters = EditorGUILayout.TextArea(additionalCharacters, GUILayout.MinHeight(64f));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Excluded Paths", EditorStyles.boldLabel);
        excludedPaths = EditorGUILayout.TextArea(excludedPaths, GUILayout.MinHeight(72f));

        EditorGUILayout.Space();
        DrawActionButtons();
        DrawLastResult();
        DrawLog();

        EditorGUILayout.EndScrollView();
    }

    private void DrawActionButtons()
    {
        string rootPath = GetScanRootPath();
        bool canScan = !string.IsNullOrWhiteSpace(rootPath) && AssetDatabase.IsValidFolder(rootPath);
        bool canUpdate = canScan && targetFontAsset != null;

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = canScan;
            if (GUILayout.Button("Scan Project Characters", GUILayout.Height(28f)))
            {
                RunScan(exportCharactersToFile: true);
            }

            GUI.enabled = canUpdate;
            if (GUILayout.Button("Scan And Update Font Asset", GUILayout.Height(28f)))
            {
                RunScanAndUpdateFontAsset();
            }

            GUI.enabled = true;
        }

        if (!canScan)
        {
            EditorGUILayout.HelpBox("Scan Root must point to a valid folder under Assets.", MessageType.Warning);
        }

        if (targetFontAsset == null)
        {
            EditorGUILayout.HelpBox("Assign a TMP_FontAsset before running the update step.", MessageType.Info);
        }
    }

    private void DrawLastResult()
    {
        if (lastScanResult == null || lastScanResult.codePoints.Length == 0)
        {
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Last Scan", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Unique code points", lastScanResult.codePoints.Length.ToString());
        EditorGUILayout.LabelField("Scenes", lastScanResult.scannedScenes.ToString());
        EditorGUILayout.LabelField("Prefabs", lastScanResult.scannedPrefabs.ToString());
        EditorGUILayout.LabelField("ScriptableObjects", lastScanResult.scannedScriptableObjects.ToString());
        EditorGUILayout.LabelField("TextAssets", lastScanResult.scannedTextAssets.ToString());
        EditorGUILayout.LabelField("C# files", lastScanResult.scannedScripts.ToString());
        EditorGUILayout.LabelField("Serialized string fields", lastScanResult.scannedStringFields.ToString());

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Character Preview", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(BuildPreview(lastScanResult.characterString, 400), EditorStyles.textArea, GUILayout.MinHeight(72f));

        if (!string.IsNullOrEmpty(lastScanResult.missingCharacterString))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Some scanned characters could not be added to the source font. Review the missing character preview below.", MessageType.Warning);
            EditorGUILayout.SelectableLabel(BuildPreview(lastScanResult.missingCharacterString, 300), EditorStyles.textArea, GUILayout.MinHeight(54f));
        }
    }

    private void DrawLog()
    {
        if (logBuilder.Length == 0)
        {
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(logBuilder.ToString(), EditorStyles.textArea, GUILayout.MinHeight(220f));
    }

    private void RunScanAndUpdateFontAsset()
    {
        ScanResult scan = RunScan(exportCharactersToFile: true);
        if (scan == null || scan.codePoints.Length == 0 || targetFontAsset == null)
        {
            return;
        }

        string missingCharacters;
        if (RebuildFontAsset(targetFontAsset, scan.codePoints, scan.characterString, out missingCharacters))
        {
            scan.missingCharacterString = missingCharacters;
            lastScanResult = scan;

            if (string.IsNullOrEmpty(missingCharacters))
            {
                AppendLog($"Updated font asset: {AssetDatabase.GetAssetPath(targetFontAsset)}");
            }
            else
            {
                AppendLog($"Updated font asset with missing characters: {missingCharacters.Length} UTF-16 code units could not be added.");
            }
        }
    }

    private ScanResult RunScan(bool exportCharactersToFile)
    {
        string rootPath = GetScanRootPath();
        if (string.IsNullOrWhiteSpace(rootPath) || !AssetDatabase.IsValidFolder(rootPath))
        {
            AppendLog("Scan root is invalid.");
            return null;
        }

        try
        {
            ScanResult result = ScanProjectCharacters(rootPath);
            lastScanResult = result;

            if (result.codePoints.Length == 0)
            {
                AppendLog("Scan completed, but no characters were collected.");
                return result;
            }

            if (exportCharactersToFile)
            {
                ExportCharacters(result.characterString);
            }

            AppendLog($"Scan completed: {result.codePoints.Length} unique code points.");
            return result;
        }
        catch (Exception exception)
        {
            AppendLog($"Scan failed: {exception.Message}");
            Debug.LogException(exception);
            return null;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private ScanResult ScanProjectCharacters(string rootPath)
    {
        SortedSet<uint> codePoints = new SortedSet<uint>();
        ScanResult result = new ScanResult();
        string[] roots = { rootPath };
        string[] excludedPrefixes = ParseExcludedPaths();

        AddStringToSet(additionalCharacters, codePoints);

        if (includeScenes)
        {
            ScanScenes(roots, excludedPrefixes, codePoints, result);
        }

        if (includePrefabs)
        {
            ScanPrefabs(roots, excludedPrefixes, codePoints, result);
        }

        if (includeScriptableObjects)
        {
            ScanScriptableObjects(roots, excludedPrefixes, codePoints, result);
        }

        if (includeTextAssets)
        {
            ScanTextAssets(roots, excludedPrefixes, codePoints, result);
        }

        if (includeCSharpStringLiterals)
        {
            ScanCSharpFiles(roots, excludedPrefixes, codePoints, result);
        }

        result.codePoints = codePoints.ToArray();
        result.characterString = BuildCharacterString(result.codePoints);
        return result;
    }

    private void ScanScenes(string[] roots, string[] excludedPrefixes, SortedSet<uint> codePoints, ScanResult result)
    {
        string[] scenePaths = FindAssetPaths("t:Scene", roots, excludedPrefixes);
        for (int index = 0; index < scenePaths.Length; index++)
        {
            string scenePath = scenePaths[index];
            ShowProgress("Scanning scenes", scenePath, index, scenePaths.Length);

            Scene loadedScene = FindLoadedSceneByPath(scenePath);
            bool wasAlreadyLoaded = loadedScene.IsValid() && loadedScene.isLoaded;
            Scene scene = wasAlreadyLoaded ? loadedScene : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try
            {
                foreach (GameObject rootObject in scene.GetRootGameObjects())
                {
                    ScanGameObjectHierarchy(rootObject, scenePath, codePoints, result);
                }

                result.scannedScenes += 1;
            }
            finally
            {
                if (!wasAlreadyLoaded && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }

    private void ScanPrefabs(string[] roots, string[] excludedPrefixes, SortedSet<uint> codePoints, ScanResult result)
    {
        string[] prefabPaths = FindAssetPaths("t:Prefab", roots, excludedPrefixes);
        for (int index = 0; index < prefabPaths.Length; index++)
        {
            string prefabPath = prefabPaths[index];
            ShowProgress("Scanning prefabs", prefabPath, index, prefabPaths.Length);

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ScanGameObjectHierarchy(prefabRoot, prefabPath, codePoints, result);
                result.scannedPrefabs += 1;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }

    private void ScanScriptableObjects(string[] roots, string[] excludedPrefixes, SortedSet<uint> codePoints, ScanResult result)
    {
        string[] assetPaths = FindAssetPaths("t:ScriptableObject", roots, excludedPrefixes);
        for (int index = 0; index < assetPaths.Length; index++)
        {
            string assetPath = assetPaths[index];
            ShowProgress("Scanning ScriptableObjects", assetPath, index, assetPaths.Length);

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            bool counted = false;
            foreach (UnityEngine.Object asset in assets)
            {
                if (!ShouldScanGenericAssetObject(asset))
                {
                    continue;
                }

                ScanSerializedStrings(asset, assetPath, codePoints, result);
                counted = true;
            }

            if (counted)
            {
                result.scannedScriptableObjects += 1;
            }
        }
    }

    private void ScanTextAssets(string[] roots, string[] excludedPrefixes, SortedSet<uint> codePoints, ScanResult result)
    {
        string[] assetPaths = FindAssetPaths("t:TextAsset", roots, excludedPrefixes);
        for (int index = 0; index < assetPaths.Length; index++)
        {
            string assetPath = assetPaths[index];
            ShowProgress("Scanning TextAssets", assetPath, index, assetPaths.Length);

            TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (textAsset == null)
            {
                continue;
            }

            AddStringToSet(textAsset.text, codePoints);
            result.scannedTextAssets += 1;
        }
    }

    private void ScanCSharpFiles(string[] roots, string[] excludedPrefixes, SortedSet<uint> codePoints, ScanResult result)
    {
        string[] scriptPaths = FindAssetPaths("t:MonoScript", roots, excludedPrefixes);
        for (int index = 0; index < scriptPaths.Length; index++)
        {
            string scriptPath = scriptPaths[index];
            ShowProgress("Scanning C# string literals", scriptPath, index, scriptPaths.Length);

            string absolutePath = Path.GetFullPath(scriptPath);
            if (!File.Exists(absolutePath))
            {
                continue;
            }

            string source = File.ReadAllText(absolutePath);
            foreach (string literal in ExtractCSharpStringLiterals(source))
            {
                AddStringToSet(literal, codePoints);
            }

            result.scannedScripts += 1;
        }
    }

    private void ScanGameObjectHierarchy(GameObject rootObject, string contextPath, SortedSet<uint> codePoints, ScanResult result)
    {
        foreach (Transform transform in rootObject.GetComponentsInChildren<Transform>(true))
        {
            GameObject current = transform.gameObject;
            ScanSerializedStrings(current, contextPath, codePoints, result);

            Component[] components = current.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component == null)
                {
                    continue;
                }

                ScanSerializedStrings(component, contextPath, codePoints, result);
            }
        }
    }

    private void ScanSerializedStrings(UnityEngine.Object targetObject, string contextPath, SortedSet<uint> codePoints, ScanResult result)
    {
        SerializedObject serializedObject;
        try
        {
            serializedObject = new SerializedObject(targetObject);
        }
        catch
        {
            return;
        }

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;

        while (iterator.Next(enterChildren))
        {
            enterChildren = true;

            if (iterator.propertyType != SerializedPropertyType.String)
            {
                continue;
            }

            if (ShouldSkipProperty(iterator.propertyPath))
            {
                continue;
            }

            string value = iterator.stringValue;
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            AddStringToSet(value, codePoints);
            result.scannedStringFields += 1;
        }
    }

    private bool RebuildFontAsset(TMP_FontAsset fontAsset, uint[] codePoints, string characterString, out string missingCharacterString)
    {
        missingCharacterString = string.Empty;

        if (fontAsset == null)
        {
            AppendLog("Target font asset is null.");
            return false;
        }

        Font sourceFont = GetEditorSourceFont(fontAsset);
        if (sourceFont == null)
        {
            AppendLog($"Font asset '{fontAsset.name}' does not keep a source font reference. Reassign the source font in the TMP inspector first.");
            return false;
        }

        string assetPath = AssetDatabase.GetAssetPath(fontAsset);
        try
        {
            EditorUtility.DisplayProgressBar("Updating TMP font asset", assetPath, 0.1f);

            Undo.RegisterCompleteObjectUndo(fontAsset, "Rebuild TMP Font Asset");

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.isMultiAtlasTexturesEnabled = enableMultiAtlas;
            ForceSourceFontReference(fontAsset, sourceFont);
            SetAtlasTexturesReadable(fontAsset, true);

            if (clearExistingGlyphs)
            {
                fontAsset.ClearFontAssetData();
            }

            EditorUtility.DisplayProgressBar("Updating TMP font asset", "Adding scanned characters", 0.55f);

            bool success = fontAsset.TryAddCharacters(codePoints, out uint[] missingCodePoints, includeFontFeatures);
            missingCharacterString = BuildCharacterString(missingCodePoints ?? Array.Empty<uint>());

            UpdateCreationSettings(fontAsset, sourceFont, characterString);

            if (keepFontStaticAfterUpdate)
            {
                fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            }

            SetAtlasTexturesReadable(fontAsset, false);

            EditorUtility.SetDirty(fontAsset);
            if (fontAsset.material != null)
            {
                EditorUtility.SetDirty(fontAsset.material);
            }

            foreach (Texture2D atlasTexture in fontAsset.atlasTextures)
            {
                if (atlasTexture != null)
                {
                    EditorUtility.SetDirty(atlasTexture);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            fontAsset.ReadFontAssetDefinition();
            TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, fontAsset);

            return success || !string.IsNullOrEmpty(missingCharacterString);
        }
        catch (Exception exception)
        {
            AppendLog($"Font update failed: {exception.Message}");
            Debug.LogException(exception);
            return false;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private void UpdateCreationSettings(TMP_FontAsset fontAsset, Font sourceFont, string characterString)
    {
        FontAssetCreationSettings settings = fontAsset.creationSettings;
        string sourceFontPath = AssetDatabase.GetAssetPath(sourceFont);

        settings.sourceFontFileName = sourceFont.name;
        settings.sourceFontFileGUID = AssetDatabase.AssetPathToGUID(sourceFontPath);
        settings.pointSize = Mathf.RoundToInt(fontAsset.faceInfo.pointSize);
        settings.pointSizeSamplingMode = settings.pointSizeSamplingMode < 0 ? 0 : settings.pointSizeSamplingMode;
        settings.padding = fontAsset.atlasPadding;
        settings.packingMode = settings.packingMode < 0 ? 0 : settings.packingMode;
        settings.atlasWidth = fontAsset.atlasWidth;
        settings.atlasHeight = fontAsset.atlasHeight;
        settings.characterSetSelectionMode = 7;
        settings.characterSequence = characterString;
        settings.renderMode = (int)fontAsset.atlasRenderMode;
        settings.includeFontFeatures = includeFontFeatures;
        fontAsset.creationSettings = settings;
    }

    private void ExportCharacters(string characterString)
    {
        if (string.IsNullOrWhiteSpace(exportCharacterFilePath))
        {
            return;
        }

        string assetPath = exportCharacterFilePath.Replace('\\', '/').Trim();
        if (!assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            AppendLog("Export path must stay under Assets/.");
            return;
        }

        string absolutePath = Path.GetFullPath(assetPath);
        string directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(absolutePath, characterString, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        AppendLog($"Exported characters to: {assetPath}");
    }

    private static string[] FindAssetPaths(string filter, string[] roots, string[] excludedPrefixes)
    {
        return AssetDatabase.FindAssets(filter, roots)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !ShouldSkipPath(path, excludedPrefixes))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool ShouldScanGenericAssetObject(UnityEngine.Object asset)
    {
        if (asset == null)
        {
            return false;
        }

        if (asset is TMP_FontAsset || asset is TMP_Settings || asset is Material || asset is Texture || asset is Font)
        {
            return false;
        }

        return asset is ScriptableObject;
    }

    private static bool ShouldSkipProperty(string propertyPath)
    {
        return string.Equals(propertyPath, "m_Name", StringComparison.Ordinal) ||
               string.Equals(propertyPath, "m_EditorClassIdentifier", StringComparison.Ordinal) ||
               string.Equals(propertyPath, "m_TargetAssemblyTypeName", StringComparison.Ordinal) ||
               string.Equals(propertyPath, "m_MethodName", StringComparison.Ordinal) ||
               string.Equals(propertyPath, "m_TagString", StringComparison.Ordinal);
    }

    private static bool ShouldSkipPath(string assetPath, string[] excludedPrefixes)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return true;
        }

        for (int index = 0; index < excludedPrefixes.Length; index++)
        {
            if (assetPath.StartsWith(excludedPrefixes[index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddStringToSet(string value, ISet<uint> codePoints)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        for (int index = 0; index < value.Length; index++)
        {
            uint codePoint = value[index];
            if (index + 1 < value.Length && char.IsHighSurrogate(value[index]) && char.IsLowSurrogate(value[index + 1]))
            {
                codePoint = (uint)char.ConvertToUtf32(value[index], value[index + 1]);
                index += 1;
            }

            if (ShouldSkipCodePoint(codePoint))
            {
                continue;
            }

            codePoints.Add(codePoint);
        }
    }

    private static bool ShouldSkipCodePoint(uint codePoint)
    {
        return codePoint == '\r' || codePoint == '\n' || codePoint == '\t' || codePoint == 0;
    }

    private static string BuildCharacterString(IEnumerable<uint> codePoints)
    {
        StringBuilder builder = new StringBuilder();
        foreach (uint codePoint in codePoints)
        {
            if (codePoint <= 0x10FFFF)
            {
                builder.Append(char.ConvertFromUtf32((int)codePoint));
            }
        }

        return builder.ToString();
    }

    private static string BuildPreview(string source, int maxCodeUnits)
    {
        if (string.IsNullOrEmpty(source))
        {
            return string.Empty;
        }

        if (source.Length <= maxCodeUnits)
        {
            return source;
        }

        return source.Substring(0, maxCodeUnits) + "...";
    }

    private static IEnumerable<string> ExtractCSharpStringLiterals(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            yield break;
        }

        int index = 0;
        while (index < source.Length)
        {
            char current = source[index];

            if (current == '/' && index + 1 < source.Length)
            {
                if (source[index + 1] == '/')
                {
                    index += 2;
                    while (index < source.Length && source[index] != '\n')
                    {
                        index += 1;
                    }

                    continue;
                }

                if (source[index + 1] == '*')
                {
                    index += 2;
                    while (index + 1 < source.Length && !(source[index] == '*' && source[index + 1] == '/'))
                    {
                        index += 1;
                    }

                    index = Math.Min(index + 2, source.Length);
                    continue;
                }
            }

            if (current == '"')
            {
                bool isVerbatim = index > 0 && source[index - 1] == '@';
                StringBuilder literal = new StringBuilder();
                index += 1;

                while (index < source.Length)
                {
                    char value = source[index];

                    if (isVerbatim)
                    {
                        if (value == '"' && index + 1 < source.Length && source[index + 1] == '"')
                        {
                            literal.Append('"');
                            index += 2;
                            continue;
                        }

                        if (value == '"')
                        {
                            index += 1;
                            break;
                        }

                        literal.Append(value);
                        index += 1;
                        continue;
                    }

                    if (value == '\\' && index + 1 < source.Length)
                    {
                        char escaped = source[index + 1];
                        literal.Append(UnescapeCSharpCharacter(escaped));
                        index += 2;
                        continue;
                    }

                    if (value == '"')
                    {
                        index += 1;
                        break;
                    }

                    literal.Append(value);
                    index += 1;
                }

                if (literal.Length > 0)
                {
                    yield return literal.ToString();
                }

                continue;
            }

            if (current == '\'' && index + 2 < source.Length)
            {
                index += 1;
                if (source[index] == '\\')
                {
                    index += 2;
                }
                else
                {
                    index += 1;
                }

                if (index < source.Length && source[index] == '\'')
                {
                    index += 1;
                }

                continue;
            }

            index += 1;
        }
    }

    private static char UnescapeCSharpCharacter(char escaped)
    {
        switch (escaped)
        {
            case '\'':
                return '\'';
            case '"':
                return '"';
            case '\\':
                return '\\';
            case '0':
                return '\0';
            case 'a':
                return '\a';
            case 'b':
                return '\b';
            case 'f':
                return '\f';
            case 'n':
                return '\n';
            case 'r':
                return '\r';
            case 't':
                return '\t';
            case 'v':
                return '\v';
            default:
                return escaped;
        }
    }

    private static Font GetEditorSourceFont(TMP_FontAsset fontAsset)
    {
        SerializedObject serializedObject = new SerializedObject(fontAsset);
        SerializedProperty property = serializedObject.FindProperty("m_SourceFontFile_EditorRef");
        return property != null ? property.objectReferenceValue as Font : null;
    }

    private static Scene FindLoadedSceneByPath(string scenePath)
    {
        for (int index = 0; index < UnityEngine.SceneManagement.SceneManager.sceneCount; index++)
        {
            Scene loadedScene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(index);
            if (string.Equals(loadedScene.path, scenePath, StringComparison.OrdinalIgnoreCase))
            {
                return loadedScene;
            }
        }

        return default;
    }

    private static void ForceSourceFontReference(TMP_FontAsset fontAsset, Font sourceFont)
    {
        SerializedObject serializedObject = new SerializedObject(fontAsset);
        SerializedProperty editorRefProperty = serializedObject.FindProperty("m_SourceFontFile_EditorRef");
        SerializedProperty runtimeRefProperty = serializedObject.FindProperty("m_SourceFontFile");
        SerializedProperty guidProperty = serializedObject.FindProperty("m_SourceFontFileGUID");

        if (editorRefProperty != null)
        {
            editorRefProperty.objectReferenceValue = sourceFont;
        }

        if (runtimeRefProperty != null)
        {
            runtimeRefProperty.objectReferenceValue = sourceFont;
        }

        if (guidProperty != null)
        {
            guidProperty.stringValue = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sourceFont));
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetAtlasTexturesReadable(TMP_FontAsset fontAsset, bool isReadable)
    {
        Texture2D[] atlasTextures = fontAsset.atlasTextures;
        if (atlasTextures == null)
        {
            return;
        }

        foreach (Texture2D atlasTexture in atlasTextures)
        {
            if (atlasTexture == null || atlasTexture.isReadable == isReadable)
            {
                continue;
            }

            if (SetAtlasTextureReadableMethod == null)
            {
                throw new MissingMethodException("Could not resolve UnityEditor.TextCore.LowLevel.FontEngineEditorUtilities.SetAtlasTextureIsReadable.");
            }

            SetAtlasTextureReadableMethod?.Invoke(null, new object[] { atlasTexture, isReadable });
        }
    }

    private void ShowProgress(string title, string currentPath, int index, int total)
    {
        float progress = total <= 0 ? 0f : (index + 1f) / total;
        EditorUtility.DisplayProgressBar(title, currentPath, progress);
    }

    private string GetScanRootPath()
    {
        if (scanRootFolder == null)
        {
            return DefaultScanRoot;
        }

        string assetPath = AssetDatabase.GetAssetPath(scanRootFolder);
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return DefaultScanRoot;
        }

        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return assetPath;
        }

        string directory = Path.GetDirectoryName(assetPath);
        return string.IsNullOrWhiteSpace(directory) ? DefaultScanRoot : directory.Replace('\\', '/');
    }

    private string[] ParseExcludedPaths()
    {
        return (excludedPaths ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => path.Trim().Replace('\\', '/'))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void AppendLog(string message)
    {
        logBuilder.Append('[');
        logBuilder.Append(DateTime.Now.ToString("HH:mm:ss"));
        logBuilder.Append("] ");
        logBuilder.AppendLine(message);
        Repaint();
    }
}
