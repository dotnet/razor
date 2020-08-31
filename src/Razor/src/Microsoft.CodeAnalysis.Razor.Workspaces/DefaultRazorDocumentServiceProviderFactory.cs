// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.Workspaces
{
    [Shared]
    [Export(typeof(RazorDocumentServiceProviderFactory))]
    [Export(typeof(ProjectSnapshotChangeTrigger))]
    internal class DefaultRazorDocumentServiceProviderFactory : RazorDocumentServiceProviderFactory
    {
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private ProjectSnapshotManagerBase _projectManager;

        [ImportingConstructor]
        public DefaultRazorDocumentServiceProviderFactory(ForegroundDispatcher foregroundDispatcher)
        {
            if (foregroundDispatcher == null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
            }

            _foregroundDispatcher = foregroundDispatcher;
        }

        public override void Initialize(ProjectSnapshotManagerBase projectManager)
        {
            if (projectManager == null)
            {
                throw new ArgumentNullException(nameof(projectManager));
            }

            _projectManager = projectManager;
        }

        public override IRazorDocumentServiceProvider Create(DynamicDocumentContainer documentContainer)
        {
            if (documentContainer is null)
            {
                throw new ArgumentNullException(nameof(documentContainer));
            }

            return new RazorDocumentServiceProvider(_foregroundDispatcher, _projectManager, documentContainer);
        }

        public override IRazorDocumentServiceProvider CreateEmpty()
        {
            return new RazorDocumentServiceProvider();
        }
    }
}
