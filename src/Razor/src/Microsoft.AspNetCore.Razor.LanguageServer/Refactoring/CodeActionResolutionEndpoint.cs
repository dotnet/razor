using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    class CodeActionResolutionEndpoint : IRazorCodeActionResolutionHandler
    {
        private readonly Dictionary<string, RazorCodeActionResolver> _resolvers;
        private readonly ILogger _logger;

        public CodeActionResolutionEndpoint(
            IEnumerable<RazorCodeActionResolver> resolvers,
            ILoggerFactory loggerFactory)
        {
            if (resolvers is null)
            {
                throw new ArgumentNullException(nameof(resolvers));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _resolvers = new Dictionary<string, RazorCodeActionResolver>();
            foreach (var resolver in resolvers)
            {
                if (_resolvers.ContainsKey(resolver.Action))
                {
                    Debug.Fail($"duplicate resolver action for {resolver.Action}");
                }
                _resolvers[resolver.Action] = resolver;
            }

            _logger = loggerFactory.CreateLogger<CodeActionResolutionEndpoint>();
        }

        public async Task<RazorCodeActionResolutionResponse> Handle(RazorCodeActionResolutionParams request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            _logger.LogDebug($"resolving action {request.Action} with data {request.Data}");
            if (!_resolvers.ContainsKey(request.Action))
            {
                Debug.Fail($"no resolver registered for {request.Action}");
                return new RazorCodeActionResolutionResponse() { Edit = null };
            }

            var edit = await _resolvers[request.Action].ResolveAsync(request.Data, cancellationToken);
            return new RazorCodeActionResolutionResponse() { Edit = edit };
        }
    }
}
