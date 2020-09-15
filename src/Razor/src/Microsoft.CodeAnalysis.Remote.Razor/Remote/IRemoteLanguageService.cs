// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Remote.Razor
{
    internal interface IRemoteLanguageService
    {
        ValueTask<TagHelperResolutionResult> GetTagHelpersAsync(object solutionInfo, ProjectSnapshotHandle projectHandle, string factoryTypeName, CancellationToken cancellationToken);
    }
}
