// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp.Models
{
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
}
