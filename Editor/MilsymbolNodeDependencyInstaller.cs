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
            return !string.IsNullOrEmpty(FindNodeDir(false));
        }

        public static bool AreDependenciesInstalled()
        {
            var nodeDir = FindNodeDir(false);
            return !string.IsNullOrEmpty(nodeDir) &&
                   File.Exists(Path.Combine(nodeDir, "node_modules", "milsymbol", "package.json")) &&
                   File.Exists(Path.Combine(nodeDir, "node_modules", "@resvg", "resvg-js", "package.json"));
        }

        public static bool EnsureInstalledOrPrompt()
        {
            if (AreDependenciesInstalled())
            {
                return true;
            }

            var install = EditorUtility.DisplayDialog(
                "Milsymbol Node Dependencies",
                "Icon generation needs the milsymbol library. Download it now (npm install)?",
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
                    "milsymbol is installed.",
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
            var nodeDir = FindNodeDir(true);
            var packageJson = Path.Combine(nodeDir, "package.json");
            if (!File.Exists(packageJson))
            {
                throw new FileNotFoundException("Could not find Editor/Node/package.json.", packageJson);
            }

            EditorUtility.DisplayProgressBar(
                "Milsymbol Node Dependencies",
                "Downloading milsymbol (npm install) in " + nodeDir,
                0.5f);

            return RunNpmInstall(nodeDir);
        }

        private static string RunNpmInstall(string workingDirectory)
        {
            var output = new StringBuilder();
            var errors = new StringBuilder();

            using (var process = new Process())
            {
                process.StartInfo = BuildNpmInstallStartInfo(workingDirectory);

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
                        "Failed to start npm. Install Node.js/npm, or set the full node path in " +
                        "Tools/Milsymbol/Icon Generator > Node Executable.",
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

        private static string FindNodeDir(bool throwIfMissing)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var candidates = new[]
            {
                Path.Combine(projectRoot, "Packages", "milsymbol-unity", "Editor", "Node~"),
                Path.Combine(projectRoot, "Packages", "com.leuconoe.milsymbol-unity", "Editor", "Node~")
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(Path.Combine(candidate, "package.json")))
                {
                    return candidate;
                }
            }

            if (throwIfMissing)
            {
                throw new DirectoryNotFoundException(
                    "Could not find Editor/Node~/package.json under Packages/milsymbol-unity.");
            }

            return "";
        }

        /// <summary>
        /// Builds the process to run "npm install --omit=dev". Prefers invoking npm's CLI
        /// entry point through the resolved node binary, which avoids the Windows npm.cmd
        /// shim and the login-shell PATH dependency entirely. Falls back to launching the
        /// resolved npm executable directly.
        /// </summary>
        private static ProcessStartInfo BuildNpmInstallStartInfo(string workingDirectory)
        {
            var node = MilsymbolToolLocator.ResolveNode();
            const string npmArguments = "install";

            var startInfo = new ProcessStartInfo
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var npmCli = FindNpmCli(node);
            if (!string.IsNullOrEmpty(npmCli))
            {
                startInfo.FileName = node;
                startInfo.Arguments = Quote(npmCli) + " " + npmArguments;
            }
            else
            {
                startInfo.FileName = MilsymbolToolLocator.ResolveNpm();
                startInfo.Arguments = npmArguments;
            }

            MilsymbolToolLocator.PrepareEnvironment(startInfo, node, startInfo.FileName);
            return startInfo;
        }

        /// <summary>Locates npm-cli.js bundled next to the node binary, if present.</summary>
        private static string FindNpmCli(string nodePath)
        {
            if (string.IsNullOrEmpty(nodePath) || nodePath.IndexOf(Path.DirectorySeparatorChar) < 0)
            {
                return "";
            }

            var nodeDir = Path.GetDirectoryName(nodePath);
            if (string.IsNullOrEmpty(nodeDir))
            {
                return "";
            }

            var candidates = new[]
            {
                // Windows global install layout: node.exe and node_modules share a dir.
                Path.Combine(nodeDir, "node_modules", "npm", "bin", "npm-cli.js"),
                // Unix layout: bin/node with lib/node_modules alongside.
                Path.Combine(Path.GetDirectoryName(nodeDir) ?? nodeDir, "lib", "node_modules", "npm", "bin", "npm-cli.js")
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return "";
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
