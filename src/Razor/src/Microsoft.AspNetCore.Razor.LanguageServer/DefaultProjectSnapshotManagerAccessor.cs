// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class DefaultProjectSnapshotManagerAccessor : ProjectSnapshotManagerAccessor, IDisposable
    {
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly IEnumerable<ProjectSnapshotChangeTrigger> _changeTriggers;
        private readonly FilePathNormalizer _filePathNormalizer;
        private readonly IOptionsMonitor<RazorLSPOptions> _optionsMonitor;
        private readonly AdhocWorkspaceFactory _workspaceFactory;
        private ProjectSnapshotManagerBase _instance;
        private bool _disposed;

        public DefaultProjectSnapshotManagerAccessor(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            IEnumerable<ProjectSnapshotChangeTrigger> changeTriggers,
            FilePathNormalizer filePathNormalizer,
            IOptionsMonitor<RazorLSPOptions> optionsMonitor,
            AdhocWorkspaceFactory workspaceFactory)
        {
            if (projectSnapshotManagerDispatcher == null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            }

            if (changeTriggers == null)
            {
                throw new ArgumentNullException(nameof(changeTriggers));
            }

            if (filePathNormalizer == null)
            {
                throw new ArgumentNullException(nameof(filePathNormalizer));
            }

            if (optionsMonitor is null)
            {
                throw new ArgumentNullException(nameof(optionsMonitor));
            }

            if (workspaceFactory is null)
            {
                throw new ArgumentNullException(nameof(workspaceFactory));
            }

            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _changeTriggers = changeTriggers;
            _filePathNormalizer = filePathNormalizer;
            _optionsMonitor = optionsMonitor;
            _workspaceFactory = workspaceFactory;
        }

        public override ProjectSnapshotManagerBase Instance
        {
            get
            {
                if (_instance == null)
                {
                    var workspace = _workspaceFactory.Create(
                        workspaceServices: new[]
                        {
                            new RemoteProjectSnapshotProjectEngineFactory(_filePathNormalizer, _optionsMonitor)
                        });
                    _instance = new DefaultProjectSnapshotManager(
                        _projectSnapshotManagerDispatcher,
                        new DefaultErrorReporter(),
                        _changeTriggers,
                        workspace);
                }

                return _instance;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _instance?.Workspace.Dispose();
        }
    }
}
