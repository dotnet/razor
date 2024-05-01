// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal partial class ProjectConfigurationStateSynchronizer
{
    private abstract class Work(ProjectKey projectKey)
    {
        public ProjectKey ProjectKey => projectKey;

        public bool Skip { get; set; }
    }

    private sealed class AddProject(RazorProjectInfo projectInfo) : Work(ProjectKey.From(projectInfo))
    {
        public RazorProjectInfo ProjectInfo => projectInfo;

        public void Deconstruct(out RazorProjectInfo projectInfo)
        {
            projectInfo = ProjectInfo;
        }

        public void Deconstruct(out ProjectKey projectKey, out RazorProjectInfo projectInfo)
        {
            projectKey = ProjectKey;
            projectInfo = ProjectInfo;
        }
    }

    private sealed class ResetProject(ProjectKey projectKey) : Work(projectKey)
    {
        public void Deconstruct(out ProjectKey projectKey)
        {
            projectKey = ProjectKey;
        }
    }

    private sealed class UpdateProject(ProjectKey projectKey, RazorProjectInfo projectInfo) : Work(projectKey)
    {
        public RazorProjectInfo ProjectInfo => projectInfo;

        public void Deconstruct(out ProjectKey projectKey, out RazorProjectInfo projectInfo)
        {
            projectKey = ProjectKey;
            projectInfo = ProjectInfo;
        }
    }
}
