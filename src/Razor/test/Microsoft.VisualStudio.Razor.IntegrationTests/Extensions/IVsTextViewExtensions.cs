﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Razor.IntegrationTests.Extensions;

internal static class IVsTextViewExtensions
{
    public static async Task<IWpfTextViewHost> GetTextViewHostAsync(this IVsTextView textView, JoinableTaskFactory joinableTaskFactory, CancellationToken cancellationToken)
    {
        await joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        ErrorHandler.ThrowOnFailure(((IVsUserData)textView).GetData(DefGuidList.guidIWpfTextViewHost, out var wpfTextViewHost));
        return (IWpfTextViewHost)wpfTextViewHost;
    }

    public static async Task<string> GetContentAsync(this IVsTextView vsTextView, JoinableTaskFactory joinableTaskFactory, CancellationToken cancellationToken)
    {
        var textViewHost = await vsTextView.GetTextViewHostAsync(joinableTaskFactory, cancellationToken);
        return textViewHost.TextView.TextSnapshot.GetText();
    }
}
