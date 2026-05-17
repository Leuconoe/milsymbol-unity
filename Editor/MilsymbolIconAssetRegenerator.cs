using System;
using System.IO;
using Leuconoe.MilsymbolUnity;
using UnityEditor;
using UnityEngine;

namespace Leuconoe.MilsymbolUnity.Editor
{
    public static class MilsymbolIconAssetRegenerator
    {
        private const string NodeExecutablePrefsKey = "Leuconoe.MilsymbolUnity.NodeExecutable";

        [MenuItem("Assets/Milsymbol/Regenerate Icon", false, 2100)]
        public static void RegenerateSelectedAssets()
        {
            var regenerated = 0;
            foreach (var selected in Selection.objects)
            {
                if (selected is MilsymbolIconAsset iconAsset)
                {
                    Regenerate(iconAsset);
                    regenerated++;
                }
            }

            if (regenerated > 0)
            {
                AssetDatabase.Refresh();
                Debug.Log("Regenerated " + regenerated + " milsymbol icon asset(s).");
            }
        }

        [MenuItem("Assets/Milsymbol/Regenerate Icon", true)]
        private static bool CanRegenerateSelectedAssets()
        {
            foreach (var selected in Selection.objects)
            {
                if (selected is MilsymbolIconAsset)
                {
                    return true;
                }
            }

            return false;
        }

        [MenuItem("CONTEXT/MilsymbolIconAsset/Regenerate Icon")]
        private static void RegenerateFromInspector(MenuCommand command)
        {
            if (command.context is MilsymbolIconAsset iconAsset)
            {
                Regenerate(iconAsset);
                AssetDatabase.Refresh();
            }
        }

        private static void Regenerate(MilsymbolIconAsset iconAsset)
        {
            if (iconAsset == null)
            {
                return;
            }

            if (!MilsymbolNodeDependencyInstaller.EnsureInstalledOrPrompt())
            {
                return;
            }

            var iconAssetPath = AssetDatabase.GetAssetPath(iconAsset);
            if (string.IsNullOrWhiteSpace(iconAssetPath))
            {
                throw new InvalidOperationException("MilsymbolIconAsset must be saved as a project asset before regeneration.");
            }

            var pngAssetPath = ResolvePngAssetPath(iconAsset, iconAssetPath);
            var request = new MilsymbolIconRequest
            {
                sidc = iconAsset.Sidc,
                standard = iconAsset.Standard,
                iconOnly = true,
                style = iconAsset.Style ?? new MilsymbolIconStyle()
            };

            var nodeExecutable = EditorPrefs.GetString(NodeExecutablePrefsKey, "node");
            var result = MilsymbolSvgGenerator.Generate(request, nodeExecutable);

            var width = Mathf.Clamp(Mathf.CeilToInt(result.width), 16, 4096);
            var height = Mathf.Clamp(Mathf.CeilToInt(result.height), 16, 4096);
            MilsymbolPngExporter.SavePng(request, ToAbsoluteProjectPath(pngAssetPath), width, height, nodeExecutable);

            AssetDatabase.ImportAsset(pngAssetPath, ImportAssetOptions.ForceUpdate);
            MilsymbolSvgGenerator.ConfigureSavedPng(pngAssetPath);

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngAssetPath);
            iconAsset.SetGeneratedData(
                request,
                result.svg,
                result.valid,
                result.width,
                result.height,
                result.Anchor,
                texture,
                pngAssetPath);

            EditorUtility.SetDirty(iconAsset);
            AssetDatabase.SaveAssets();
        }

        private static string ResolvePngAssetPath(MilsymbolIconAsset iconAsset, string iconAssetPath)
        {
            if (!string.IsNullOrWhiteSpace(iconAsset.TextureAssetPath))
            {
                return NormalizeAssetPath(iconAsset.TextureAssetPath);
            }

            if (iconAsset.Texture != null)
            {
                var texturePath = AssetDatabase.GetAssetPath(iconAsset.Texture);
                if (!string.IsNullOrWhiteSpace(texturePath))
                {
                    return NormalizeAssetPath(texturePath);
                }
            }

            var folder = Path.GetDirectoryName(iconAssetPath)?.Replace("\\", "/") ?? "Assets";
            var sidcFilePath = folder.TrimEnd('/') + "/" + MilsymbolSvgGenerator.CreateSidcFileName(iconAsset.Sidc, ".png");
            if (File.Exists(ToAbsoluteProjectPath(sidcFilePath)))
            {
                return sidcFilePath;
            }

            var sameNamePath = Path.ChangeExtension(iconAssetPath, ".png").Replace("\\", "/");
            if (File.Exists(ToAbsoluteProjectPath(sameNamePath)))
            {
                return sameNamePath;
            }

            return sidcFilePath;
        }

        private static string NormalizeAssetPath(string path)
        {
            var normalized = path.Replace("\\", "/");
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) && normalized != "Assets")
            {
                throw new ArgumentException("PNG output path must be inside this project's Assets folder.");
            }

            return normalized;
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, assetPath);
        }
    }
}
