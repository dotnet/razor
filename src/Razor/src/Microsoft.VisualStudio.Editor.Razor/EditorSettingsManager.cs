// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Razor.Editor;
using Microsoft.Extensions.Internal;

namespace Microsoft.VisualStudio.Editor.Razor;

public abstract class EditorSettingsManager
{
    public abstract event EventHandler<EditorSettingsChangedEventArgs>? Changed;

    public abstract EditorSettings Current { get; }

    public abstract void Update(EditorSettings updateSettings);
}

public sealed class EditorSettingsChangedEventArgs : EventArgs
{
    public EditorSettingsChangedEventArgs(EditorSettings settings)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        Settings = settings;
    }

    public EditorSettings Settings { get; }
}
