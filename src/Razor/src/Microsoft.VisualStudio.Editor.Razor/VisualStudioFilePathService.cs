// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudio.Editor.Razor;

[Export(typeof(IFilePathService)), Shared]
[method: ImportingConstructor]
internal sealed class VisualStudioFilePathService(LanguageServerFeatureOptions options) : AbstractFilePathService(options)
{
}
