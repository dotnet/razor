// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp.Models
{
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
}
