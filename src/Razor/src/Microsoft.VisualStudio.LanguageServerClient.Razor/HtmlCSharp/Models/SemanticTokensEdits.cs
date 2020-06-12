// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp.Models
{
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
}
