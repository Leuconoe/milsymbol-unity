using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Leuconoe.MilsymbolUnity.Editor
{
    /// <summary>
    /// Resolves full paths to external command line tools (node, npm, git) without
    /// relying on the Unity Editor process inheriting the user's login-shell PATH.
    ///
    /// Unity launched from Unity Hub (and from Finder on macOS) does not inherit the
    /// interactive shell PATH, so tools installed through nvm / fnm / volta / Homebrew
    /// or under user-scoped directories are invisible to <see cref="Process"/> when only
    /// the bare executable name is used. This locator probes the well-known install
    /// locations for each platform and, on Unix, falls back to asking a login shell.
    /// </summary>
    public static class MilsymbolToolLocator
    {
        public const string NodeExecutablePrefsKey = "Leuconoe.MilsymbolUnity.NodeExecutable";

        private static readonly bool IsWindows = Application.platform == RuntimePlatform.WindowsEditor;

        // Cache resolved directories so repeated generations do not re-probe the disk.
        private static readonly Dictionary<string, string> ResolvedToolCache = new Dictionary<string, string>();

        /// <summary>
        /// Resolves the node executable. An explicit, existing path supplied by the user
        /// (via the Generator window's "Node Executable" field) always wins.
        /// Returns the bare name "node" as a last resort so a healthy PATH still works.
        /// </summary>
        public static string ResolveNode(string configured = null)
        {
            if (TryUseConfigured(configured, out var configuredPath))
            {
                return configuredPath;
            }

            return ResolveTool("node");
        }

        /// <summary>
        /// Resolves the npm executable (npm.cmd on Windows). Prefers the npm that sits
        /// next to the resolved node binary, since that is the matching version.
        /// </summary>
        public static string ResolveNpm(string configuredNode = null)
        {
            var nodePath = ResolveNode(configuredNode);
            if (!IsBareName(nodePath))
            {
                var nodeDir = Path.GetDirectoryName(nodePath);
                var sibling = FindInDirectory(nodeDir, NpmNames());
                if (!string.IsNullOrEmpty(sibling))
                {
                    return sibling;
                }
            }

            return ResolveTool("npm");
        }

        /// <summary>Resolves the git executable, used for submodule checkout.</summary>
        public static string ResolveGit()
        {
            return ResolveTool("git");
        }

        /// <summary>
        /// Prepends the directories of the resolved tools to the child process PATH so
        /// that spawned tools can find each other (npm shells out to node, npm needs git,
        /// node-gyp needs python on PATH, etc.).
        /// </summary>
        public static void PrepareEnvironment(ProcessStartInfo startInfo, params string[] toolPaths)
        {
            if (startInfo == null)
            {
                return;
            }

            var prepend = new List<string>();
            foreach (var toolPath in toolPaths)
            {
                if (!string.IsNullOrEmpty(toolPath) && !IsBareName(toolPath))
                {
                    var dir = Path.GetDirectoryName(toolPath);
                    if (!string.IsNullOrEmpty(dir) && !prepend.Contains(dir))
                    {
                        prepend.Add(dir);
                    }
                }
            }

            if (prepend.Count == 0)
            {
                return;
            }

            var existing = startInfo.EnvironmentVariables.ContainsKey("PATH")
                ? startInfo.EnvironmentVariables["PATH"]
                : Environment.GetEnvironmentVariable("PATH") ?? "";

            startInfo.EnvironmentVariables["PATH"] =
                string.Join(Path.PathSeparator.ToString(), prepend) +
                (string.IsNullOrEmpty(existing) ? "" : Path.PathSeparator + existing);
        }

        private static bool TryUseConfigured(string configured, out string resolved)
        {
            resolved = null;
            if (string.IsNullOrWhiteSpace(configured) || IsBareName(configured))
            {
                return false;
            }

            if (File.Exists(configured))
            {
                resolved = configured;
                return true;
            }

            // The user may have configured a directory instead of the binary itself.
            if (Directory.Exists(configured))
            {
                var inside = FindInDirectory(configured, NodeNames());
                if (!string.IsNullOrEmpty(inside))
                {
                    resolved = inside;
                    return true;
                }
            }

            return false;
        }

        private static string ResolveTool(string tool)
        {
            if (ResolvedToolCache.TryGetValue(tool, out var cached) && (IsBareName(cached) || File.Exists(cached)))
            {
                return cached;
            }

            var names = NamesFor(tool);

            foreach (var dir in CandidateDirectories())
            {
                var found = FindInDirectory(dir, names);
                if (!string.IsNullOrEmpty(found))
                {
                    ResolvedToolCache[tool] = found;
                    return found;
                }
            }

            var fromShell = QueryLoginShell(tool);
            if (!string.IsNullOrEmpty(fromShell) && File.Exists(fromShell))
            {
                ResolvedToolCache[tool] = fromShell;
                return fromShell;
            }

            // Last resort: bare name. Works whenever Unity did inherit a usable PATH.
            ResolvedToolCache[tool] = names[0];
            return names[0];
        }

        private static string[] NamesFor(string tool)
        {
            switch (tool)
            {
                case "node": return NodeNames();
                case "npm": return NpmNames();
                case "git": return IsWindows ? new[] { "git.exe", "git.cmd" } : new[] { "git" };
                default: return IsWindows ? new[] { tool + ".exe", tool + ".cmd" } : new[] { tool };
            }
        }

        private static string[] NodeNames() => IsWindows ? new[] { "node.exe" } : new[] { "node" };

        private static string[] NpmNames() => IsWindows ? new[] { "npm.cmd", "npm.exe" } : new[] { "npm" };

        private static string FindInDirectory(string dir, IEnumerable<string> names)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                return null;
            }

            foreach (var name in names)
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static IEnumerable<string> CandidateDirectories()
        {
            var dirs = new List<string>();
            var home = Environment.GetEnvironmentVariable("HOME")
                       ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (IsWindows)
            {
                var programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
                var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
                var appData = Environment.GetEnvironmentVariable("APPDATA");
                var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
                var programData = Environment.GetEnvironmentVariable("ProgramData");

                AddIfSet(dirs, programFiles, "nodejs");
                AddIfSet(dirs, programFilesX86, "nodejs");
                AddIfSet(dirs, appData, "npm");
                AddIfSet(dirs, localAppData, "Volta", "bin");
                AddIfSet(dirs, programData, "chocolatey", "bin");

                // nvm-windows exposes the active version through a symlink dir.
                var nvmSymlink = Environment.GetEnvironmentVariable("NVM_SYMLINK");
                if (!string.IsNullOrEmpty(nvmSymlink)) dirs.Add(nvmSymlink);

                // nvm-windows / fnm version stores: pick newest version dir.
                AddNewestVersionChild(dirs, Environment.GetEnvironmentVariable("NVM_HOME"), null);
                if (!string.IsNullOrEmpty(appData))
                {
                    AddNewestVersionChild(dirs, Path.Combine(appData, "nvm"), null);
                    AddNewestVersionChild(dirs, Path.Combine(appData, "fnm", "node-versions"), "installation");
                }
            }
            else
            {
                dirs.Add("/usr/local/bin");
                dirs.Add("/opt/homebrew/bin");
                dirs.Add("/usr/bin");
                dirs.Add("/opt/local/bin");

                if (!string.IsNullOrEmpty(home))
                {
                    dirs.Add(Path.Combine(home, ".volta", "bin"));
                    dirs.Add(Path.Combine(home, ".asdf", "shims"));

                    AddNewestVersionChild(dirs, Path.Combine(home, ".nvm", "versions", "node"), "bin");
                    AddNewestVersionChild(dirs, Path.Combine(home, ".fnm", "node-versions"), Path.Combine("installation", "bin"));
                    AddNewestVersionChild(dirs, Path.Combine(home, "Library", "Application Support", "fnm", "node-versions"), Path.Combine("installation", "bin"));
                    AddNewestVersionChild(dirs, "/usr/local/n/versions/node", "bin");
                }
            }

            return dirs;
        }

        private static void AddIfSet(List<string> dirs, string root, params string[] segments)
        {
            if (string.IsNullOrEmpty(root))
            {
                return;
            }

            var combined = segments != null && segments.Length > 0
                ? Path.Combine(new[] { root }.Concat(segments).ToArray())
                : root;
            dirs.Add(combined);
        }

        /// <summary>
        /// For version-manager layouts (one directory per installed node version),
        /// adds the binary directory of the most recent version (lexicographically last).
        /// </summary>
        private static void AddNewestVersionChild(List<string> dirs, string versionsRoot, string binSubPath)
        {
            if (string.IsNullOrEmpty(versionsRoot) || !Directory.Exists(versionsRoot))
            {
                return;
            }

            try
            {
                var versions = Directory.GetDirectories(versionsRoot)
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                for (var i = versions.Count - 1; i >= 0; i--)
                {
                    var binDir = string.IsNullOrEmpty(binSubPath)
                        ? versions[i]
                        : Path.Combine(versions[i], binSubPath);
                    if (Directory.Exists(binDir))
                    {
                        dirs.Add(binDir);
                    }
                }
            }
            catch
            {
                // Probing is best-effort; ignore unreadable directories.
            }
        }

        /// <summary>
        /// Unix fallback: ask a login shell for the tool location so version managers
        /// configured purely through shell init files are still discovered.
        /// </summary>
        private static string QueryLoginShell(string tool)
        {
            if (IsWindows)
            {
                return null;
            }

            try
            {
                var shell = Environment.GetEnvironmentVariable("SHELL");
                if (string.IsNullOrEmpty(shell) || !File.Exists(shell))
                {
                    shell = "/bin/bash";
                }

                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = shell,
                        Arguments = "-lic " + Quote("command -v " + tool),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    if (!process.WaitForExit(5000))
                    {
                        try { process.Kill(); } catch { /* best effort */ }
                        return null;
                    }

                    var path = output.Trim().Split('\n').FirstOrDefault()?.Trim();
                    return string.IsNullOrEmpty(path) ? null : path;
                }
            }
            catch
            {
                return null;
            }
        }

        private static bool IsBareName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            return value.IndexOf('/') < 0 && value.IndexOf('\\') < 0;
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
