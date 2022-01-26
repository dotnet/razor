// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Debugging
{
    internal class RazorBreakpointSpanParams : IRequest<RazorBreakpointSpanResponse>
    {
        public Uri Uri { get; set; }

        public Position Position { get; set; }
    }
}
