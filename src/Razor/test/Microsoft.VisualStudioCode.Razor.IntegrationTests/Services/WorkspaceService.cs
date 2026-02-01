// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.VisualStudioCode.Razor.IntegrationTests.Services;

/// <summary>
/// Service for creating and managing test workspaces.
/// </summary>
public class WorkspaceService(IntegrationTestServices testServices)
{
    private string? _workspacePath;


    /// <summary>
    /// Gets the path to the current test workspace.
    /// </summary>
    public string Path => _workspacePath ?? throw new InvalidOperationException("Workspace not created. Call CreateAsync first.");

    /// <summary>
    /// Gets the workspace folder name.
    /// </summary>
    public string Name => System.IO.Path.GetFileName(_workspacePath) ?? throw new InvalidOperationException("Workspace not created.");

    /// <summary>
    /// Creates a new test workspace with a Blazor project.
    /// </summary>
    public async Task CreateAsync()
    {
        _workspacePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"vscode-razor-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workspacePath);

        testServices.Logger.Log($"Creating test workspace at: {_workspacePath}");

        // Create a new Blazor project using dotnet new (includes .razor files)
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "new blazor --name TestApp --output .",
                WorkingDirectory = _workspacePath,
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
            testServices.Logger.Log($"dotnet new output: {output}");
            testServices.Logger.Log($"dotnet new error: {error}");
            throw new InvalidOperationException($"Failed to create test project: {error}");
        }

        testServices.Logger.Log("Test workspace created successfully");

        // Run dotnet restore to ensure all packages are available
        testServices.Logger.Log("Restoring packages...");
        process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "restore",
                WorkingDirectory = _workspacePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };

        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet restore failed with exit code {process.ExitCode}. " +
                $"stdout: {stdout}, stderr: {stderr}");
        }

        testServices.Logger.Log("Packages restored");
    }

    /// <summary>
    /// Cleans up the test workspace.
    /// </summary>
    public void Cleanup()
    {
        if (_workspacePath != null && Directory.Exists(_workspacePath))
        {
            try
            {
                Directory.Delete(_workspacePath, recursive: true);
                testServices.Logger.Log("Workspace cleaned up");
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
