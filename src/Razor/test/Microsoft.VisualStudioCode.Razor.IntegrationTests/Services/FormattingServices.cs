// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.VisualStudioCode.Razor.IntegrationTests.Services;

/// <summary>
/// Services for formatting operations in integration tests.
/// </summary>
public class FormattingServices(IntegrationTestServices testServices)
{
    /// <summary>
    /// Formats the entire document using the command palette.
    /// </summary>
    public async Task FormatDocumentAsync()
    {
        // Formatting could be async, so make sure we save after the edit, so WaitForEditorTextChangeAsync works correctly
        await testServices.Editor.SaveAsync();
        testServices.Logger.Log("Formatting document via command palette...");
        await testServices.Editor.ExecuteCommandAsync("Format Document");
        testServices.Logger.Log("Format Document command executed");
        await testServices.Editor.WaitForEditorDirtyAsync();
        testServices.Logger.Log("Editor is dirty after formatting");
    }
}
