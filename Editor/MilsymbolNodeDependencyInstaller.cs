using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Leuconoe.MilsymbolUnity.Editor
{
    public static class MilsymbolNodeDependencyInstaller
    {
        private const int InstallTimeoutMilliseconds = 300000;

        [MenuItem("Tools/Milsymbol/Install Node Dependencies")]
        public static void InstallFromMenu()
        {
            InstallWithDialog();
        }

        [MenuItem("Tools/Milsymbol/Install Node Dependencies", true)]
        private static bool CanInstallFromMenu()
        {
            return Directory.Exists(FindMilsymbolRoot(false));
        }

        public static bool AreDependenciesInstalled()
        {
            var milsymbolRoot = FindMilsymbolRoot(false);
            return !string.IsNullOrEmpty(milsymbolRoot) &&
                   File.Exists(Path.Combine(milsymbolRoot, "node_modules", "@resvg", "resvg-js", "package.json"));
        }

        public static bool EnsureInstalledOrPrompt()
        {
            if (AreDependenciesInstalled())
            {
                return true;
            }

            var install = EditorUtility.DisplayDialog(
                "Milsymbol Node Dependencies",
                "PNG export needs Node dependencies inside the bundled milsymbol submodule. Run npm install now?",
                "Install",
                "Cancel");

            return install && InstallWithDialog();
        }

        public static bool InstallWithDialog()
        {
            try
            {
                var log = Install();
                if (!string.IsNullOrWhiteSpace(log))
                {
                    UnityEngine.Debug.Log(log);
                }

                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog(
                    "Milsymbol Node Dependencies",
                    "Node dependencies are installed.",
                    "OK");
                return true;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Milsymbol Node Dependencies",
                    exception.Message,
                    "OK");
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static string Install()
        {
            var milsymbolRoot = FindMilsymbolRoot(true);
            var packageJson = Path.Combine(milsymbolRoot, "package.json");
            if (!File.Exists(packageJson))
            {
                throw new FileNotFoundException("Could not find milsymbol/package.json.", packageJson);
            }

            EditorUtility.DisplayProgressBar(
                "Milsymbol Node Dependencies",
                "Running npm install --omit=dev in " + milsymbolRoot,
                0.5f);

            return RunNpmInstall(milsymbolRoot);
        }

        private static string RunNpmInstall(string workingDirectory)
        {
            var output = new StringBuilder();
            var errors = new StringBuilder();

            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = GetNpmExecutable(),
                    Arguments = "install --omit=dev",
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                process.OutputDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        output.AppendLine(args.Data);
                    }
                };
                process.ErrorDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        errors.AppendLine(args.Data);
                    }
                };

                try
                {
                    process.Start();
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        "Failed to start npm. Install Node.js/npm or make sure npm is available from PATH.",
                        exception);
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(InstallTimeoutMilliseconds))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // Process termination is best effort after a timeout.
                    }

                    throw new TimeoutException("npm install --omit=dev did not finish within 5 minutes.");
                }

                process.WaitForExit();

                var log = output.ToString() + errors.ToString();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "npm install --omit=dev failed with exit code " + process.ExitCode + Environment.NewLine + log);
                }

                return log;
            }
        }

        private static string FindMilsymbolRoot(bool throwIfMissing)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var candidates = new[]
            {
                Path.Combine(projectRoot, "Packages", "milsymbol-unity", "milsymbol"),
                Path.Combine(projectRoot, "Packages", "com.leuconoe.milsymbol-unity", "milsymbol")
            };

            foreach (var candidate in candidates)
            {
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            if (throwIfMissing)
            {
                throw new DirectoryNotFoundException(
                    "Could not find the bundled milsymbol submodule under Packages/milsymbol-unity/milsymbol.");
            }

            return "";
        }

        private static string GetNpmExecutable()
        {
            return Application.platform == RuntimePlatform.WindowsEditor ? "npm.cmd" : "npm";
        }
    }
}
