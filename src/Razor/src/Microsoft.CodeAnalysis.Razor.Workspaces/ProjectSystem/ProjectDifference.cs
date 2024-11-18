// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

[Flags]
internal enum ProjectDifference
{
    None = 1 << 0,
    ConfigurationChanged = 1 << 1,
    DocumentAdded = 1 << 2,
    DocumentRemoved = 1 << 3,
    DocumentChanged = 1 << 4,
}
