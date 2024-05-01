// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Razor.Serialization;

namespace Microsoft.VisualStudio.Razor.LanguageClient.ProjectSystem;

[Export(typeof(IRazorProjectInfoFileSerializer))]
internal sealed class VisualStudioRazorProjectInfoFileSerializer : RazorProjectInfoFileSerializer
{
}
