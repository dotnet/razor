﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Text;

internal static class TextBufferExtensions
{
    public static async Task<IVsHierarchy?> GetVsHierarchyAsync(
        this ITextBuffer textBuffer,
        ITextDocumentFactoryService textDocumentFactoryService,
        IServiceProvider serviceProvider,
        JoinableTaskFactory jtf,
        CancellationToken cancellationToken)
    {
        await jtf.SwitchToMainThreadAsync(cancellationToken);

        return textBuffer.GetVsHierarchy(textDocumentFactoryService, serviceProvider, jtf);
    }

    public static IVsHierarchy? GetVsHierarchy(
        this ITextBuffer textBuffer,
        ITextDocumentFactoryService textDocumentFactoryService,
        IServiceProvider serviceProvider,
        JoinableTaskFactory jtf)
    {
        jtf.AssertUIThread();

        // If there's no document we can't find the FileName, or look for a matching hierarchy.
        if (!textDocumentFactoryService.TryGetTextDocument(textBuffer, out var textDocument))
        {
            return null;
        }

        var vsRunningDocumentTable = serviceProvider.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
        Assumes.Present(vsRunningDocumentTable);

        var hresult = vsRunningDocumentTable.FindAndLockDocument(
            dwRDTLockType: (uint)_VSRDTFLAGS.RDT_NoLock,
            textDocument.FilePath,
            out var hierarchy,
            pitemid: out _,
            ppunkDocData: out _,
            pdwCookie: out _);

        return ErrorHandler.Succeeded(hresult)
            ? hierarchy
            : null;
    }

    /// <summary>
    /// Indicates if a <paramref name="textBuffer"/> has the LSP Razor content type. This is represented by the LSP based ASP.NET Core Razor editor.
    /// </summary>
    /// <param name="textBuffer">The text buffer to inspect</param>
    /// <returns><c>true</c> if the text buffers content type represents an ASP.NET Core LSP based Razor editor content type.</returns>
    public static bool IsRazorLSPBuffer(this ITextBuffer textBuffer)
    {
        if (textBuffer is null)
        {
            throw new ArgumentNullException(nameof(textBuffer));
        }

        var matchesContentType = textBuffer.ContentType.IsOfType(RazorConstants.RazorLSPContentTypeName);
        return matchesContentType;
    }
}
