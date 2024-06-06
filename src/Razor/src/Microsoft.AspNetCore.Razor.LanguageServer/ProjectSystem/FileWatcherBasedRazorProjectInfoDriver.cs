// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

internal class FileWatcherBasedRazorProjectInfoDriver : IRazorProjectInfoDriver
{
    // TODO: Implement!

    private ImmutableArray<IRazorProjectInfoListener> _listeners;

    public ImmutableArray<RazorProjectInfo> GetLatestProjectInfo() => [];

    public void AddListener(IRazorProjectInfoListener listener)
    {
        ImmutableInterlocked.Update(ref _listeners, array => array.Add(listener));
    }
}
