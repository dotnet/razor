// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudioCode.Razor.E2ETests.Infrastructure;

/// <summary>
/// Downloads and installs VS Code to a local directory for isolated E2E testing.
/// </summary>
public static class VSCodeInstaller
{
    private const string CSharpExtensionId = "ms-dotnettools.csharp";

    /// <summary>
    /// Ensures VS Code is installed in the specified directory.
    /// </summary>
    public static async Task<string> EnsureVSCodeInstalledAsync(string installDir, bool useInsiders = false)
    {
        var vscodeDir = Path.Combine(installDir, useInsiders ? "vscode-insiders" : "vscode");
        var executablePath = GetExecutablePath(vscodeDir);

        if (File.Exists(executablePath))
        {
            Console.WriteLine($"VS Code already installed at: {vscodeDir}");
            return executablePath;
        }

        Console.WriteLine($"Downloading VS Code to: {vscodeDir}");
        Directory.CreateDirectory(vscodeDir);

        var downloadUrl = GetDownloadUrl(useInsiders);
        var archivePath = Path.Combine(installDir, GetArchiveFileName());

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(10);

        // Download VS Code
        Console.WriteLine($"Downloading from: {downloadUrl}");
        using (var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            using var fileStream = File.Create(archivePath);
            await response.Content.CopyToAsync(fileStream);
        }

        Console.WriteLine("Extracting VS Code...");
        ExtractArchive(archivePath, vscodeDir);

        // Clean up archive
        File.Delete(archivePath);

        executablePath = GetExecutablePath(vscodeDir);
        if (!File.Exists(executablePath))
        {
            throw new InvalidOperationException($"VS Code executable not found after installation: {executablePath}");
        }

        Console.WriteLine($"VS Code installed successfully: {executablePath}");
        return executablePath;
    }

    /// <summary>
    /// Installs an extension from the VS Code marketplace.
    /// </summary>
    public static async Task InstallExtensionAsync(string vscodePath, string extensionId, string? extensionsDir = null, bool preRelease = false)
    {
        Console.WriteLine($"Installing extension: {extensionId}{(preRelease ? " (pre-release)" : "")}");

        var args = new List<string>
        {
            "--install-extension", extensionId,
            "--force" // Overwrite if already installed
        };

        if (preRelease)
        {
            args.Add("--pre-release");
        }

        if (!string.IsNullOrEmpty(extensionsDir))
        {
            args.AddRange(["--extensions-dir", extensionsDir]);
        }

        var cliPath = GetCliPath(vscodePath);
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = cliPath,
                Arguments = string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a)),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            Console.WriteLine($"Extension install output: {output}");
            Console.WriteLine($"Extension install error: {error}");
            throw new InvalidOperationException($"Failed to install extension {extensionId}: {error}");
        }

        Console.WriteLine($"Extension {extensionId} installed successfully");
    }

    /// <summary>
    /// Installs the C# extension required for Razor language support (pre-release version).
    /// </summary>
    public static async Task InstallCSharpExtensionAsync(string vscodePath, string? extensionsDir = null)
    {
        // Use pre-release version to get latest Razor language server features
        await InstallExtensionAsync(vscodePath, CSharpExtensionId, extensionsDir, preRelease: true);
    }

    /// <summary>
    /// Checks if an extension is installed.
    /// </summary>
    public static async Task<bool> IsExtensionInstalledAsync(string vscodePath, string extensionId, string? extensionsDir = null)
    {
        var args = new List<string> { "--list-extensions" };

        if (!string.IsNullOrEmpty(extensionsDir))
        {
            args.AddRange(new[] { "--extensions-dir", extensionsDir });
        }

        var cliPath = GetCliPath(vscodePath);
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = cliPath,
                Arguments = string.Join(" ", args),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return output.Contains(extensionId, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDownloadUrl(bool useInsiders)
    {
        var channel = useInsiders ? "insider" : "stable";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
            return $"https://update.code.visualstudio.com/latest/win32-{arch}-archive/{channel}";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "darwin-arm64" : "darwin";
            return $"https://update.code.visualstudio.com/latest/{arch}/{channel}";
        }
        else // Linux
        {
            var arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
            return $"https://update.code.visualstudio.com/latest/{arch}/{channel}";
        }
    }

    private static string GetArchiveFileName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "vscode.zip";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "vscode.zip";
        }
        else
        {
            return "vscode.tar.gz";
        }
    }

    private static void ExtractArchive(string archivePath, string destDir)
    {
        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archivePath, destDir);
        }
        else if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            // Use tar command on Unix systems
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "tar",
                    Arguments = $"-xzf \"{archivePath}\" -C \"{destDir}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("Failed to extract VS Code archive");
            }
        }
    }

    private static string GetExecutablePath(string vscodeDir)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(vscodeDir, "Code.exe");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // The zip extracts to "Visual Studio Code.app"
            return Path.Combine(vscodeDir, "Visual Studio Code.app", "Contents", "MacOS", "Electron");
        }
        else // Linux
        {
            return Path.Combine(vscodeDir, "code");
        }
    }

    private static string GetCliPath(string vscodePath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // The CLI is in the bin folder relative to Code.exe
            var dir = Path.GetDirectoryName(vscodePath)!;
            return Path.Combine(dir, "bin", "code.cmd");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Convert Electron path to CLI path
            // /path/to/Visual Studio Code.app/Contents/MacOS/Electron
            // -> /path/to/Visual Studio Code.app/Contents/Resources/app/bin/code
            var appPath = vscodePath.Replace("/Contents/MacOS/Electron", "");
            return Path.Combine(appPath, "Contents", "Resources", "app", "bin", "code");
        }
        else // Linux
        {
            return vscodePath;
        }
    }

    /// <summary>
    /// Gets the CLI path for the given VS Code executable path.
    /// The CLI should be used for launching VS Code with folder arguments.
    /// </summary>
    public static string GetCliPathForExecutable(string vscodePath)
    {
        return GetCliPath(vscodePath);
    }
}
