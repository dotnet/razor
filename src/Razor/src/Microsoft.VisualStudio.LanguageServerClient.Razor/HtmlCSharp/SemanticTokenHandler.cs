// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    /// <summary>
    /// Parameters for semantic tokens full Document request.
    /// </summary>
    [DataContract]
    public class SemanticTokensParams
    {
        /// <summary>
        /// Gets or sets an identifier for the document to fetch semantic tokens from.
        /// </summary>
        [DataMember(Name = "textDocument")]
        public TextDocumentIdentifier TextDocument { get; set; }

        /// <summary>
        /// Gets or sets the value of the Progress instance.
        /// </summary>
        [DataMember(Name = Methods.PartialResultTokenName, IsRequired = false)]
        public IProgress<SumType<SemanticTokens, SemanticTokensEdits>> PartialResultToken
        {
            get;
            set;
        }
    }

    /// <summary>
    /// Represents a response from a semantic tokens Document provider Edits request.
    /// </summary>
    [DataContract]
    public class SemanticTokensEdits
    {
        /// <summary>
        /// Gets or sets the Id for the client's new version after applying all
        /// edits to their current semantic tokens data.
        /// </summary>
        [DataMember(Name = "resultId")]
        public string ResultId { get; set; }

        /// <summary>
        /// Gets or sets an array of edits to apply to a previous response from a
        /// semantic tokens Document provider.
        /// </summary>
        [DataMember(Name = "edits", IsRequired = true)]
        public SemanticTokensEdit[] Edits { get; set; }

        /// <summary>
        /// Gets or sets the value of the Progress instance.
        /// </summary>
        [DataMember(Name = Methods.PartialResultTokenName, IsRequired = false)]
        public IProgress<SemanticTokens> PartialResultToken
        {
            get;
            set;
        }
    }

    /// <summary>
    /// Class representing an individual edit incrementally applied to a previous
    /// semantic tokens response from the Document provider.
    /// </summary>
    [DataContract]
    public class SemanticTokensEdit
    {
        /// <summary>
        /// Gets or sets the position in the previous response's <see cref="SemanticTokens.Data"/>
        /// to begin the edit.
        /// </summary>
        [DataMember(Name = "start")]
        public int Start { get; set; }

        /// <summary>
        /// Gets or sets the number of numbers to delete in the <see cref="SemanticTokens.Data"/>
        /// from the previous response.
        /// </summary>
        [DataMember(Name = "deleteCount")]
        public int DeleteCount { get; set; }

        /// <summary>
        /// Gets or sets an array containing the encoded semantic tokens information to insert
        /// into a previous response.
        /// </summary>
        [DataMember(Name = "data")]
        public int[] Data { get; set; }
    }

    /// <summary>
    /// Class representing response to semantic tokens messages.
    /// </summary>
    [DataContract]
    public class SemanticTokens
    {
        /// <summary>
        /// Gets or sets a property that identifies this version of the document's semantic tokens.
        /// </summary>
        [DataMember(Name = "resultId")]
        public string ResultId { get; set; }

        /// <summary>
        /// Gets or sets and array containing encoded semantic tokens data.
        /// </summary>
        [DataMember(Name = "data", IsRequired = true)]
        public int[] Data { get; set; }
    }

    [Shared]
    [ExportLspMethod("textDocument/semanticTokens")]
    internal class SemanticTokenHandler : IRequestHandler<SemanticTokensParams, SemanticTokens>
    {
        private readonly LSPRequestInvoker _requestInvoker;

        [ImportingConstructor]
        public SemanticTokenHandler(LSPRequestInvoker requestInvoker)
        {
            if (requestInvoker is null)
            {
                throw new ArgumentNullException(nameof(requestInvoker));
            }

            _requestInvoker = requestInvoker;
        }

        public async Task<SemanticTokens> HandleRequestAsync(SemanticTokensParams request, ClientCapabilities clientCapabilities, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (clientCapabilities is null)
            {
                throw new ArgumentNullException(nameof(clientCapabilities));
            }

            var result = await _requestInvoker.ReinvokeRequestOnServerAsync<SemanticTokensParams, SemanticTokens>(
                "textDocument/semanticTokens",
                LanguageServerKind.Razor,
                request,
                cancellationToken).ConfigureAwait(false);

            return result;
        }
    }
}
