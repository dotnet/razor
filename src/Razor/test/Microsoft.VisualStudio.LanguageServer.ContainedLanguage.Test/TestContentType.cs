﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage.Test;

internal class TestContentType : IContentType
{
    public TestContentType(string typeName)
    {
        TypeName = typeName;
    }

    public string TypeName { get; }

    public string DisplayName => TypeName;

    public IEnumerable<IContentType> BaseTypes => Enumerable.Empty<IContentType>();

    public bool IsOfType(string type) => string.Equals(type, TypeName, StringComparison.OrdinalIgnoreCase);
}
