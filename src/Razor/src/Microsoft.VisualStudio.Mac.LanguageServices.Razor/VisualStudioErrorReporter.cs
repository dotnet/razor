﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using MonoDevelop.Core;

namespace Microsoft.VisualStudio.Mac.LanguageServices.Razor
{
    internal class VisualStudioErrorReporter : ErrorReporter
    {
        public override void ReportError(Exception exception)
        {
            if (exception == null)
            {
                Debug.Fail("Null exceptions should not be reported.");
                return;
            }

            LoggingService.LogError(
                Resources.RazorLanguageServiceGeneralError,
                exception);
        }

        public override void ReportError(Exception exception, Project project)
        {
            if (exception == null)
            {
                Debug.Fail("Null exceptions should not be reported.");
                return;
            }

            LoggingService.LogError(
                Resources.FormatRazorLanguageServiceProjectError(project?.Name),
                exception);
        }

        public override void ReportError(Exception exception, ProjectSnapshot project)
        {
            if (exception == null)
            {
                Debug.Fail("Null exceptions should not be reported.");
                return;
            }

            LoggingService.LogError(
                Resources.FormatRazorLanguageServiceProjectSnapshotError(project?.FilePath, exception),
                exception);
        }
    }
}
