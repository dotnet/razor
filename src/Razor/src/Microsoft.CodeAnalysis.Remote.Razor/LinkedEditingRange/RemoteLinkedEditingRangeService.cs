// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Api;
using Microsoft.CodeAnalysis.Razor.LinkedEditingRange;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.ServiceHub.Framework;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed class RemoteLinkedEditingRangeService(
    IServiceBroker serviceBroker,
    DocumentSnapshotFactory documentSnapshotFactory,
    ILoggerFactory loggerFactory)
    : RazorServiceBase(serviceBroker), IRemoteLinkedEditingRangeService
{
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<RemoteLinkedEditingRangeService>();
    private readonly DocumentSnapshotFactory _documentSnapshotFactory = documentSnapshotFactory;

    public ValueTask<LinePositionSpan[]?> GetRangesAsync(RazorPinnedSolutionInfoWrapper solutionInfo, DocumentId razorDocumentId, LinePosition linePosition, CancellationToken cancellationToken)
        => RazorBrokeredServiceImplementation.RunServiceAsync(
            solutionInfo,
            ServiceBrokerClient,
            solution => GetRangesAsync(solution, razorDocumentId, linePosition, cancellationToken),
            cancellationToken);

    public async ValueTask<LinePositionSpan[]?> GetRangesAsync(Solution solution, DocumentId razorDocumentId, LinePosition linePosition, CancellationToken cancellationToken)
    {
        var razorDocument = solution.GetAdditionalDocument(razorDocumentId);
        if (razorDocument is null)
        {
            return null;
        }

        var snapshot = _documentSnapshotFactory.GetOrCreate(razorDocument);
        var codeDocument = await snapshot.GetGeneratedOutputAsync().ConfigureAwait(false);

        return LinkedEditingRangeHelper.GetLinkedSpans(linePosition, codeDocument, _logger);
    }
}
