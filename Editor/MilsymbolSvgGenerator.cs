using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Leuconoe.MilsymbolUnity;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Leuconoe.MilsymbolUnity.Editor
{
    public static class MilsymbolSvgGenerator
    {
        private const string DefaultNodeExecutable = "node";
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        [Serializable]
        public sealed class Result
        {
            public bool ok;
            public string svg;
            public bool valid;
            public float width;
            public float height;
            public float anchorX;
            public float anchorY;
            public string description;
            public string error;

            public Vector2 Anchor => new Vector2(anchorX, anchorY);
        }

        public static Result Generate(MilsymbolIconRequest request, string nodeExecutable = DefaultNodeExecutable)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.sidc))
            {
                throw new ArgumentException("SIDC is required.", nameof(request));
            }

            var scriptPath = FindGeneratorScriptPath();
            var tempRoot = Path.Combine(Path.GetTempPath(), "milsymbol-unity");
            Directory.CreateDirectory(tempRoot);

            var nonce = Guid.NewGuid().ToString("N");
            var requestPath = Path.Combine(tempRoot, nonce + "-request.json");
            var responsePath = Path.Combine(tempRoot, nonce + "-response.json");

            try
            {
                File.WriteAllText(requestPath, JsonUtility.ToJson(request), Utf8NoBom);
                RunNode(nodeExecutable, scriptPath, requestPath, responsePath);

                if (!File.Exists(responsePath))
                {
                    throw new InvalidOperationException("milsymbol generator did not create a response file.");
                }

                var result = JsonUtility.FromJson<Result>(File.ReadAllText(responsePath, Utf8NoBom));
                if (result == null)
                {
                    throw new InvalidOperationException("milsymbol generator returned an unreadable response.");
                }

                if (!result.ok)
                {
                    throw new InvalidOperationException(result.error);
                }

                return result;
            }
            finally
            {
                TryDelete(requestPath);
                TryDelete(responsePath);
            }
        }

        public static string SaveSvg(string assetFolder, string fileName, string svg)
        {
            if (string.IsNullOrWhiteSpace(assetFolder))
            {
                throw new ArgumentException("Asset folder is required.", nameof(assetFolder));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name is required.", nameof(fileName));
            }

            if (string.IsNullOrEmpty(svg))
            {
                throw new ArgumentException("SVG content is empty.", nameof(svg));
            }

            var normalizedFolder = NormalizeAssetFolder(assetFolder);
            Directory.CreateDirectory(ToAbsoluteProjectPath(normalizedFolder));

            var safeFileName = SanitizeFileName(fileName);
            if (!safeFileName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                safeFileName += ".svg";
            }

            // Overwrite an existing file with the same name instead of creating a numbered copy.
            var assetPath = normalizedFolder + "/" + safeFileName;
            File.WriteAllText(ToAbsoluteProjectPath(assetPath), svg, Utf8NoBom);
            AssetDatabase.ImportAsset(assetPath);
            return assetPath;
        }

        public static string SavePng(string assetFolder, MilsymbolIconRequest request, int width, int height, string nodeExecutable = "node", int maxTextureSize = 128)
        {
            if (string.IsNullOrWhiteSpace(assetFolder))
            {
                throw new ArgumentException("Asset folder is required.", nameof(assetFolder));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.sidc))
            {
                throw new ArgumentException("SIDC is required.", nameof(request));
            }

            var normalizedFolder = NormalizeAssetFolder(assetFolder);
            Directory.CreateDirectory(ToAbsoluteProjectPath(normalizedFolder));

            // Overwrite an existing PNG with the same name instead of creating a numbered copy.
            var assetPath = normalizedFolder + "/" + CreateSidcFileName(request.sidc, ".png");
            MilsymbolPngExporter.SavePng(request, ToAbsoluteProjectPath(assetPath), width, height, nodeExecutable);
            AssetDatabase.ImportAsset(assetPath);
            ConfigureSavedPng(assetPath, maxTextureSize);
            EnsureSpriteAtlasForFolder(normalizedFolder);
            return assetPath;
        }

        public static MilsymbolIconAsset SaveIconAsset(string sourceAssetPath, MilsymbolIconRequest request, Result result)
        {
            // Overwrite an existing icon asset with the same name instead of creating a numbered copy.
            var assetPath = Path.ChangeExtension(sourceAssetPath, ".asset").Replace("\\", "/");

            var asset = ScriptableObject.CreateInstance<MilsymbolIconAsset>();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sourceAssetPath);
            asset.SetGeneratedData(
                request,
                string.IsNullOrEmpty(result.description) ? MilsymbolSidcDecoder.Describe(request.sidc) : result.description,
                result.valid,
                result.width,
                result.height,
                result.Anchor,
                texture);
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            return asset;
        }

        public static string NormalizeAssetFolder(string folder)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace("\\", "/");
            var assetsRoot = Path.GetFullPath(Application.dataPath).Replace("\\", "/").TrimEnd('/');
            var trimmedFolder = folder.Replace("\\", "/").TrimEnd('/');

            string fullPath;
            if (trimmedFolder.StartsWith("Assets/", StringComparison.Ordinal) || trimmedFolder == "Assets")
            {
                fullPath = Path.GetFullPath(Path.Combine(projectRoot, trimmedFolder)).Replace("\\", "/").TrimEnd('/');
            }
            else
            {
                fullPath = Path.GetFullPath(trimmedFolder).Replace("\\", "/").TrimEnd('/');
            }

            if (string.Equals(fullPath, assetsRoot, StringComparison.OrdinalIgnoreCase))
            {
                return "Assets";
            }

            if (fullPath.StartsWith(assetsRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return "Assets" + fullPath.Substring(assetsRoot.Length);
            }

            throw new ArgumentException("Output folder must be inside this project's Assets folder.");
        }

        public static string CreateSidcFileName(string sidc, string extension)
        {
            var safeName = SanitizeFileName(string.IsNullOrWhiteSpace(sidc) ? "milsymbol-icon" : sidc);
            var safeExtension = string.IsNullOrWhiteSpace(extension) ? "" : extension.Trim();
            if (!string.IsNullOrEmpty(safeExtension) && !safeExtension.StartsWith(".", StringComparison.Ordinal))
            {
                safeExtension = "." + safeExtension;
            }

            if (!string.IsNullOrEmpty(safeExtension) && !safeName.EndsWith(safeExtension, StringComparison.OrdinalIgnoreCase))
            {
                safeName += safeExtension;
            }

            return safeName;
        }

        private static void RunNode(string nodeExecutable, string scriptPath, string requestPath, string responsePath)
        {
            var executable = MilsymbolToolLocator.ResolveNode(nodeExecutable);
            var arguments = Quote(scriptPath) + " " + Quote(requestPath) + " " + Quote(responsePath);

            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(scriptPath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                MilsymbolToolLocator.PrepareEnvironment(process.StartInfo, executable);

                process.Start();
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "milsymbol generator failed with exit code " +
                        process.ExitCode.ToString(CultureInfo.InvariantCulture) +
                        Environment.NewLine +
                        stdout +
                        stderr);
                }
            }
        }

        internal static string FindGeneratorScriptPath()
        {
            // Editor/Node~ uses a trailing '~' so Unity ignores the folder (and its
            // node_modules); the script is located by physical file path, not as a Unity
            // asset. PackageRoots() resolves the real on-disk path (required for UPM /
            // PackageCache installs, where "Packages/<name>" is virtual).
            foreach (var root in MilsymbolToolLocator.PackageRoots())
            {
                var candidate = Path.Combine(root, "Editor", "Node~", "generate-symbol.mjs");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new FileNotFoundException("Could not find Editor/Node~/generate-symbol.mjs in the milsymbol-unity package.");
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, assetPath);
        }

        private static string SanitizeFileName(string fileName)
        {
            var sanitized = fileName.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(sanitized) ? "milsymbol-icon.svg" : sanitized;
        }

        public static void ConfigureSavedPng(string assetPath, int maxTextureSize = 128)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = ClampTextureSize(maxTextureSize);
            importer.SaveAndReimport();
        }

        /// <summary>Clamps to the power-of-two range Unity's importer accepts (32-16384).</summary>
        public static int ClampTextureSize(int size)
        {
            var clamped = Mathf.Clamp(size, 32, 16384);
            var power = Mathf.RoundToInt(Mathf.Pow(2f, Mathf.Round(Mathf.Log(clamped, 2f))));
            return Mathf.Clamp(power, 32, 16384);
        }

        private static void EnsureSpriteAtlasForFolder(string assetFolder)
        {
            var atlasPath = assetFolder.TrimEnd('/') + "/Milsymbol Icons.spriteatlas";
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            if (atlas == null)
            {
                atlas = new SpriteAtlas();
                AssetDatabase.CreateAsset(atlas, atlasPath);

                var packingSettings = new SpriteAtlasPackingSettings
                {
                    enableRotation = false,
                    enableTightPacking = false,
                    padding = 2
                };
                atlas.SetPackingSettings(packingSettings);

                var textureSettings = new SpriteAtlasTextureSettings
                {
                    readable = false,
                    generateMipMaps = false,
                    sRGB = true,
                    filterMode = FilterMode.Bilinear
                };
                atlas.SetTextureSettings(textureSettings);
            }

            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(assetFolder);
            if (folder == null)
            {
                return;
            }

            foreach (var packable in atlas.GetPackables())
            {
                if (packable == folder)
                {
                    return;
                }
            }

            atlas.Add(new UnityEngine.Object[] { folder });
            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
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
                // Temporary files are best-effort cleanup.
            }
        }
    }
}
