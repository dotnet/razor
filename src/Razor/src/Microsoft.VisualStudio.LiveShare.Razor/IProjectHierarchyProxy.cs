﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.LiveShare.Razor;

internal interface IProjectHierarchyProxy
{
    Task<Uri?> GetProjectPathAsync(Uri documentFilePath, CancellationToken cancellationToken);
}
