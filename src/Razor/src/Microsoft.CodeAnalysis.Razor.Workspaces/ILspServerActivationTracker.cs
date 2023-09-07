﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

internal interface ILspServerActivationTracker
{
    bool IsActive { get; }

    void Activated();

    void Deactivated();
}
