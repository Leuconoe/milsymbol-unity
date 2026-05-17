using System;
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
        private const double AutoPreviewDelaySeconds = 0.45d;

        [SerializeField] private MilsymbolIconRequest request = new MilsymbolIconRequest
        {
            sidc = DefaultLetterSidc
        };

        [SerializeField] private string outputFolder = "Assets/Milsymbol Icons";
        [SerializeField] private bool createRuntimeAsset = true;
        [SerializeField] private int pngWidth = 512;
        [SerializeField] private int pngHeight = 512;
        [SerializeField] private int pngAntiAliasing = 1;
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
        [SerializeField] private string sidcFunctionId = "MFQ---";
        [SerializeField] private int sidcModifierOneIndex;
        [SerializeField] private int sidcModifierTwoIndex = 2;
        [SerializeField] private string sidcCountryCode = "--";
        [SerializeField] private int sidcOrderOfBattleIndex;

        private string nodeExecutable;
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
            window.minSize = new Vector2(460, 560);
            window.Show();
        }

        private void OnEnable()
        {
            nodeExecutable = EditorPrefs.GetString(NodeExecutablePrefsKey, "node");
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

            EditorGUILayout.LabelField("Generator", EditorStyles.boldLabel);
            nodeExecutable = EditorGUILayout.TextField("Node Executable", nodeExecutable);
            if (GUILayout.Button("Save Node Setting"))
            {
                EditorPrefs.SetString(NodeExecutablePrefsKey, string.IsNullOrWhiteSpace(nodeExecutable) ? "node" : nodeExecutable);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    "Node Dependencies",
                    MilsymbolNodeDependencyInstaller.AreDependenciesInstalled() ? "Installed" : "Missing");

                if (GUILayout.Button("Install", GUILayout.Width(96)))
                {
                    if (MilsymbolNodeDependencyInstaller.InstallWithDialog())
                    {
                        status = "Node dependencies are installed.";
                    }
                }
            }

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(360)))
                {
                    EditorGUI.BeginChangeCheck();

                    EditorGUILayout.LabelField("Symbol", EditorStyles.boldLabel);
                    if (useSidcBuilder)
                    {
                        request.sidc = BuildSidcFromBuilder();
                        using (new EditorGUI.DisabledScope(true))
                        {
                            EditorGUILayout.TextField("SIDC", request.sidc);
                        }
                    }
                    else
                    {
                        request.sidc = EditorGUILayout.TextField("SIDC", request.sidc);
                    }

                    useSidcBuilder = EditorGUILayout.Toggle("Use SIDC Builder", useSidcBuilder);
                    if (useSidcBuilder)
                    {
                        DrawSidcBuilder();
                        request.sidc = BuildSidcFromBuilder();
                    }

                    request.standard = (MilsymbolStandard)EditorGUILayout.EnumPopup("Standard", request.standard);
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
                        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
                        if (GUILayout.Button("...", GUILayout.Width(32)))
                        {
                            PickOutputFolder();
                        }
                    }

                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.TextField("File Name", GetAutomaticPngFileName());
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

                var pngPath = MilsymbolSvgGenerator.SavePng(outputFolder, request, pngWidth, pngHeight, nodeExecutable);
                if (createRuntimeAsset)
                {
                    MilsymbolSvgGenerator.SaveIconAsset(pngPath, request, lastResult, editableSvg);
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
                outputFolder = MilsymbolSvgGenerator.NormalizeAssetFolder(selected);
            }
            catch (Exception exception)
            {
                status = exception.Message;
                Debug.LogException(exception);
            }
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
        }

        private void DrawSidcBuilder()
        {
            sidcCodingSchemeIndex = DrawPopup("Coding Scheme", sidcCodingSchemeIndex, CodingSchemes);
            sidcAffiliationIndex = DrawPopup("Affiliation", sidcAffiliationIndex, Affiliations);
            sidcBattleDimensionIndex = DrawPopup("Battle Dimension", sidcBattleDimensionIndex, BattleDimensions);
            sidcStatusIndex = DrawPopup("Status", sidcStatusIndex, Statuses);

            sidcSymbolPartOneIndex = DrawPopup("Symbol Code Part 1", sidcSymbolPartOneIndex, SymbolPartOnes);
            sidcSymbolDomainIndex = DrawPopup("Symbol Code Domain", sidcSymbolDomainIndex, SymbolDomains);

            var specificSymbols = SpecificSymbolsForDomain(SymbolDomains[ClampIndex(sidcSymbolDomainIndex, SymbolDomains.Length)].Value);
            sidcSpecificSymbolIndex = EditorGUILayout.Popup("Specific Symbol", ClampIndex(sidcSpecificSymbolIndex, specificSymbols.Length), Labels(specificSymbols));
            var selectedSpecificSymbol = specificSymbols[ClampIndex(sidcSpecificSymbolIndex, specificSymbols.Length)];
            if (!string.IsNullOrEmpty(selectedSpecificSymbol.FunctionId))
            {
                sidcFunctionId = selectedSpecificSymbol.FunctionId;
            }
            else
            {
                sidcFunctionId = EditorGUILayout.TextField("Custom Function ID", sidcFunctionId).ToUpperInvariant();
            }

            sidcModifierOneIndex = DrawPopup("Modifier 1", sidcModifierOneIndex, ModifierOnes);
            sidcModifierTwoIndex = DrawPopup("Modifier 2", sidcModifierTwoIndex, ModifierTwos);
            sidcCountryCode = EditorGUILayout.TextField("Country Code", NormalizePart(sidcCountryCode, 2, "--")).ToUpperInvariant();
            sidcOrderOfBattleIndex = DrawPopup("Order Of Battle", sidcOrderOfBattleIndex, OrderOfBattleOptions);
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
                    if (string.Equals(domains[domainIndex][symbolIndex].FunctionId, functionId, StringComparison.Ordinal))
                    {
                        sidcSymbolDomainIndex = domainIndex;
                        sidcSpecificSymbolIndex = symbolIndex;
                        return;
                    }
                }
            }

            sidcSpecificSymbolIndex = SpecificSymbolsForDomain(SymbolDomains[ClampIndex(sidcSymbolDomainIndex, SymbolDomains.Length)].Value).Length - 1;
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

        private static int DrawPopup(string label, int index, SidcCharOption[] options)
        {
            return EditorGUILayout.Popup(label, ClampIndex(index, options.Length), Labels(options));
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
