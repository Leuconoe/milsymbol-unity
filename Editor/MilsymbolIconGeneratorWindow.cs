using System;
using System.Collections.Generic;
using System.IO;
using Leuconoe.MilsymbolUnity;
using UnityEditor;
using UnityEngine;

namespace Leuconoe.MilsymbolUnity.Editor
{
    public sealed class MilsymbolIconGeneratorWindow : EditorWindow
    {
        private const string DefaultLetterSidc = "SFAPMFQ----B---";
        private const string NodeExecutablePrefsKey = "Leuconoe.MilsymbolUnity.NodeExecutable";
        private const string OutputFolderPrefsKey = "Leuconoe.MilsymbolUnity.OutputFolder";
        private const string DefaultOutputFolder = "Assets/Milsymbol Icons";
        private const string TextureSizePrefsKey = "Leuconoe.MilsymbolUnity.TextureSize";
        private const int DefaultTextureSize = 128;
        private const string BatchSidcPrefsKey = "Leuconoe.MilsymbolUnity.BatchSidc";

        private static readonly int[] TextureSizeValues = { 32, 64, 128, 256, 512, 1024, 2048 };
        private static readonly string[] TextureSizeLabels = { "32", "64", "128", "256", "512", "1024", "2048" };
        private const double AutoPreviewDelaySeconds = 0.45d;

        [SerializeField] private MilsymbolIconRequest request = new MilsymbolIconRequest
        {
            sidc = DefaultLetterSidc
        };

        [SerializeField] private string outputFolder = DefaultOutputFolder;
        [SerializeField] private bool createRuntimeAsset = true;
        [SerializeField] private int pngWidth = 512;
        [SerializeField] private int pngHeight = 512;
        [SerializeField] private int pngAntiAliasing = 1;
        [SerializeField] private int textureSize = DefaultTextureSize;
        [SerializeField] private bool autoPreview = true;
        [SerializeField] private bool useSidcBuilder = true;
        [SerializeField] private bool showStyle;
        [SerializeField] private bool showAdvancedSave;
        [SerializeField] private bool showMoreSaveOptions;
        [SerializeField] private bool showGeneratedSource;
        [SerializeField] private int sidcCodingSchemeIndex;
        [SerializeField] private int sidcAffiliationIndex = 3;
        [SerializeField] private int sidcBattleDimensionIndex = 1;
        [SerializeField] private int sidcStatusIndex;
        [SerializeField] private int sidcSymbolPartOneIndex;
        [SerializeField] private int sidcSymbolDomainIndex;
        [SerializeField] private int sidcSpecificSymbolIndex = 2;
        [SerializeField] private int sidcSpecificModifierIndex;
        [SerializeField] private string sidcFunctionId = "MFQ---";
        [SerializeField] private int sidcModifierOneIndex;
        [SerializeField] private int sidcModifierTwoIndex = 2;
        [SerializeField] private string sidcCountryCode = "--";
        [SerializeField] private int sidcOrderOfBattleIndex;

        private string nodeExecutable;
        private string batchSidc = "";
        private bool showNodeSettings;
        private MilsymbolSvgGenerator.Result lastResult;
        private string editableSvg = "";
        private Texture2D previewTexture;
        private string previewStatus = "";
        private Vector2 scroll;
        private string status = "";
        private bool autoPreviewQueued;
        private double autoPreviewRunAt;

        private readonly struct SidcCharOption
        {
            public SidcCharOption(string label, char value)
            {
                Label = label;
                Value = value;
            }

            public string Label { get; }
            public char Value { get; }
        }

        private readonly struct SidcStringOption
        {
            public SidcStringOption(string label, string value)
            {
                Label = label;
                Value = value;
            }

            public string Label { get; }
            public string Value { get; }
        }

        private readonly struct SidcSymbolOption
        {
            public SidcSymbolOption(string label, string generatorCode, string functionId)
            {
                Label = label;
                GeneratorCode = generatorCode;
                FunctionId = functionId;
            }

            public string Label { get; }
            public string GeneratorCode { get; }
            public string FunctionId { get; }
        }

        private static readonly SidcCharOption[] CodingSchemes =
        {
            new SidcCharOption("Warfighting (S)", 'S'),
            new SidcCharOption("Tactical Graphics (G)", 'G'),
            new SidcCharOption("METOC (W)", 'W'),
            new SidcCharOption("Signals Intelligence (I)", 'I'),
            new SidcCharOption("Stability Operations (O)", 'O'),
            new SidcCharOption("Emergency Management (E)", 'E')
        };

        private static readonly SidcCharOption[] Affiliations =
        {
            new SidcCharOption("Pending (P)", 'P'),
            new SidcCharOption("Unknown (U)", 'U'),
            new SidcCharOption("Assumed Friend (A)", 'A'),
            new SidcCharOption("Friend (F)", 'F'),
            new SidcCharOption("Neutral (N)", 'N'),
            new SidcCharOption("Suspect (S)", 'S'),
            new SidcCharOption("Hostile (H)", 'H'),
            new SidcCharOption("Exercise Friend (D)", 'D'),
            new SidcCharOption("Joker (J)", 'J'),
            new SidcCharOption("Faker (K)", 'K')
        };

        private static readonly SidcCharOption[] BattleDimensions =
        {
            new SidcCharOption("Space (P)", 'P'),
            new SidcCharOption("Air (A)", 'A'),
            new SidcCharOption("Ground (G)", 'G'),
            new SidcCharOption("Sea Surface (S)", 'S'),
            new SidcCharOption("Subsurface (U)", 'U'),
            new SidcCharOption("SOF (F)", 'F'),
            new SidcCharOption("Other (X)", 'X'),
            new SidcCharOption("Unknown (Z)", 'Z')
        };

        private static readonly SidcCharOption[] Statuses =
        {
            new SidcCharOption("Present (P)", 'P'),
            new SidcCharOption("Planned/Anticipated (A)", 'A'),
            new SidcCharOption("Fully Capable (C)", 'C'),
            new SidcCharOption("Damaged (D)", 'D'),
            new SidcCharOption("Destroyed (X)", 'X'),
            new SidcCharOption("Full To Capacity (F)", 'F')
        };

        private static readonly SidcStringOption[] FunctionIds =
        {
            new SidcStringOption("Air / UAV (MFQ---)", "MFQ---"),
            new SidcStringOption("Ground / Infantry (UCI---)", "UCI---"),
            new SidcStringOption("Equipment / Rifle Long Range (EWRH--)", "EWRH--"),
            new SidcStringOption("None / Unknown (------)", "------"),
            new SidcStringOption("Custom", "")
        };

        private static readonly SidcCharOption[] SymbolPartOnes =
        {
            new SidcCharOption("Unit (U)", 'U'),
            new SidcCharOption("Equipment (E)", 'E'),
            new SidcCharOption("Installation (I)", 'I')
        };

        private static readonly SidcCharOption[] SymbolDomains =
        {
            new SidcCharOption("Air (A)", 'A'),
            new SidcCharOption("Ground (G)", 'G'),
            new SidcCharOption("Sea Surface (S)", 'S'),
            new SidcCharOption("Subsurface (U)", 'U')
        };

        private static readonly SidcSymbolOption[] AirSpecificSymbols =
        {
            new SidcSymbolOption("Aircraft, Fixed Wing (UUAA)", "UUAA", "MF----"),
            new SidcSymbolOption("Aircraft, Rotary Wing (UUAB)", "UUAB", "MH----"),
            new SidcSymbolOption("Unmanned Aerial Vehicle (UUAC)", "UUAC", "MFQ---"),
            new SidcSymbolOption("Missile (UUAE)", "UUAE", "WM----"),
            new SidcSymbolOption("Custom Function ID", "", "")
        };

        private static readonly SidcSymbolOption[] GroundSpecificSymbols =
        {
            new SidcSymbolOption("Infantry (UUGA)", "UUGA", "UCI---"),
            new SidcSymbolOption("Mechanized Infantry (UUGB)", "UUGB", "UCIZ--"),
            new SidcSymbolOption("Motorized Infantry (UUGC)", "UUGC", "UCIM--"),
            new SidcSymbolOption("Reconnaissance (UUGE)", "UUGE", "UCR---"),
            new SidcSymbolOption("Special Forces (UUGF)", "UUGF", "UCF---"),
            new SidcSymbolOption("Artillery (UUGJ)", "UUGJ", "UCD---"),
            new SidcSymbolOption("Mortar (UUGM)", "UUGM", "UCDM--"),
            new SidcSymbolOption("Custom Function ID", "", "")
        };

        private static readonly SidcSymbolOption[] SeaSpecificSymbols =
        {
            new SidcSymbolOption("Carrier (UUSA)", "UUSA", "CLCV--"),
            new SidcSymbolOption("Cruiser (UUSB)", "UUSB", "CLCC--"),
            new SidcSymbolOption("Destroyer (UUSC)", "UUSC", "CLDD--"),
            new SidcSymbolOption("Frigate (UUSD)", "UUSD", "CLFF--"),
            new SidcSymbolOption("Patrol Craft (UUSF)", "UUSF", "CLPT--"),
            new SidcSymbolOption("Custom Function ID", "", "")
        };

        private static readonly SidcSymbolOption[] SubsurfaceSpecificSymbols =
        {
            new SidcSymbolOption("Submarine (UUUA)", "UUUA", "SN----"),
            new SidcSymbolOption("Submarine Nuclear Ballistic Missile (UUUB)", "UUUB", "SNB---"),
            new SidcSymbolOption("Submarine Nuclear Attack (UUUC)", "UUUC", "SNA---"),
            new SidcSymbolOption("Submarine Diesel Attack (UUUD)", "UUUD", "SC----"),
            new SidcSymbolOption("Custom Function ID", "", "")
        };

        private static readonly SidcCharOption[] ModifierOnes =
        {
            new SidcCharOption("None (-)", '-'),
            new SidcCharOption("Headquarters (A)", 'A'),
            new SidcCharOption("Task Force (E)", 'E'),
            new SidcCharOption("Feint/Dummy (F)", 'F'),
            new SidcCharOption("Installation (H)", 'H'),
            new SidcCharOption("Mobility (M)", 'M'),
            new SidcCharOption("Towed Array (N)", 'N')
        };

        private static readonly SidcCharOption[] ModifierTwos =
        {
            new SidcCharOption("None (-)", '-'),
            new SidcCharOption("Team/Crew (A)", 'A'),
            new SidcCharOption("Squad (B)", 'B'),
            new SidcCharOption("Section (C)", 'C'),
            new SidcCharOption("Platoon/Detachment (D)", 'D'),
            new SidcCharOption("Company/Battery/Troop (E)", 'E'),
            new SidcCharOption("Battalion/Squadron (F)", 'F'),
            new SidcCharOption("Regiment/Group (G)", 'G'),
            new SidcCharOption("Brigade (H)", 'H'),
            new SidcCharOption("Division (I)", 'I'),
            new SidcCharOption("Corps/MEF (J)", 'J'),
            new SidcCharOption("Army (K)", 'K')
        };

        private static readonly SidcCharOption[] OrderOfBattleOptions =
        {
            new SidcCharOption("None (-)", '-'),
            new SidcCharOption("Air (A)", 'A'),
            new SidcCharOption("Electronic (E)", 'E'),
            new SidcCharOption("Civilian (C)", 'C'),
            new SidcCharOption("Ground (G)", 'G'),
            new SidcCharOption("Maritime (N)", 'N'),
            new SidcCharOption("Strategic Force (S)", 'S')
        };

        [MenuItem("Tools/Milsymbol/Icon Generator")]
        public static void Open()
        {
            var window = GetWindow<MilsymbolIconGeneratorWindow>("Milsymbol Icons");
            window.minSize = new Vector2(820, 600);
            window.Show();
        }

        private void OnEnable()
        {
            nodeExecutable = EditorPrefs.GetString(NodeExecutablePrefsKey, "node");
            // Persist the output folder across full editor restarts (SerializeField only
            // survives domain reloads, not a restart).
            outputFolder = EditorPrefs.GetString(OutputFolderPrefsKey, DefaultOutputFolder);
            textureSize = EditorPrefs.GetInt(TextureSizePrefsKey, DefaultTextureSize);
            batchSidc = EditorPrefs.GetString(BatchSidcPrefsKey, "");
            // Expand the Node setup section only when setup is incomplete; collapse it once
            // the submodule and Node dependencies are ready.
            showNodeSettings = !IsNodeSetupComplete();
            if (request == null)
            {
                request = new MilsymbolIconRequest();
            }

            if (string.IsNullOrWhiteSpace(request.sidc) || request.sidc == "130310001412110000000000000000")
            {
                request.sidc = DefaultLetterSidc;
            }

            SyncBuilderFromSidc(request.sidc);
            request.iconOnly = true;
            lastResult = null;
            editableSvg = "";
            ClearPreview();
            status = "";
        }

        private void OnDisable()
        {
            EditorApplication.update -= RunQueuedAutoPreview;
            autoPreviewQueued = false;
            ClearPreview();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            var setupReady = IsNodeSetupComplete();
            var generatorHeader = setupReady ? "Generator (ready)" : "Generator (setup required)";
            showNodeSettings = EditorGUILayout.Foldout(showNodeSettings, generatorHeader, true, EditorStyles.foldoutHeader);
            if (showNodeSettings)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    nodeExecutable = EditorGUILayout.TextField(
                        FieldLabel("Node Executable", "Path to the node binary. Leave as 'node' to auto-detect common install locations."),
                        nodeExecutable);
                    if (GUILayout.Button("Save Node Setting"))
                    {
                        EditorPrefs.SetString(NodeExecutablePrefsKey, string.IsNullOrWhiteSpace(nodeExecutable) ? "node" : nodeExecutable);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(
                            "milsymbol (npm)",
                            MilsymbolNodeDependencyInstaller.AreDependenciesInstalled() ? "Installed" : "Missing");

                        if (GUILayout.Button("Install", GUILayout.Width(96)))
                        {
                            if (MilsymbolNodeDependencyInstaller.InstallWithDialog())
                            {
                                status = "milsymbol is installed.";
                            }
                        }
                    }
                }
            }

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(460)))
                {
                    EditorGUI.BeginChangeCheck();

                    EditorGUILayout.LabelField("Symbol", EditorStyles.boldLabel);

                    // SIDC is always directly editable, even while the builder is shown.
                    EditorGUI.BeginChangeCheck();
                    var typedSidc = EditorGUILayout.TextField(
                        FieldLabel("SIDC", "15-character letter SIDC. Editable directly even when the builder is on; typing here syncs the builder."),
                        request.sidc);
                    var sidcEditedManually = EditorGUI.EndChangeCheck();
                    if (sidcEditedManually)
                    {
                        request.sidc = typedSidc;
                        if (useSidcBuilder)
                        {
                            // Keep the builder dropdowns in step with the typed SIDC.
                            SyncBuilderFromSidc(typedSidc);
                        }
                    }

                    EditorGUI.BeginChangeCheck();
                    var builderEnabled = EditorGUILayout.Toggle(
                        FieldLabel("Use SIDC Builder", "Build the SIDC from dropdowns. Turn off to type a SIDC freely."),
                        useSidcBuilder);
                    if (EditorGUI.EndChangeCheck())
                    {
                        useSidcBuilder = builderEnabled;
                        if (useSidcBuilder)
                        {
                            SyncBuilderFromSidc(request.sidc);
                        }
                    }

                    if (useSidcBuilder)
                    {
                        EditorGUI.BeginChangeCheck();
                        DrawSidcBuilder();
                        // Only rebuild from the dropdowns when they actually change, so a
                        // manual SIDC edit in the same frame is not overwritten.
                        if (EditorGUI.EndChangeCheck())
                        {
                            request.sidc = BuildSidcFromBuilder();
                        }
                    }

                    request.standard = (MilsymbolStandard)EditorGUILayout.EnumPopup(
                        FieldLabel("Standard", "Symbol standard milsymbol renders with: Auto / MIL-STD-2525 / APP-6."),
                        request.standard);
                    request.iconOnly = true;
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.Toggle("Icon Only", true);
                    }

                    EditorGUILayout.Space(8);
                    var style = request.style ?? (request.style = new MilsymbolIconStyle());
                    showStyle = EditorGUILayout.Foldout(showStyle, "Style", true);
                    if (showStyle)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            style.size = EditorGUILayout.IntSlider("Size", style.size, 16, 512);
                            style.frame = EditorGUILayout.Toggle("Frame", style.frame);
                            style.fill = EditorGUILayout.Toggle("Fill", style.fill);
                            style.square = EditorGUILayout.Toggle("Square Canvas", style.square);
                            style.civilianColor = EditorGUILayout.Toggle("Civilian Color", style.civilianColor);
                            style.alternateMedal = EditorGUILayout.Toggle("Alternate Medal", style.alternateMedal);
                            style.fillOpacity = EditorGUILayout.Slider("Fill Opacity", style.fillOpacity, 0f, 1f);
                            style.strokeWidth = EditorGUILayout.Slider("Stroke Width", style.strokeWidth, 0f, 16f);
                            style.outlineWidth = EditorGUILayout.Slider("Outline Width", style.outlineWidth, 0f, 32f);
                            style.colorMode = DrawColorMode("Color Mode", style.colorMode);
                            style.monoColor = DrawOptionalColor("Mono Color", style.monoColor, Color.black);
                            style.fillColor = DrawOptionalColor("Fill Color", style.fillColor, new Color(128f / 255f, 224f / 255f, 1f));
                            style.frameColor = DrawOptionalColor("Frame Color", style.frameColor, Color.black);
                            style.iconColor = DrawOptionalColor("Icon Color", style.iconColor, Color.black);
                            style.outlineColor = DrawOptionalColor("Outline Color", style.outlineColor, new Color(239f / 255f, 239f / 255f, 239f / 255f));

                            if (style.fill && style.fillOpacity < 1f && GUILayout.Button("Make Fill Opaque"))
                            {
                                style.fillOpacity = 1f;
                            }
                        }
                    }

                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var editedFolder = EditorGUILayout.TextField(
                            FieldLabel("Output Folder", "Project-relative folder under Assets where generated PNG/.asset files are saved. Persists across restarts."),
                            outputFolder);
                        if (editedFolder != outputFolder)
                        {
                            SetOutputFolder(editedFolder);
                        }
                        if (GUILayout.Button("...", GUILayout.Width(32)))
                        {
                            PickOutputFolder();
                        }
                    }

                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.TextField("File Name", GetAutomaticPngFileName());
                    }

                    var editedTextureSize = EditorGUILayout.IntPopup(
                        FieldLabel("Texture Size", "Imported sprite max size (TextureImporter.maxTextureSize). Independent of the rendered PNG resolution."),
                        textureSize,
                        Array.ConvertAll(TextureSizeLabels, label => new GUIContent(label)),
                        TextureSizeValues);
                    if (editedTextureSize != textureSize)
                    {
                        textureSize = editedTextureSize;
                        EditorPrefs.SetInt(TextureSizePrefsKey, textureSize);
                    }

                    createRuntimeAsset = EditorGUILayout.Toggle("Create .asset", createRuntimeAsset);
                    autoPreview = EditorGUILayout.Toggle("Auto Preview", autoPreview);

                    showAdvancedSave = EditorGUILayout.Foldout(showAdvancedSave, "Advanced PNG", true);
                    if (showAdvancedSave)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            pngWidth = EditorGUILayout.IntSlider("Width", pngWidth, 16, 4096);
                            pngHeight = EditorGUILayout.IntSlider("Height", pngHeight, 16, 4096);
                            pngAntiAliasing = EditorGUILayout.IntSlider("Anti Aliasing", pngAntiAliasing, 1, 4);
                        }
                    }

                    var generationInputChanged = EditorGUI.EndChangeCheck();
                    if (generationInputChanged && autoPreview)
                    {
                        QueueAutoPreview();
                    }
                }

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(300)))
                {
                    DrawPreview();
                    DrawActions();
                    DrawResult();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void Generate(bool manual)
        {
            try
            {
                request.iconOnly = true;
                lastResult = MilsymbolSvgGenerator.Generate(request, nodeExecutable);
                editableSvg = FormatSvgForEditing(lastResult.svg);
                if (lastResult.width > 0f && lastResult.height > 0f)
                {
                    pngWidth = Mathf.Clamp(Mathf.CeilToInt(lastResult.width), 16, 4096);
                    pngHeight = Mathf.Clamp(Mathf.CeilToInt(lastResult.height), 16, 4096);
                }

                GeneratePreview();

                status = lastResult.valid
                    ? "Generated valid icon source."
                    : "Generated icon source, but milsymbol reported an invalid SIDC/icon.";
            }
            catch (Exception exception)
            {
                lastResult = null;
                status = exception.Message;
                if (manual)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private void QueueAutoPreview()
        {
            autoPreviewRunAt = EditorApplication.timeSinceStartup + AutoPreviewDelaySeconds;
            if (autoPreviewQueued)
            {
                return;
            }

            autoPreviewQueued = true;
            EditorApplication.update += RunQueuedAutoPreview;
        }

        private void RunQueuedAutoPreview()
        {
            if (EditorApplication.timeSinceStartup < autoPreviewRunAt)
            {
                return;
            }

            EditorApplication.update -= RunQueuedAutoPreview;
            autoPreviewQueued = false;

            if (!autoPreview)
            {
                return;
            }

            Generate(false);
            Repaint();
        }

        private void GeneratePreview()
        {
            ClearPreview();
            previewStatus = "";

            if (lastResult == null)
            {
                return;
            }

            if (!MilsymbolNodeDependencyInstaller.AreDependenciesInstalled())
            {
                previewStatus = "Install Node dependencies to render preview.";
                return;
            }

            var tempRoot = Path.Combine(Path.GetTempPath(), "milsymbol-unity");
            Directory.CreateDirectory(tempRoot);
            var previewPath = Path.Combine(tempRoot, Guid.NewGuid().ToString("N") + "-preview.png");

            try
            {
                var previewSize = CalculatePreviewSize(lastResult.width, lastResult.height);
                MilsymbolPngExporter.SavePng(request, previewPath, previewSize.x, previewSize.y, nodeExecutable);

                var bytes = File.ReadAllBytes(previewPath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                if (!ImageConversion.LoadImage(texture, bytes))
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    previewStatus = "Preview PNG could not be loaded.";
                    return;
                }

                previewTexture = texture;
            }
            catch (Exception exception)
            {
                previewStatus = exception.Message;
                Debug.LogException(exception);
            }
            finally
            {
                TryDelete(previewPath);
            }
        }

        private void Save()
        {
            if (lastResult == null || string.IsNullOrEmpty(lastResult.svg))
            {
                status = "Generate an icon before saving.";
                return;
            }

            try
            {
                if (!MilsymbolNodeDependencyInstaller.EnsureInstalledOrPrompt())
                {
                    status = "PNG export canceled because Node dependencies are missing.";
                    return;
                }

                var pngPath = MilsymbolSvgGenerator.SavePng(outputFolder, request, pngWidth, pngHeight, nodeExecutable, textureSize);
                if (createRuntimeAsset)
                {
                    MilsymbolSvgGenerator.SaveIconAsset(pngPath, request, lastResult);
                }

                AssetDatabase.Refresh();
                status = "Saved " + pngPath;
            }
            catch (Exception exception)
            {
                status = exception.Message;
                Debug.LogException(exception);
            }
        }

        private static bool IsNodeSetupComplete()
        {
            return MilsymbolNodeDependencyInstaller.AreDependenciesInstalled();
        }

        private void SetOutputFolder(string folder)
        {
            outputFolder = folder;
            EditorPrefs.SetString(OutputFolderPrefsKey, string.IsNullOrWhiteSpace(folder) ? DefaultOutputFolder : folder);
        }

        private void PickOutputFolder()
        {
            var absoluteStart = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), outputFolder);
            if (!Directory.Exists(absoluteStart))
            {
                absoluteStart = Application.dataPath;
            }

            var selected = EditorUtility.OpenFolderPanel("Select PNG Output Folder", absoluteStart, "");
            if (string.IsNullOrEmpty(selected))
            {
                return;
            }

            try
            {
                SetOutputFolder(MilsymbolSvgGenerator.NormalizeAssetFolder(selected));
            }
            catch (Exception exception)
            {
                status = exception.Message;
                Debug.LogException(exception);
            }

            // The Output Folder TextField was already drawn with the old value this OnGUI
            // pass, and IMGUI keeps showing the focused editing buffer. Drop keyboard focus
            // so the field re-reads `outputFolder`, and repaint so the change shows now
            // instead of only after the next editor refresh.
            GUIUtility.keyboardControl = 0;
            EditorGUIUtility.editingTextField = false;
            Repaint();
        }

        private void SavePngAs()
        {
            var defaultName = GetAutomaticPngFileName();
            var selected = EditorUtility.SaveFilePanel("Save Milsymbol PNG", Application.dataPath, defaultName, "png");
            if (string.IsNullOrEmpty(selected))
            {
                return;
            }

            try
            {
                if (!MilsymbolNodeDependencyInstaller.EnsureInstalledOrPrompt())
                {
                    status = "PNG export canceled because Node dependencies are missing.";
                    return;
                }

                MilsymbolPngExporter.SavePng(request, selected, pngWidth, pngHeight, nodeExecutable);
                AssetDatabase.Refresh();
                status = "Saved PNG " + selected;
            }
            catch (Exception exception)
            {
                status = exception.Message;
                Debug.LogException(exception);
            }
        }

        private bool CanSaveEditedSvg()
        {
            return lastResult != null && !string.IsNullOrWhiteSpace(editableSvg);
        }

        private string GetAutomaticPngFileName()
        {
            return MilsymbolSvgGenerator.CreateSidcFileName(request.sidc, ".png");
        }

        private void DrawActions()
        {
            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate", GUILayout.Height(28)))
                {
                    Generate(true);
                }

                using (new EditorGUI.DisabledScope(!CanSaveEditedSvg()))
                {
                    if (GUILayout.Button("Save PNG", GUILayout.Height(28)))
                    {
                        Save();
                    }
                }
            }

            showMoreSaveOptions = EditorGUILayout.Foldout(showMoreSaveOptions, "More Save Options", true);
            if (!showMoreSaveOptions)
            {
                return;
            }

            using (new EditorGUI.DisabledScope(!CanSaveEditedSvg()))
            {
                if (GUILayout.Button("Save PNG As Local File...", GUILayout.Height(28)))
                {
                    SavePngAs();
                }
            }

            if (GUILayout.Button("Generate And Save", GUILayout.Height(28)))
            {
                Generate(true);
                if (CanSaveEditedSvg())
                {
                    Save();
                }
            }

            DrawBatch();
        }

        private void DrawBatch()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Batch", EditorStyles.boldLabel);

            var batchTooltip = new GUIContent(
                "SIDCs (comma-separated)",
                "Enter one or more 15-character letter SIDCs separated by commas " +
                "(semicolons or new lines also work).\n" +
                "Each is rendered with the current Standard, Style and Texture Size, then saved as a " +
                "PNG (and optional .asset) into the Output Folder. Duplicates are skipped.\n" +
                "Example: SFAPMFQR-------,SHGPEWT--------");
            EditorGUILayout.LabelField(batchTooltip, EditorStyles.miniLabel);

            var wrapStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            using (new EditorGUILayout.HorizontalScope())
            {
                // wordWrap keeps a long comma-separated line from stretching the layout.
                var edited = EditorGUILayout.TextArea(
                    batchSidc ?? "",
                    wrapStyle,
                    GUILayout.Height(64),
                    GUILayout.ExpandWidth(true),
                    GUILayout.MaxWidth(360));
                if (edited != batchSidc)
                {
                    batchSidc = edited;
                    EditorPrefs.SetString(BatchSidcPrefsKey, batchSidc ?? "");
                }

                var batchButton = new GUIContent(
                    "Generate\nTo Folder",
                    "Generate every SIDC above into the Output Folder.");
                if (GUILayout.Button(batchButton, GUILayout.Width(96), GUILayout.Height(64)))
                {
                    GenerateBatchToOutputFolder();
                }
            }
        }

        private void GenerateBatchToOutputFolder()
        {
            var sidcs = (batchSidc ?? "").Split(new[] { ',', '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries);
            var unique = new List<string>();
            foreach (var raw in sidcs)
            {
                var trimmed = raw.Trim();
                if (trimmed.Length > 0 && !unique.Contains(trimmed))
                {
                    unique.Add(trimmed);
                }
            }

            if (unique.Count == 0)
            {
                status = "Enter one or more SIDCs (comma-separated) before batch generating.";
                return;
            }

            if (!MilsymbolNodeDependencyInstaller.EnsureInstalledOrPrompt())
            {
                status = "Batch canceled because Node dependencies are missing.";
                return;
            }

            var generated = 0;
            var failures = new List<string>();
            try
            {
                for (var i = 0; i < unique.Count; i++)
                {
                    var sidc = unique[i];
                    EditorUtility.DisplayProgressBar("Milsymbol Batch", "Generating " + sidc, (float)i / unique.Count);
                    try
                    {
                        var req = new MilsymbolIconRequest
                        {
                            sidc = sidc,
                            standard = request.standard,
                            iconOnly = true,
                            style = request.style
                        };

                        var result = MilsymbolSvgGenerator.Generate(req, nodeExecutable);
                        var pngPath = MilsymbolSvgGenerator.SavePng(outputFolder, req, pngWidth, pngHeight, nodeExecutable, textureSize);
                        if (createRuntimeAsset)
                        {
                            MilsymbolSvgGenerator.SaveIconAsset(pngPath, req, result);
                        }

                        generated++;
                    }
                    catch (Exception exception)
                    {
                        failures.Add(sidc + ": " + exception.Message);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();

            status = "Batch generated " + generated + "/" + unique.Count + " into " + outputFolder + ".";
            if (failures.Count > 0)
            {
                status += "\nFailed: " + string.Join("; ", failures);
                Debug.LogWarning("Milsymbol batch failures:\n" + string.Join("\n", failures));
            }
        }

        private void DrawSidcBuilder()
        {
            sidcCodingSchemeIndex = DrawPopup("Coding Scheme", "SIDC position 1: symbology set (S=Warfighting, G=Tactical Graphics, W=METOC, I=SIGINT, ...).", sidcCodingSchemeIndex, CodingSchemes);
            sidcAffiliationIndex = DrawPopup("Affiliation", "SIDC position 2: friend / hostile / neutral / unknown and exercise variants.", sidcAffiliationIndex, Affiliations);
            sidcBattleDimensionIndex = DrawPopup("Battle Dimension", "SIDC position 3: Air / Ground / Sea Surface / Subsurface / Space / SOF.", sidcBattleDimensionIndex, BattleDimensions);
            sidcStatusIndex = DrawPopup("Status", "SIDC position 4: Present / Planned / Fully Capable / Damaged / Destroyed.", sidcStatusIndex, Statuses);

            sidcSymbolPartOneIndex = DrawPopup("Symbol Code Part 1", "Entity kind helper: Unit / Equipment / Installation.", sidcSymbolPartOneIndex, SymbolPartOnes);
            sidcSymbolDomainIndex = DrawPopup("Symbol Code Domain", "Entity domain used to list the specific symbols below.", sidcSymbolDomainIndex, SymbolDomains);

            var specificSymbols = SpecificSymbolsForDomain(SymbolDomains[ClampIndex(sidcSymbolDomainIndex, SymbolDomains.Length)].Value);
            sidcSpecificSymbolIndex = EditorGUILayout.Popup(
                FieldLabel("Specific Symbol", "Common entity for this domain. For anything more granular, type the SIDC directly above."),
                ClampIndex(sidcSpecificSymbolIndex, specificSymbols.Length),
                Labels(specificSymbols));
            var selectedSpecificSymbol = specificSymbols[ClampIndex(sidcSpecificSymbolIndex, specificSymbols.Length)];

            // Variable sub-type (e.g. UAV/aircraft role): enabled only for symbols that
            // define variants; disabled for the rest.
            var variants = VariantsForSymbol(selectedSpecificSymbol);
            var hasVariants = variants.Length > 1;
            using (new EditorGUI.DisabledScope(!hasVariants))
            {
                sidcSpecificModifierIndex = EditorGUILayout.Popup(
                    FieldLabel("Variant", "Sub-type appended to the function id (e.g. UAV role: Reconnaissance/Attack/Bomber). Disabled for symbols without variants."),
                    ClampIndex(sidcSpecificModifierIndex, variants.Length),
                    Labels(variants));
            }

            if (!string.IsNullOrEmpty(selectedSpecificSymbol.FunctionId))
            {
                if (hasVariants)
                {
                    var baseCode = selectedSpecificSymbol.FunctionId.TrimEnd('-');
                    var suffix = variants[ClampIndex(sidcSpecificModifierIndex, variants.Length)].Value;
                    sidcFunctionId = NormalizePart(baseCode + suffix, 6, "------");
                }
                else
                {
                    sidcFunctionId = selectedSpecificSymbol.FunctionId;
                }
            }
            else
            {
                sidcFunctionId = EditorGUILayout.TextField(
                    FieldLabel("Custom Function ID", "Raw 6-char function id (SIDC positions 5-10), e.g. MFQR--."),
                    sidcFunctionId).ToUpperInvariant();
            }

            sidcModifierOneIndex = DrawPopup("Modifier 1", "SIDC position 11: symbol modifier (HQ / Task Force / Feint / Mobility ...).", sidcModifierOneIndex, ModifierOnes);
            sidcModifierTwoIndex = DrawPopup("Modifier 2", "SIDC position 12: echelon size (Team / Squad / Platoon ... Army).", sidcModifierTwoIndex, ModifierTwos);
            sidcCountryCode = EditorGUILayout.TextField(
                FieldLabel("Country Code", "SIDC positions 13-14: country code (-- = none)."),
                NormalizePart(sidcCountryCode, 2, "--")).ToUpperInvariant();
            sidcOrderOfBattleIndex = DrawPopup("Order Of Battle", "SIDC position 15: order of battle (Air / Electronic / Ground / Maritime ...).", sidcOrderOfBattleIndex, OrderOfBattleOptions);
        }

        private string BuildSidcFromBuilder()
        {
            return new string(new[]
            {
                CodingSchemes[ClampIndex(sidcCodingSchemeIndex, CodingSchemes.Length)].Value,
                Affiliations[ClampIndex(sidcAffiliationIndex, Affiliations.Length)].Value,
                BattleDimensions[ClampIndex(sidcBattleDimensionIndex, BattleDimensions.Length)].Value,
                Statuses[ClampIndex(sidcStatusIndex, Statuses.Length)].Value
            }) +
            NormalizePart(sidcFunctionId, 6, "------") +
            ModifierOnes[ClampIndex(sidcModifierOneIndex, ModifierOnes.Length)].Value +
            ModifierTwos[ClampIndex(sidcModifierTwoIndex, ModifierTwos.Length)].Value +
            NormalizePart(sidcCountryCode, 2, "--") +
            OrderOfBattleOptions[ClampIndex(sidcOrderOfBattleIndex, OrderOfBattleOptions.Length)].Value;
        }

        private void SyncBuilderFromSidc(string sidc)
        {
            var normalized = NormalizePart(sidc, 15, DefaultLetterSidc).ToUpperInvariant();
            sidcCodingSchemeIndex = FindIndex(CodingSchemes, normalized[0]);
            sidcAffiliationIndex = FindIndex(Affiliations, normalized[1]);
            sidcBattleDimensionIndex = FindIndex(BattleDimensions, normalized[2]);
            sidcStatusIndex = FindIndex(Statuses, normalized[3]);
            sidcFunctionId = NormalizePart(normalized.Substring(4, 6), 6, "------");
            SyncSpecificSymbolFromFunctionId(sidcFunctionId);

            sidcModifierOneIndex = FindIndex(ModifierOnes, normalized[10]);
            sidcModifierTwoIndex = FindIndex(ModifierTwos, normalized[11]);
            sidcCountryCode = NormalizePart(normalized.Substring(12, 2), 2, "--");
            sidcOrderOfBattleIndex = FindIndex(OrderOfBattleOptions, normalized[14]);
        }

        private void SyncSpecificSymbolFromFunctionId(string functionId)
        {
            var domains = new[] { AirSpecificSymbols, GroundSpecificSymbols, SeaSpecificSymbols, SubsurfaceSpecificSymbols };
            for (var domainIndex = 0; domainIndex < domains.Length; domainIndex++)
            {
                for (var symbolIndex = 0; symbolIndex < domains[domainIndex].Length; symbolIndex++)
                {
                    var symbol = domains[domainIndex][symbolIndex];
                    if (string.IsNullOrEmpty(symbol.FunctionId))
                    {
                        continue;
                    }

                    if (string.Equals(symbol.FunctionId, functionId, StringComparison.Ordinal))
                    {
                        sidcSymbolDomainIndex = domainIndex;
                        sidcSpecificSymbolIndex = symbolIndex;
                        sidcSpecificModifierIndex = 0;
                        return;
                    }

                    // Match a variant: base function id is a prefix and the suffix is a
                    // known variant code (e.g. functionId "MFQR--" -> base "MFQ", variant "R").
                    if (SpecificSymbolVariants.TryGetValue(symbol.FunctionId, out var variants))
                    {
                        var baseCode = symbol.FunctionId.TrimEnd('-');
                        if (functionId.StartsWith(baseCode, StringComparison.Ordinal))
                        {
                            var suffix = functionId.Substring(baseCode.Length).TrimEnd('-');
                            var variantIndex = FindIndex(variants, suffix);
                            if (variantIndex >= 0)
                            {
                                sidcSymbolDomainIndex = domainIndex;
                                sidcSpecificSymbolIndex = symbolIndex;
                                sidcSpecificModifierIndex = variantIndex;
                                return;
                            }
                        }
                    }
                }
            }

            sidcSpecificSymbolIndex = SpecificSymbolsForDomain(SymbolDomains[ClampIndex(sidcSymbolDomainIndex, SymbolDomains.Length)].Value).Length - 1;
        }

        // Specific symbols whose function id supports a variable sub-type suffix. Keyed by
        // the base function id. Shared with MilsymbolSidcDecoder so build/parse stay in sync.
        private static readonly SidcStringOption[] NoVariants = { new SidcStringOption("None (-)", "") };
        private static readonly SidcStringOption[] UavVariants = BuildVariants(MilsymbolSidcDecoder.UavRoles);
        private static readonly SidcStringOption[] AirVariants = BuildVariants(MilsymbolSidcDecoder.AirRoles);
        private static readonly Dictionary<string, SidcStringOption[]> SpecificSymbolVariants =
            new Dictionary<string, SidcStringOption[]>
            {
                { "MFQ---", UavVariants },
                { "MF----", AirVariants },
                { "MH----", AirVariants }
            };

        private static SidcStringOption[] BuildVariants(MilsymbolSidcDecoder.Variant[] roles)
        {
            var options = new SidcStringOption[roles.Length];
            for (var i = 0; i < roles.Length; i++)
            {
                // Show the appended code in the dropdown, e.g. "Attack (A)" / "None (-)",
                // matching the other SIDC dropdowns. The stored Value stays the raw code.
                var code = string.IsNullOrEmpty(roles[i].Code) ? "-" : roles[i].Code;
                options[i] = new SidcStringOption(roles[i].Label + " (" + code + ")", roles[i].Code);
            }

            return options;
        }

        private static SidcStringOption[] VariantsForSymbol(SidcSymbolOption symbol)
        {
            if (!string.IsNullOrEmpty(symbol.FunctionId) &&
                SpecificSymbolVariants.TryGetValue(symbol.FunctionId, out var variants))
            {
                return variants;
            }

            return NoVariants;
        }

        private static SidcSymbolOption[] SpecificSymbolsForDomain(char domain)
        {
            switch (domain)
            {
                case 'G':
                    return GroundSpecificSymbols;
                case 'S':
                    return SeaSpecificSymbols;
                case 'U':
                    return SubsurfaceSpecificSymbols;
                default:
                    return AirSpecificSymbols;
            }
        }

        private static int DrawPopup(string label, string tooltip, int index, SidcCharOption[] options)
        {
            return EditorGUILayout.Popup(FieldLabel(label, tooltip), ClampIndex(index, options.Length), Labels(options));
        }

        /// <summary>
        /// Builds a label GUIContent. When a tooltip is supplied, a " (?)" marker is appended
        /// to the visible label so users can tell which fields have hover help.
        /// </summary>
        private static GUIContent FieldLabel(string label, string tooltip)
        {
            return string.IsNullOrEmpty(tooltip)
                ? new GUIContent(label)
                : new GUIContent(label + " (?)", tooltip);
        }

        private static string NormalizePart(string value, int length, string fallback)
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().Replace(' ', '-');
            normalized = normalized.ToUpperInvariant();
            while (normalized.Length < length)
            {
                normalized += "-";
            }

            return normalized.Length > length ? normalized.Substring(0, length) : normalized;
        }

        private static int FindIndex(SidcCharOption[] options, char value)
        {
            for (var i = 0; i < options.Length; i++)
            {
                if (options[i].Value == value)
                {
                    return i;
                }
            }

            return 0;
        }

        private static int FindIndex(SidcStringOption[] options, string value)
        {
            for (var i = 0; i < options.Length; i++)
            {
                if (string.Equals(options[i].Value, value, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int ClampIndex(int index, int length)
        {
            return Mathf.Clamp(index, 0, Mathf.Max(0, length - 1));
        }

        private static string[] Labels(SidcCharOption[] options)
        {
            var labels = new string[options.Length];
            for (var i = 0; i < options.Length; i++)
            {
                labels[i] = options[i].Label;
            }

            return labels;
        }

        private static string[] Labels(SidcStringOption[] options)
        {
            var labels = new string[options.Length];
            for (var i = 0; i < options.Length; i++)
            {
                labels[i] = options[i].Label;
            }

            return labels;
        }

        private static string[] Labels(SidcSymbolOption[] options)
        {
            var labels = new string[options.Length];
            for (var i = 0; i < options.Length; i++)
            {
                labels[i] = options[i].Label;
            }

            return labels;
        }

        private void DrawResult()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);

            if (!string.IsNullOrEmpty(status))
            {
                EditorGUILayout.HelpBox(status, MessageType.Info);
            }

            if (lastResult == null)
            {
                return;
            }

            EditorGUILayout.LabelField("Valid", lastResult.valid ? "True" : "False");
            EditorGUILayout.LabelField("Size", lastResult.width.ToString("0.##") + " x " + lastResult.height.ToString("0.##"));
            EditorGUILayout.LabelField("Anchor", lastResult.anchorX.ToString("0.##") + ", " + lastResult.anchorY.ToString("0.##"));

            showGeneratedSource = EditorGUILayout.Foldout(showGeneratedSource, "Generated SVG Source", true);
            if (showGeneratedSource)
            {
                var svgTextStyle = new GUIStyle(EditorStyles.textArea)
                {
                    wordWrap = true
                };
                editableSvg = EditorGUILayout.TextArea(
                    editableSvg ?? "",
                    svgTextStyle,
                    GUILayout.Height(120),
                    GUILayout.ExpandWidth(true));

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Reset SVG From Generated Result"))
                    {
                        editableSvg = FormatSvgForEditing(lastResult.svg);
                    }
                }
            }
        }

        private void DrawPreview()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            if (!string.IsNullOrEmpty(previewStatus))
            {
                EditorGUILayout.HelpBox(previewStatus, MessageType.Info);
            }

            const float previewBoxSize = 260f;
            var outerRect = GUILayoutUtility.GetRect(previewBoxSize, previewBoxSize, GUILayout.ExpandWidth(true));
            var previewRect = new Rect(
                outerRect.x + Mathf.Max(0f, (outerRect.width - previewBoxSize) * 0.5f),
                outerRect.y,
                previewBoxSize,
                previewBoxSize);

            EditorGUI.DrawRect(previewRect, new Color(0.22f, 0.22f, 0.22f));
            GUI.Box(previewRect, GUIContent.none);

            if (previewTexture != null)
            {
                var imageRect = new Rect(
                    previewRect.x + 8f,
                    previewRect.y + 8f,
                    previewRect.width - 16f,
                    previewRect.height - 16f);
                GUI.DrawTexture(imageRect, previewTexture, ScaleMode.ScaleToFit, true);
                return;
            }

            var labelStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(previewRect, "No preview", labelStyle);
        }

        private void ClearPreview()
        {
            if (previewTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(previewTexture);
                previewTexture = null;
            }

            previewStatus = "";
        }

        private static Vector2Int CalculatePreviewSize(float sourceWidth, float sourceHeight)
        {
            var width = Mathf.Max(1f, sourceWidth);
            var height = Mathf.Max(1f, sourceHeight);
            var scale = Mathf.Min(256f / width, 256f / height);
            scale = Mathf.Max(1f, scale);

            return new Vector2Int(
                Mathf.Clamp(Mathf.CeilToInt(width * scale), 16, 1024),
                Mathf.Clamp(Mathf.CeilToInt(height * scale), 16, 1024));
        }

        private static string FormatSvgForEditing(string svg)
        {
            return string.IsNullOrEmpty(svg) ? "" : svg.Replace("><", ">\n<");
        }

        private static string DrawColorMode(string label, string value)
        {
            var modes = new[] { "Light", "Medium", "Dark" };
            var current = Array.IndexOf(modes, string.IsNullOrWhiteSpace(value) ? "Light" : value);
            if (current < 0)
            {
                current = 0;
            }

            return modes[EditorGUILayout.Popup(label, current, modes)];
        }

        private static string DrawOptionalColor(string label, string value, Color defaultColor)
        {
            var enabled = !string.IsNullOrWhiteSpace(value);
            using (new EditorGUILayout.HorizontalScope())
            {
                enabled = EditorGUILayout.Toggle(label, enabled, GUILayout.Width(EditorGUIUtility.labelWidth + 16f));
                using (new EditorGUI.DisabledScope(!enabled))
                {
                    var color = ParseColor(value, defaultColor);
                    color = EditorGUILayout.ColorField(GUIContent.none, color, false, false, false);
                    return enabled ? "#" + ColorUtility.ToHtmlStringRGB(color) : "";
                }
            }
        }

        private static Color ParseColor(string value, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            var trimmed = value.Trim();
            if (ColorUtility.TryParseHtmlString(trimmed, out var parsed))
            {
                return parsed;
            }

            if (trimmed.Equals("black", StringComparison.OrdinalIgnoreCase))
            {
                return Color.black;
            }

            if (trimmed.Equals("white", StringComparison.OrdinalIgnoreCase))
            {
                return Color.white;
            }

            if (trimmed.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(")", StringComparison.Ordinal))
            {
                var parts = trimmed.Substring(4, trimmed.Length - 5).Split(',');
                if (parts.Length == 3 &&
                    float.TryParse(parts[0], out var r) &&
                    float.TryParse(parts[1], out var g) &&
                    float.TryParse(parts[2], out var b))
                {
                    return new Color(
                        Mathf.Clamp01(r / 255f),
                        Mathf.Clamp01(g / 255f),
                        Mathf.Clamp01(b / 255f));
                }
            }

            return fallback;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Preview files are temporary and can be cleaned up by the OS.
            }
        }
    }
}
