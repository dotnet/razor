// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor;

internal interface IRazorGeneratedDocumentProvider : IWorkspaceService
{
    Task<(string CSharp, string Html, string Json)> GetGeneratedDocumentAsync(IDocumentSnapshot documentSnapshot);
}
