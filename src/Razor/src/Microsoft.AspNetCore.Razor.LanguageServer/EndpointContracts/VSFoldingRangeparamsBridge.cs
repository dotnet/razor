// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using MediatR;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts
{
    /// <summary>
    /// This class is used as a "bridge" between the O# and VS worlds. Ultimately it only exists because the base <see cref="FoldingRangeParams"/>
    /// type does not implement <see cref="IRequest{TResponse}"/>.
    /// </summary>
    internal class VSFoldingRangeParamsBridge : FoldingRangeParams, IRequest<IEnumerable<FoldingRange>?>
    {
    }
}
