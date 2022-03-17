// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    internal static class PositionExtensions
    {
        public static int CompareTo(this Position position!!, Position other!!)
        {
            var result = position.Line.CompareTo(other.Line);
            return (result != 0) ? result : position.Character.CompareTo(other.Character);
        }
    }
}
