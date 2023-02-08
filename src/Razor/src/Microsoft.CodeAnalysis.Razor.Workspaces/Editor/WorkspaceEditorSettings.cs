﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Host;

namespace Microsoft.CodeAnalysis.Razor.Editor;

internal abstract class WorkspaceEditorSettings : ILanguageService
{
    public abstract event EventHandler<ClientSettingsChangedEventArgs> Changed;

    public abstract ClientSettings Current { get; }
}
