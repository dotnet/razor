// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Design;

namespace Microsoft.VisualStudio.Razor.IntegrationTests.InProcess
{
    public static class WellKnownCommandNames
    {
        public const string Build_BuildSolution = "Build.BuildSolution";

        public const string View_ErrorList = "View.ErrorList";
    }

    public static class WellKnownCommands
    {
        private const int GoToImplementationInt = 0x0200;

        public static readonly CommandID GoToImplementation = new(Guids.RoslynGroupId, GoToImplementationInt);

        private static class Guids
        {
            public static readonly Guid RoslynGroupId = new("b61e1a20-8c13-49a9-a727-a0ec091647dd");
        }
    }
}
