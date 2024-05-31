// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal interface IRazorProjectInfoListener
{
    ValueTask UpdatedAsync(RazorProjectInfo projectInfo);
}
