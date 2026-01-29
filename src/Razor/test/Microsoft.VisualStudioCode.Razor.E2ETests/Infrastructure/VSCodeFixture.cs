// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudioCode.Razor.E2ETests.Infrastructure;

using Playwright = Playwright.Playwright;

/// <summary>
/// xUnit fixture that manages the VS Code lifecycle for E2E tests.
/// Downloads VS Code, installs extensions, and provides access to the page via Playwright.
/// </summary>
public class VSCodeFixture : IAsyncLifetime
{
    private readonly TestSettings _settings = TestSettings.CreateDefault();
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private string? _workspaceCopyPath;
    private System.Diagnostics.Process? _vsCodeProcess;
    private string? _vsCodeExecutablePath;
    private ITestOutputHelper? _output;

    /// <summary>
    /// Sets the test output helper for logging. Call from test constructor.
    /// </summary>
    public void SetOutput(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Gets the current test output helper.
    /// </summary>
    public ITestOutputHelper Output => _output ?? throw new InvalidOperationException("Output must be set before being used");

    private void Log(string message)
    {
        _output?.WriteLine(message);
    }

    /// <summary>
    /// The Playwright page connected to VS Code.
    /// </summary>
    public IPage Page => _page ?? throw new InvalidOperationException("VS Code not initialized. Call InitializeAsync first.");

    /// <summary>
    /// The test settings.
    /// </summary>
    public TestSettings Settings => _settings;

    /// <summary>
    /// Path to the workspace being used for this test session.
    /// </summary>
    public string WorkspacePath => _workspaceCopyPath ?? throw new InvalidOperationException("Workspace not initialized.");

    public async Task InitializeAsync()
    {
        // Step 1: Ensure VS Code is installed locally
        await EnsureVSCodeInstalledAsync();

        // Step 2: Ensure required extensions are installed
        await EnsureExtensionsInstalledAsync();

        // Step 3: Create a test workspace using dotnet new razor
        _workspaceCopyPath = await CreateTestWorkspaceAsync();

        // Step 4: Configure the workspace to use local Razor builds
        ConfigureWorkspaceSettings();

        // Step 5: Initialize Playwright
        _playwright = await Playwright.CreateAsync();

        // Step 6: Launch VS Code as a separate process, then connect via CDP
        await LaunchVSCodeAsync();

        // Step 7: Connect to VS Code via CDP
        await ConnectToVSCodeAsync();

        // Step 8: Wait for VS Code to be ready
        await WaitForVSCodeReadyAsync();
    }

    private async Task EnsureVSCodeInstalledAsync()
    {
        if (!string.IsNullOrEmpty(_settings.VSCodePath) && File.Exists(_settings.VSCodePath))
        {
            _vsCodeExecutablePath = _settings.VSCodePath;
            Console.WriteLine($"Using configured VS Code path: {_vsCodeExecutablePath}");
            return;
        }

        var installDir = _settings.VSCodeInstallDir
            ?? throw new InvalidOperationException("VSCodeInstallDir not configured");

        Directory.CreateDirectory(installDir);

        _vsCodeExecutablePath = await VSCodeInstaller.EnsureVSCodeInstalledAsync(
            installDir,
            _settings.UseInsiders);
    }

    private async Task EnsureExtensionsInstalledAsync()
    {
        var extensionsDir = _settings.ExtensionsDir
            ?? throw new InvalidOperationException("ExtensionsDir not configured");

        Directory.CreateDirectory(extensionsDir);

        // Check if C# extension is already installed
        if (!await VSCodeInstaller.IsExtensionInstalledAsync(
            _vsCodeExecutablePath!,
            "ms-dotnettools.csharp",
            extensionsDir))
        {
            await VSCodeInstaller.InstallCSharpExtensionAsync(_vsCodeExecutablePath!, extensionsDir);
        }
        else
        {
            Console.WriteLine("C# extension already installed");
        }
    }

    private async Task LaunchVSCodeAsync()
    {
        // Verify workspace exists
        if (!Directory.Exists(_workspaceCopyPath))
        {
            throw new InvalidOperationException($"Workspace directory does not exist: {_workspaceCopyPath}");
        }

        Console.WriteLine($"Workspace directory verified: {_workspaceCopyPath}");
        Console.WriteLine($"Workspace contents: {string.Join(", ", Directory.GetFileSystemEntries(_workspaceCopyPath!).Select(Path.GetFileName))}");

        // Use the CLI (code.cmd) instead of the Electron executable for proper folder opening
        var cliPath = VSCodeInstaller.GetCliPathForExecutable(_vsCodeExecutablePath!);

        if (!File.Exists(cliPath))
        {
            throw new InvalidOperationException($"VS Code CLI not found: {cliPath}");
        }

        var args = BuildVSCodeArgs();
        var processArgs = string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));

        Console.WriteLine($"Launching VS Code: {cliPath} {processArgs}");

        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = cliPath,
                Arguments = processArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }
        };
        process.Start();
        _vsCodeProcess = process;

        // Give VS Code time to start and open the debugging port
        Console.WriteLine("Waiting for VS Code to start...");
        await Task.Delay(5000);

        // Check if process is still running
        if (process.HasExited)
        {
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            Console.WriteLine($"VS Code CLI exited with code {process.ExitCode}");
            Console.WriteLine($"stdout: {stdout}");
            Console.WriteLine($"stderr: {stderr}");
        }
    }

    private async Task ConnectToVSCodeAsync()
    {
        var cdpUrl = $"http://localhost:{_settings.RemoteDebuggingPort}";
        Console.WriteLine($"Connecting to VS Code via CDP: {cdpUrl}");

        var retries = 5;
        while (retries > 0)
        {
            try
            {
                _browser = await _playwright!.Chromium.ConnectOverCDPAsync(cdpUrl);

                // Find the page that has the workspace open (look for the workbench with our folder)
                _page = await FindWorkspacePageAsync();

                if (_page != null)
                {
                    _context = _page.Context;
                    Console.WriteLine("Connected to VS Code workspace window successfully");
                    return;
                }

                // Fallback to first available page if we couldn't find the workspace
                _context = _browser.Contexts.FirstOrDefault() ?? await _browser.NewContextAsync();
                _page = _context.Pages.FirstOrDefault() ?? await _context.NewPageAsync();
                Console.WriteLine("Connected to VS Code (fallback to first page)");
                return;
            }
            catch (Exception ex) when (retries > 1)
            {
                Console.WriteLine($"Failed to connect, retrying... ({ex.Message})");
                await Task.Delay(2000);
                retries--;
            }
        }

        throw new InvalidOperationException("Failed to connect to VS Code via CDP after multiple retries");
    }

    private async Task<IPage?> FindWorkspacePageAsync()
    {
        var workspaceName = Path.GetFileName(_workspaceCopyPath);
        Console.WriteLine($"Looking for workspace page with folder: {workspaceName}");

        foreach (var context in _browser!.Contexts)
        {
            foreach (var page in context.Pages)
            {
                try
                {
                    // Check if this page has the VS Code workbench
                    var workbench = await page.QuerySelectorAsync(".monaco-workbench");
                    if (workbench == null)
                    {
                        continue;
                    }

                    // Check if the title or explorer contains our workspace name
                    var title = await page.TitleAsync();
                    Console.WriteLine($"Found VS Code page with title: {title}");

                    if (title.Contains(workspaceName!, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"Matched workspace by title");
                        return page;
                    }

                    // Also check for explorer view showing the folder
                    var explorerTitle = await page.QuerySelectorAsync(".explorer-folders-view .monaco-icon-label");
                    if (explorerTitle != null)
                    {
                        var explorerText = await explorerTitle.TextContentAsync();
                        Console.WriteLine($"Explorer shows: {explorerText}");
                        if (explorerText?.Contains(workspaceName!, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            Console.WriteLine($"Matched workspace by explorer");
                            return page;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error checking page: {ex.Message}");
                }
            }
        }

        // If we only have one page with a workbench, use that
        var pagesWithWorkbench = new List<IPage>();
        foreach (var context in _browser.Contexts)
        {
            foreach (var page in context.Pages)
            {
                try
                {
                    var workbench = await page.QuerySelectorAsync(".monaco-workbench");
                    if (workbench != null)
                    {
                        pagesWithWorkbench.Add(page);
                    }
                }
                catch { }
            }
        }

        if (pagesWithWorkbench.Count == 1)
        {
            Console.WriteLine("Only one VS Code page found, using it");
            return pagesWithWorkbench[0];
        }

        Console.WriteLine($"Found {pagesWithWorkbench.Count} VS Code pages, could not determine correct one");
        return null;
    }

    public async Task DisposeAsync()
    {
        Log("Disposing VS Code fixture...");

        if (_page != null)
        {
            await _page.CloseAsync();
            _page = null;
        }

        if (_context != null)
        {
            await _context.CloseAsync();
            _context = null;
        }

        if (_browser != null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;

        // Kill the VS Code process
        if (_vsCodeProcess != null && !_vsCodeProcess.HasExited)
        {
            try
            {
                _vsCodeProcess.Kill(entireProcessTree: true);
                _vsCodeProcess.Dispose();
            }
            catch
            {
                // Ignore cleanup errors
            }

            _vsCodeProcess = null;
        }

        // Clean up workspace copy (but not the VS Code installation or extensions)
        if (_workspaceCopyPath != null && Directory.Exists(_workspaceCopyPath))
        {
            try
            {
                Directory.Delete(_workspaceCopyPath, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        Log("VS Code fixture disposed.");
    }

    /// <summary>
    /// Creates and initializes a new VS Code fixture for a test.
    /// Use this instead of IClassFixture for per-test isolation.
    /// </summary>
    public static async Task<VSCodeFixture> CreateAsync(ITestOutputHelper output)
    {
        var fixture = new VSCodeFixture();
        fixture.SetOutput(output);
        await fixture.InitializeAsync();
        return fixture;
    }

    private static async Task<string> CreateTestWorkspaceAsync()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), $"vscode-razor-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspacePath);

        Console.WriteLine($"Creating test workspace at: {workspacePath}");

        // Create a new Blazor project using dotnet new (includes .razor files)
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "new blazor --name TestApp --output .",
                WorkingDirectory = workspacePath,
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
            Console.WriteLine($"dotnet new output: {output}");
            Console.WriteLine($"dotnet new error: {error}");
            throw new InvalidOperationException($"Failed to create test project: {error}");
        }

        Console.WriteLine("Test workspace created successfully");

        // Run dotnet restore to ensure all packages are available
        Console.WriteLine("Restoring packages...");
        process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "restore",
                WorkingDirectory = workspacePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };

        process.Start();
        await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        Console.WriteLine("Packages restored");

        return workspacePath;
    }

    private void ConfigureWorkspaceSettings()
    {
        // Configure user-level settings to prevent session restore and unwanted windows
        ConfigureUserSettings();

        if (string.IsNullOrEmpty(_settings.RazorExtensionPath))
        {
            return; // Use bundled extension
        }

        var vscodeDir = Path.Combine(_workspaceCopyPath!, ".vscode");
        Directory.CreateDirectory(vscodeDir);

        var settingsPath = Path.Combine(vscodeDir, "settings.json");
        var settings = new Dictionary<string, object>();

        if (File.Exists(settingsPath))
        {
            var existing = File.ReadAllText(settingsPath);
            settings = JsonSerializer.Deserialize<Dictionary<string, object>>(existing) ?? [];
        }

        // Add the Razor extension path
        settings["dotnet.server.componentPaths"] = new Dictionary<string, string>
        {
            ["razorExtension"] = _settings.RazorExtensionPath
        };

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(settingsPath, json);
    }

    private void ConfigureUserSettings()
    {
        if (string.IsNullOrEmpty(_settings.UserDataDir))
        {
            return;
        }

        // Clear any cached window state to prevent window restoration
        ClearWindowState();

        // Create the User settings directory
        var userSettingsDir = Path.Combine(_settings.UserDataDir, "User");
        Directory.CreateDirectory(userSettingsDir);

        var settingsPath = Path.Combine(userSettingsDir, "settings.json");
        var settings = new Dictionary<string, object>
        {
            // Disable session restore - this prevents VS Code from opening previous windows
            ["window.restoreWindows"] = "none",
            // Don't reopen folders
            ["window.reopenFolders"] = "none",
            // Open files in the same window
            ["window.openFilesInNewWindow"] = "off",
            // Open folders in the same window
            ["window.openFoldersInNewWindow"] = "off",
            // Don't open untitled editors
            ["workbench.startupEditor"] = "none",
            // Don't restore editors from previous session
            ["workbench.editor.restoreViewState"] = false,
            // Disable telemetry
            ["telemetry.telemetryLevel"] = "off",
            // Disable update checks
            ["update.mode"] = "none",
            // Disable extension recommendations
            ["extensions.ignoreRecommendations"] = true,
        };

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(settingsPath, json);

        Console.WriteLine($"Configured user settings at: {settingsPath}");
    }

    private void ClearWindowState()
    {
        if (string.IsNullOrEmpty(_settings.UserDataDir))
        {
            return;
        }

        // Clear the storage directory which contains window state
        var storagePath = Path.Combine(_settings.UserDataDir, "User", "globalStorage");
        if (Directory.Exists(storagePath))
        {
            try
            {
                Directory.Delete(storagePath, recursive: true);
                Console.WriteLine("Cleared global storage");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not clear global storage: {ex.Message}");
            }
        }

        // Also clear the workspaceStorage
        var workspaceStoragePath = Path.Combine(_settings.UserDataDir, "User", "workspaceStorage");
        if (Directory.Exists(workspaceStoragePath))
        {
            try
            {
                Directory.Delete(workspaceStoragePath, recursive: true);
                Console.WriteLine("Cleared workspace storage");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not clear workspace storage: {ex.Message}");
            }
        }

        // Clear the backup directory (can contain old window states)
        var backupPath = Path.Combine(_settings.UserDataDir, "Backups");
        if (Directory.Exists(backupPath))
        {
            try
            {
                Directory.Delete(backupPath, recursive: true);
                Console.WriteLine("Cleared backups");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not clear backups: {ex.Message}");
            }
        }
    }

    private string[] BuildVSCodeArgs()
    {
        var args = new List<string>
        {
            _workspaceCopyPath!,
            $"--remote-debugging-port={_settings.RemoteDebuggingPort}",
            "--disable-gpu",
            "--no-sandbox",
            "--skip-welcome",
            "--skip-release-notes",
            "--disable-workspace-trust",
            "--new-window",
        };

        // Use isolated user data and extensions directories to prevent interference
        // with any existing VS Code instances
        if (!string.IsNullOrEmpty(_settings.UserDataDir))
        {
            Directory.CreateDirectory(_settings.UserDataDir);
            args.Add($"--user-data-dir={_settings.UserDataDir}");
        }

        if (!string.IsNullOrEmpty(_settings.ExtensionsDir))
        {
            args.Add($"--extensions-dir={_settings.ExtensionsDir}");
        }

        return [.. args];
    }

    private async Task WaitForVSCodeReadyAsync()
    {
        // Wait for the VS Code window to be visible and the workbench to load
        var timeout = _settings.StartupTimeout;

        Console.WriteLine("Waiting for VS Code workbench to load...");

        // Wait for the main VS Code container
        await Page.WaitForSelectorAsync(".monaco-workbench", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = (float)timeout.TotalMilliseconds
        });

        // Give extensions a moment to initialize
        Console.WriteLine("Workbench loaded, waiting for extensions...");

        // Wait for the status bar to be visible as a sign of full initialization
        try
        {
            await Page.WaitForSelectorAsync(".statusbar", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 10000
            });
        }
        catch (TimeoutException)
        {
            // Status bar should be there, but continue anyway
            Console.WriteLine("Warning: Status bar not found, continuing...");
        }
    }

    /// <summary>
    /// Opens a file in the editor and waits for it to be active.
    /// </summary>
    public async Task OpenFileAsync(string relativePath)
    {
        Console.WriteLine($"Opening file: {relativePath}");

        // Use the Quick Open dialog (Ctrl+P) to open the file
        await Page.Keyboard.PressAsync("Control+p");

        // Wait for Quick Open to appear
        var editor = new VSCodeEditor(Page, _settings, Output);
        await editor.WaitForQuickInputAsync();

        await Page.Keyboard.TypeAsync(relativePath);

        // Wait briefly for the file list to populate
        await Task.Delay(200);

        await Page.Keyboard.PressAsync("Enter");

        // Wait for the file to be open by checking the active tab
        var expectedFileName = Path.GetFileName(relativePath);
        await VSCodeEditor.WaitForConditionAsync(
            async () =>
            {
                var activeTab = await Page.QuerySelectorAsync(".tab.active .monaco-icon-label-container");
                if (activeTab == null)
                    return false;
                var tabText = await activeTab.TextContentAsync();
                return tabText?.Contains(expectedFileName, StringComparison.OrdinalIgnoreCase) == true;
            },
            _settings.LspTimeout);

        Console.WriteLine($"File opened: {relativePath}");
    }

    /// <summary>
    /// Waits for the C# extension and Razor to be ready.
    /// Uses multiple indicators to detect LSP readiness.
    /// </summary>
    public async Task WaitForLspReadyAsync()
    {
        var timeout = _settings.LspTimeout;
        Console.WriteLine("Waiting for C# LSP to be ready...");

        // Strategy 1: Look for C# status bar item
        var csharpReady = false;
        try
        {
            await Page.WaitForSelectorAsync("[aria-label*='C#']", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = (float)(timeout.TotalMilliseconds / 2)
            });
            Console.WriteLine("C# status bar item found");
            csharpReady = true;
        }
        catch (TimeoutException)
        {
            Console.WriteLine("C# status bar item not found, trying alternative detection...");
        }

        // Strategy 2: Check for language mode indicator in status bar
        if (!csharpReady)
        {
            try
            {
                // Look for language mode indicator showing C# or Razor
                await VSCodeEditor.WaitForConditionAsync(
                    async () =>
                    {
                        var languageMode = await Page.QuerySelectorAsync("[aria-label*='Select Language Mode']");
                        if (languageMode == null)
                            return false;
                        var text = await languageMode.TextContentAsync();
                        return text?.Contains("C#", StringComparison.OrdinalIgnoreCase) == true ||
                               text?.Contains("Razor", StringComparison.OrdinalIgnoreCase) == true ||
                               text?.Contains("ASP.NET", StringComparison.OrdinalIgnoreCase) == true;
                    },
                    TimeSpan.FromSeconds(timeout.TotalSeconds / 2));
                Console.WriteLine("Language mode indicator found");
                csharpReady = true;
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Language mode indicator not found");
            }
        }

        // Strategy 3: Look for any loading indicators to disappear
        if (!csharpReady)
        {
            try
            {
                // Wait for any progress/loading indicators to disappear
                await VSCodeEditor.WaitForConditionAsync(
                    async () =>
                    {
                        var loading = await Page.QuerySelectorAsync(".progress-bit");
                        return loading == null;
                    },
                    TimeSpan.FromSeconds(10));
                Console.WriteLine("No loading indicators present");
            }
            catch (TimeoutException)
            {
                // Continue anyway
            }
        }

        Console.WriteLine("C# LSP ready check complete");
    }
}
