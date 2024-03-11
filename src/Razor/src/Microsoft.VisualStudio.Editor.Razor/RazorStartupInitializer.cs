// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudio.Editor.Razor;

[Export(typeof(RazorStartupInitializer))]
[method: ImportingConstructor]
internal sealed class RazorStartupInitializer([ImportMany] IEnumerable<IRazorStartupService> services)
{
    public IEnumerable<IRazorStartupService> Services { get; } = services;
}
