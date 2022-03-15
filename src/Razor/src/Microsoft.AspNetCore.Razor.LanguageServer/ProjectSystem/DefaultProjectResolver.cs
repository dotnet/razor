﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.IO;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem
{
    internal class DefaultProjectResolver : ProjectResolver
    {
        // Internal for testing
        protected internal readonly HostProject MiscellaneousHostProject;

        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly FilePathNormalizer _filePathNormalizer;
        private readonly ProjectSnapshotManagerAccessor _projectSnapshotManagerAccessor;

        public DefaultProjectResolver(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher!!,
            FilePathNormalizer filePathNormalizer!!,
            ProjectSnapshotManagerAccessor projectSnapshotManagerAccessor!!)
        {
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _filePathNormalizer = filePathNormalizer;
            _projectSnapshotManagerAccessor = projectSnapshotManagerAccessor;

            var miscellaneousProjectPath = Path.Combine(TempDirectory.Instance.DirectoryPath, "__MISC_RAZOR_PROJECT__");
            MiscellaneousHostProject = new HostProject(miscellaneousProjectPath, RazorDefaults.Configuration, RazorDefaults.RootNamespace);
        }

        public override bool TryResolveProject(string documentFilePath!!, out ProjectSnapshot projectSnapshot, bool enforceDocumentInProject = true)
        {
            _projectSnapshotManagerDispatcher.AssertDispatcherThread();

            var normalizedDocumentPath = _filePathNormalizer.Normalize(documentFilePath);
            var projects = _projectSnapshotManagerAccessor.Instance.Projects;
            for (var i = 0; i < projects.Count; i++)
            {
                projectSnapshot = projects[i];

                if (projectSnapshot.FilePath == MiscellaneousHostProject.FilePath)
                {
                    if (enforceDocumentInProject &&
                        IsDocumentInProject(projectSnapshot, documentFilePath))
                    {
                        return true;
                    }

                    continue;
                }

                var projectDirectory = _filePathNormalizer.GetDirectory(projectSnapshot.FilePath);
                if (normalizedDocumentPath.StartsWith(projectDirectory, FilePathComparison.Instance) &&
                    (!enforceDocumentInProject || IsDocumentInProject(projectSnapshot, documentFilePath)))
                {
                    return true;
                }
            }

            projectSnapshot = null;
            return false;

            static bool IsDocumentInProject(ProjectSnapshot projectSnapshot, string documentFilePath)
            {
                return projectSnapshot.GetDocument(documentFilePath) != null;
            }
        }

        public override ProjectSnapshot GetMiscellaneousProject()
        {
            _projectSnapshotManagerDispatcher.AssertDispatcherThread();

            var miscellaneousProject = _projectSnapshotManagerAccessor.Instance.GetLoadedProject(MiscellaneousHostProject.FilePath);
            if (miscellaneousProject is null)
            {
                _projectSnapshotManagerAccessor.Instance.ProjectAdded(MiscellaneousHostProject);
                miscellaneousProject = _projectSnapshotManagerAccessor.Instance.GetLoadedProject(MiscellaneousHostProject.FilePath);
            }

            return miscellaneousProject;
        }
    }
}
