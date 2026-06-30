using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Leuconoe.MilsymbolUnity.Editor
{
    /// <summary>
    /// Ensures the bundled <c>milsymbol</c> git submodule is checked out.
    ///
    /// Unity Package Manager clones a package from a git URL but does NOT initialise
    /// nested submodules, so a UPM install leaves <c>milsymbol/</c> empty. A plain
    /// <c>git clone</c> of this package also needs an explicit submodule update. This
    /// helper runs that update from the editor and reports actionable failures instead
    /// of letting the Node generator fail later with a missing-module error.
    /// </summary>
    public static class MilsymbolSubmoduleInstaller
    {
        private const int CheckoutTimeoutMilliseconds = 300000;

        [MenuItem("Tools/Milsymbol/Update milsymbol Submodule")]
        public static void UpdateFromMenu()
        {
            EnsureCheckedOut(true);
        }

        /// <summary>
        /// The submodule is considered present when its package entry point exists,
        /// since that is what <c>Editor/Node/generate-symbol.mjs</c> imports.
        /// </summary>
        public static bool IsCheckedOut()
        {
            var root = FindMilsymbolRoot(false);
            return !string.IsNullOrEmpty(root) && File.Exists(Path.Combine(root, "index.mjs"));
        }

        /// <summary>
        /// Ensures the submodule is checked out, optionally prompting the user and showing
        /// dialogs. Returns true when the submodule is present afterwards.
        /// </summary>
        public static bool EnsureCheckedOut(bool interactive)
        {
            if (IsCheckedOut())
            {
                return true;
            }

            var packageRoot = FindPackageRoot();
            if (string.IsNullOrEmpty(packageRoot) || !File.Exists(Path.Combine(packageRoot, ".gitmodules")))
            {
                // No working git checkout here (typical for a UPM git-URL install, which
                // does not fetch submodules). git cannot help; the user must vendor it.
                if (interactive)
                {
                    EditorUtility.DisplayDialog(
                        "Milsymbol Submodule",
                        "The bundled 'milsymbol' submodule is missing and this package is not a git " +
                        "working copy (Unity Package Manager git installs do not fetch submodules).\n\n" +
                        "Install this package by cloning it into your project's Packages folder, then run\n" +
                        "    git submodule update --init --recursive\n" +
                        "or use Tools/Milsymbol/Update milsymbol Submodule.",
                        "OK");
                }

                return false;
            }

            if (interactive)
            {
                var proceed = EditorUtility.DisplayDialog(
                    "Milsymbol Submodule",
                    "The bundled 'milsymbol' submodule is not checked out. Run " +
                    "'git submodule update --init --recursive' now?",
                    "Update",
                    "Cancel");
                if (!proceed)
                {
                    return false;
                }
            }

            try
            {
                EditorUtility.DisplayProgressBar(
                    "Milsymbol Submodule",
                    "Running git submodule update --init --recursive in " + packageRoot,
                    0.5f);

                var log = RunGitSubmoduleUpdate(packageRoot);
                if (!string.IsNullOrWhiteSpace(log))
                {
                    UnityEngine.Debug.Log(log);
                }

                AssetDatabase.Refresh();

                if (!IsCheckedOut())
                {
                    throw new InvalidOperationException(
                        "git submodule update completed but milsymbol/index.mjs is still missing.");
                }

                if (interactive)
                {
                    EditorUtility.DisplayDialog("Milsymbol Submodule", "milsymbol submodule is ready.", "OK");
                }

                return true;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                if (interactive)
                {
                    EditorUtility.DisplayDialog("Milsymbol Submodule", exception.Message, "OK");
                }

                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static string RunGitSubmoduleUpdate(string workingDirectory)
        {
            var git = MilsymbolToolLocator.ResolveGit();
            var output = new StringBuilder();
            var errors = new StringBuilder();

            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = git,
                    Arguments = "submodule update --init --recursive",
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                MilsymbolToolLocator.PrepareEnvironment(process.StartInfo, git);

                process.OutputDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data)) output.AppendLine(args.Data);
                };
                process.ErrorDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data)) errors.AppendLine(args.Data);
                };

                try
                {
                    process.Start();
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        "Failed to start git. Install git or set it on PATH, then retry.",
                        exception);
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(CheckoutTimeoutMilliseconds))
                {
                    try { process.Kill(); } catch { /* best effort */ }
                    throw new TimeoutException("git submodule update did not finish within 5 minutes.");
                }

                process.WaitForExit();

                var log = output.ToString() + errors.ToString();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "git submodule update --init --recursive failed with exit code " +
                        process.ExitCode + Environment.NewLine + log +
                        Environment.NewLine +
                        "If the error mentions an unreachable object or denied access, the bundled " +
                        "milsymbol fork commit may be private or removed.");
                }

                return log;
            }
        }

        /// <summary>The package folder that owns the submodule (contains .gitmodules).</summary>
        private static string FindPackageRoot()
        {
            var root = FindMilsymbolRoot(false);
            return string.IsNullOrEmpty(root) ? "" : Path.GetDirectoryName(root);
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
                // The folder always exists as a submodule mount point; the parent that
                // also contains .gitmodules is what we treat as the package root.
                var parent = Path.GetDirectoryName(candidate);
                if (parent != null && File.Exists(Path.Combine(parent, ".gitmodules")))
                {
                    return candidate;
                }

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
    }
}
