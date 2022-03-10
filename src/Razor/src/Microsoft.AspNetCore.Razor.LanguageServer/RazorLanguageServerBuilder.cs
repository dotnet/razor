// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class RazorLanguageServerBuilder
    {
        public RazorLanguageServerBuilder(IServiceCollection services!!)
        {
            Services = services;
        }

        public IServiceCollection Services { get; }
    }
}
