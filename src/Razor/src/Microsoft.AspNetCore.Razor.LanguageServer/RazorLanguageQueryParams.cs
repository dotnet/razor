// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using MediatR;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class RazorLanguageQueryParams : IRequest<RazorLanguageQueryResponse>
    {
        public required Uri Uri { get; set; }

        public required Position Position { get; set; }
    }
}
