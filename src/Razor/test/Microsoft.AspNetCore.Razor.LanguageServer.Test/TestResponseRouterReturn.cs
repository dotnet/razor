// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test
{
    internal class TestResponseRouterReturn : IResponseRouterReturns
    {
        private readonly object _result;

        public TestResponseRouterReturn(object result)
        {
            _result = result;
        }

        public Task<TResponse> Returning<TResponse>(CancellationToken cancellationToken)
        {
            return Task.FromResult((TResponse)(object)_result);
        }

        public Task ReturningVoid(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
