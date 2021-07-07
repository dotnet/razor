// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Composition;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.OmniSharpPlugin.StrongNamed;
using OmniSharp;

namespace Microsoft.AspNetCore.Razor.OmniSharpPlugin
{
    // We need to re-export MEF based services from the OmniSharp plugin strong named assembly in order
    // to make those services available via MEF. This isn't an issue for Roslyn based services because
    // we're able to hook into OmniSharp's Roslyn service aggregator to allow it to inspect the strong
    // named plugin assembly.

    [Shared]
    [Export(typeof(FilePathNormalizer))]
    public class ExportedFilePathNormalizer : FilePathNormalizer
    {
    }

    [Shared]
    [Export(typeof(OmniSharpSingleThreadedDispatcher))]
    internal class ExportOmniSharpSingleThreadedDispatcher : DefaultOmniSharpSingleThreadedDispatcher
    {
    }

    [Shared]
    [Export(typeof(RemoteTextLoaderFactory))]
    internal class ExportRemoteTextLoaderFactory : DefaultRemoteTextLoaderFactory
    {
        [ImportingConstructor]
        public ExportRemoteTextLoaderFactory(FilePathNormalizer filePathNormalizer) : base(filePathNormalizer)
        {
        }
    }

    [Shared]
    [Export(typeof(OmniSharpProjectSnapshotManagerAccessor))]
    internal class ExportDefaultOmniSharpProjectSnapshotManagerAccessor : DefaultOmniSharpProjectSnapshotManagerAccessor
    {
        [ImportingConstructor]
        public ExportDefaultOmniSharpProjectSnapshotManagerAccessor(
            RemoteTextLoaderFactory remoteTextLoaderFactory,
            [ImportMany] IEnumerable<IOmniSharpProjectSnapshotManagerChangeTrigger> projectChangeTriggers,
            OmniSharpSingleThreadedDispatcher singleThreadedDispatcher,
            OmniSharpWorkspace workspace) : base(remoteTextLoaderFactory, projectChangeTriggers, singleThreadedDispatcher, workspace)
        {
        }
    }

    [Shared]
    [Export(typeof(IOmniSharpProjectSnapshotManagerChangeTrigger))]
    public class ExportOmniSharpWorkspaceProjectStateChangeDetector : OmniSharpWorkspaceProjectStateChangeDetector
    {
        [ImportingConstructor]
        public ExportOmniSharpWorkspaceProjectStateChangeDetector(
            OmniSharpSingleThreadedDispatcher singleThreadedDispatcher,
            OmniSharpProjectWorkspaceStateGenerator workspaceStateGenerator) : base(singleThreadedDispatcher, workspaceStateGenerator)
        {
        }
    }

    [Shared]
    [Export(typeof(IOmniSharpProjectSnapshotManagerChangeTrigger))]
    [Export(typeof(OmniSharpProjectWorkspaceStateGenerator))]
    public class ExportOmniSharpProjectWorkspaceStateGenerator : OmniSharpProjectWorkspaceStateGenerator
    {
        [ImportingConstructor]
        public ExportOmniSharpProjectWorkspaceStateGenerator(OmniSharpSingleThreadedDispatcher singleThreadedDispatcher) : base(singleThreadedDispatcher)
        {
        }
    }

    [Shared]
    [Export(typeof(IOmniSharpProjectSnapshotManagerChangeTrigger))]
    public class ExportOmniSharpBackgroundDocumentGenerator : OmniSharpBackgroundDocumentGenerator
    {
        [ImportingConstructor]
        public ExportOmniSharpBackgroundDocumentGenerator(
            OmniSharpSingleThreadedDispatcher singleThreadedDispatcher,
            RemoteTextLoaderFactory remoteTextLoaderFactory,
            [ImportMany] IEnumerable<OmniSharpDocumentProcessedListener> documentProcessedListeners) : base(singleThreadedDispatcher, remoteTextLoaderFactory, documentProcessedListeners)
        {
        }
    }
}
