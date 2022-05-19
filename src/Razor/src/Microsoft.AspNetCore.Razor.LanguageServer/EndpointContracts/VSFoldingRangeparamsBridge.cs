// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using MediatR;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts
{
    internal class VSFoldingRangeParamsBridge : TextDocumentPositionParams, IRequest<IEnumerable<FoldingRange>?>
    {
    }
}
