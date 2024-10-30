// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;

namespace Microsoft.CodeAnalysis.Remote.Razor;

[Export(typeof(RemoteClientCapabilities)), Shared]
internal class RemoteClientCapabilities
{
    public bool SupportsVisualStudioExtensions { get; set; }
}
