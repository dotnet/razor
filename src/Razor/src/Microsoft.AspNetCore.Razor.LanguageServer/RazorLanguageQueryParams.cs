﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using MediatR;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class RazorLanguageQueryParams : IRequest<RazorLanguageQueryResponse>
    {
        public Uri Uri { get; set; }

        public Position Position { get; set; }
    }
}
