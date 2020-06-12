// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp.Models
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
}
