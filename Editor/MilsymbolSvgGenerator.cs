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

            var assetPath = AssetDatabase.GenerateUniqueAssetPath(normalizedFolder + "/" + safeFileName);
            File.WriteAllText(ToAbsoluteProjectPath(assetPath), svg, Utf8NoBom);
            AssetDatabase.ImportAsset(assetPath);
            return assetPath;
        }

        public static string SavePng(string assetFolder, MilsymbolIconRequest request, int width, int height, string nodeExecutable = "node")
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

            var assetPath = AssetDatabase.GenerateUniqueAssetPath(normalizedFolder + "/" + CreateSidcFileName(request.sidc, ".png"));
            MilsymbolPngExporter.SavePng(request, ToAbsoluteProjectPath(assetPath), width, height, nodeExecutable);
            AssetDatabase.ImportAsset(assetPath);
            ConfigureSavedPng(assetPath);
            EnsureSpriteAtlasForFolder(normalizedFolder);
            return assetPath;
        }

        public static MilsymbolIconAsset SaveIconAsset(string sourceAssetPath, MilsymbolIconRequest request, Result result, string svgOverride = null)
        {
            var assetPath = Path.ChangeExtension(sourceAssetPath, ".asset").Replace("\\", "/");
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            var asset = ScriptableObject.CreateInstance<MilsymbolIconAsset>();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sourceAssetPath);
            asset.SetGeneratedData(
                request,
                string.IsNullOrEmpty(svgOverride) ? result.svg : svgOverride,
                result.valid,
                result.width,
                result.height,
                result.Anchor,
                texture,
                texture == null ? "" : sourceAssetPath);
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

        private static string FindGeneratorScriptPath()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var directPath = Path.Combine(projectRoot, "Packages", "milsymbol-unity", "Editor", "Node", "generate-symbol.mjs");
            if (File.Exists(directPath))
            {
                return directPath;
            }

            var packagePath = Path.Combine(projectRoot, "Packages", "com.leuconoe.milsymbol-unity", "Editor", "Node", "generate-symbol.mjs");
            if (File.Exists(packagePath))
            {
                return packagePath;
            }

            var packagesPath = Path.Combine(projectRoot, "Packages");
            var matches = Directory.Exists(packagesPath)
                ? Directory.GetFiles(packagesPath, "generate-symbol.mjs", SearchOption.AllDirectories)
                : Array.Empty<string>();

            foreach (var match in matches)
            {
                if (match.Replace("\\", "/").EndsWith("/Editor/Node/generate-symbol.mjs", StringComparison.Ordinal))
                {
                    return match;
                }
            }

            throw new FileNotFoundException("Could not find Editor/Node/generate-symbol.mjs in Packages.");
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

        public static void ConfigureSavedPng(string assetPath)
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
            importer.SaveAndReimport();
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
