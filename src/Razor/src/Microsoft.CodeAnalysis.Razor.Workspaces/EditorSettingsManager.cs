// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Internal;

namespace Microsoft.VisualStudio.Editor.Razor;
#pragma warning disable CS0618 // Type or member is obsolete

[Obsolete("Use IClientSettingsManager instead")]
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

[Obsolete("Use ClientSettings instead")]
public sealed class EditorSettings : IEquatable<EditorSettings>
{
    public static readonly EditorSettings Default = new(indentWithTabs: false, indentSize: 4);

    public EditorSettings(bool indentWithTabs, int indentSize)
    {
        if (indentSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(indentSize));
        }

        IndentWithTabs = indentWithTabs;
        IndentSize = indentSize;
    }

    public bool IndentWithTabs { get; }

    public int IndentSize { get; }

    public bool Equals(EditorSettings? other)
        => other is not null &&
           IndentWithTabs == other.IndentWithTabs &&
           IndentSize == other.IndentSize;

    public override bool Equals(object? other)
    {
        return Equals(other as EditorSettings);
    }

    public override int GetHashCode()
    {
        var combiner = HashCodeCombiner.Start();
        combiner.Add(IndentWithTabs);
        combiner.Add(IndentSize);

        return combiner.CombinedHash;
    }
}
