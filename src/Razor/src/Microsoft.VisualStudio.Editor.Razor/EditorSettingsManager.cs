﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Razor.Editor;

namespace Microsoft.VisualStudio.Editor.Razor;

internal abstract class EditorSettingsManager
{
    public abstract event EventHandler<EditorSettingsChangedEventArgs>? Changed;

    public abstract EditorSettings Current { get; }

    public abstract void Update(EditorSettings updateSettings);
}
