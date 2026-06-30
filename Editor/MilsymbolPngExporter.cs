using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Leuconoe.MilsymbolUnity;
using UnityEngine;

namespace Leuconoe.MilsymbolUnity.Editor
{
    public static class MilsymbolPngExporter
    {
        private const string DefaultNodeExecutable = "node";
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        [Serializable]
        private sealed class Request
        {
            public string sidc = "";
            public MilsymbolStandard standard = MilsymbolStandard.Auto;
            public bool iconOnly = true;
            public MilsymbolIconStyle style = new MilsymbolIconStyle();
            public string pngOutputPath = "";
            public int pngWidth;
            public int pngHeight;
        }

        [Serializable]
        private sealed class Response
        {
            public bool ok;
            public string error;
        }

        public static void SavePng(MilsymbolIconRequest iconRequest, string absoluteFilePath, int width, int height, string nodeExecutable = DefaultNodeExecutable)
        {
            if (iconRequest == null)
            {
                throw new ArgumentNullException(nameof(iconRequest));
            }

            if (string.IsNullOrWhiteSpace(iconRequest.sidc))
            {
                throw new ArgumentException("SIDC is required.", nameof(iconRequest));
            }

            if (string.IsNullOrWhiteSpace(absoluteFilePath))
            {
                throw new ArgumentException("PNG file path is required.", nameof(absoluteFilePath));
            }

            if (!absoluteFilePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                absoluteFilePath += ".png";
            }

            width = Mathf.Clamp(width, 1, 8192);
            height = Mathf.Clamp(height, 1, 8192);

            var directory = Path.GetDirectoryName(absoluteFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var scriptPath = FindConverterScriptPath();
            var tempRoot = Path.Combine(Path.GetTempPath(), "milsymbol-unity");
            Directory.CreateDirectory(tempRoot);

            var nonce = Guid.NewGuid().ToString("N");
            var requestPath = Path.Combine(tempRoot, nonce + "-png-request.json");
            var responsePath = Path.Combine(tempRoot, nonce + "-png-response.json");

            try
            {
                var request = new Request
                {
                    sidc = iconRequest.sidc,
                    standard = iconRequest.standard,
                    iconOnly = true,
                    style = iconRequest.style,
                    pngOutputPath = absoluteFilePath,
                    pngWidth = width,
                    pngHeight = height
                };

                File.WriteAllText(requestPath, JsonUtility.ToJson(request), Utf8NoBom);
                RunNode(string.IsNullOrWhiteSpace(nodeExecutable) ? DefaultNodeExecutable : nodeExecutable, scriptPath, requestPath, responsePath);

                if (!File.Exists(responsePath))
                {
                    throw new InvalidOperationException("PNG converter did not create a response file.");
                }

                var response = JsonUtility.FromJson<Response>(File.ReadAllText(responsePath, Utf8NoBom));
                if (response == null)
                {
                    throw new InvalidOperationException("PNG converter returned an unreadable response.");
                }

                if (!response.ok)
                {
                    throw new InvalidOperationException(response.error);
                }
            }
            finally
            {
                TryDelete(requestPath);
                TryDelete(responsePath);
            }
        }

        private static string FindConverterScriptPath()
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

            throw new FileNotFoundException("Could not find Editor/Node/generate-symbol.mjs in Packages.");
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
                        "PNG converter failed with exit code " + process.ExitCode + Environment.NewLine + stdout + stderr);
                }
            }
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
