// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Features;

namespace Microsoft.VisualStudioCode.RazorExtension.Endpoints;

[Shared]
[RazorVSCodeEndpoint("razor/dynamicFileInfoChanged")]
[ExportRazorStatelessLspService(typeof(RazorDynamicFileChangedEndpoint))]
internal class RazorDynamicFileChangedEndpoint : AbstractRazorNotificationHandler<RazorDynamicFileChangedParams>
{
    public override bool MutatesSolutionState => false;

    public override bool RequiresLSPSolution => throw new NotImplementedException();

    protected override Task HandleNotificationAsync(RazorDynamicFileChangedParams request, RazorRequestContext context, CancellationToken cancellationToken)
    {
        var dynamicFileInfoProvider = context.GetRequiredService<IRazorLspDynamicFileInfoProvider>();
        dynamicFileInfoProvider.Update(request.RazorDocument.Uri);

        return Task.CompletedTask;
    }
}
