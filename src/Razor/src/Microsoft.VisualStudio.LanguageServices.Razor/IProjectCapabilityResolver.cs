// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.VisualStudio.Razor;

internal interface IProjectCapabilityResolver
{
    /// <summary>
    /// Determines whether the project associated with the specified document has the given <paramref name="capability"/>.
    /// </summary>
    bool ResolveCapability(string capability, string documentFilePath);

    /// <summary>
    /// Tries to return a cached value for the capability check, if a previous call to <see cref="ResolveCapability(string, string)" /> has been made for the same project and capability.
    /// </summary>
    bool TryGetCachedCapabilityMatch(string projectFilePath, string capability, out bool isMatch);
}
