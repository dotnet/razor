// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudioCode.Razor.E2ETests.Infrastructure;

/// <summary>
/// Base class for VS Code integration tests.
/// Manages the VS Code lifecycle - each test gets its own VS Code instance for isolation.
/// </summary>
public abstract class VSCodeIntegrationTestBase(ITestOutputHelper output) : IAsyncLifetime
{
    private VSCodeFixture _fixture = null!;
    private RazorEditorHelpers _razor = null!;

    /// <summary>
    /// The test output helper for logging.
    /// </summary>
    protected ITestOutputHelper TestOutput { get; } = output;

    /// <summary>
    /// The VS Code fixture for this test.
    /// </summary>
    protected VSCodeFixture Fixture => _fixture;

    /// <summary>
    /// The Playwright page connected to VS Code.
    /// </summary>
    protected IPage Page => _fixture.Page;

    /// <summary>
    /// Test settings.
    /// </summary>
    protected TestSettings Settings => _fixture.Settings;

    /// <summary>
    /// Razor-specific editor helpers.
    /// </summary>
    protected RazorEditorHelpers Razor => _razor;

    /// <summary>
    /// The underlying VS Code editor helper.
    /// </summary>
    protected VSCodeEditor Editor => _razor.Editor;

    public virtual async Task InitializeAsync()
    {
        _fixture = await VSCodeFixture.CreateAsync(TestOutput);
        _razor = new RazorEditorHelpers(_fixture.Page, _fixture.Settings, TestOutput);
    }

    public virtual async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    /// <summary>
    /// Opens a file and waits for the LSP and Razor to be ready.
    /// </summary>
    protected async Task OpenFileAndWaitForReadyAsync(string relativePath)
    {
        await Fixture.OpenFileAsync(relativePath);
        await Fixture.WaitForLspReadyAsync();
        await Razor.WaitForRazorReadyAsync();
    }
}
