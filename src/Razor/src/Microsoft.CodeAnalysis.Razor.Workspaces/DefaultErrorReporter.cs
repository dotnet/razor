// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor
{
    internal class DefaultErrorReporter : ErrorReporter
    {
        public override void ReportError(Exception exception!!)
        {

            // Do nothing.
        }

        public override void ReportError(Exception exception!!, ProjectSnapshot project)
        {

            // Do nothing.
        }

        public override void ReportError(Exception exception!!, Project workspaceProject)
        {

            // Do nothing.
        }
    }
}
