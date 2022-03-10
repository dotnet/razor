// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;

namespace Microsoft.CodeAnalysis.Razor.Workspaces.Extensions
{
    internal static class SolutionExtensions
    {
        internal static Project GetRequiredProject(this Solution solution!!, ProjectId projectId!!)
        {
            var project = solution.GetProject(projectId);

            if (project is null)
            {
                throw new InvalidOperationException($"The projectId {projectId} did not exist in {solution}.");
            }

            return project;
        }
    }
}
