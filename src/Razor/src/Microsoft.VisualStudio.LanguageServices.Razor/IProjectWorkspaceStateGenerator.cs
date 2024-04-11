﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.Razor;

internal interface IProjectWorkspaceStateGenerator
{
    Task UpdateAsync(Project? workspaceProject, IProjectSnapshot projectSnapshot, CancellationToken cancellationToken);
}
