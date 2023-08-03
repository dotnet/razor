// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor;

[Export(typeof(VirtualDocumentFactory))]
[ContentType(RazorConstants.RazorLSPContentTypeName)]
internal class CSharpVirtualDocumentFactory : VirtualDocumentFactoryBase
{
    public static readonly string CSharpClientName = "RazorCSharp";
    private static readonly IReadOnlyDictionary<object, object> s_languageBufferProperties = new Dictionary<object, object>
    {
        { LanguageClientConstants.ClientNamePropertyKey, CSharpClientName }
    };

    private static IContentType? s_csharpContentType;
    private readonly FileUriProvider _fileUriProvider;
    private readonly DocumentFilePathProvider _documentFilePathProvider;
    private readonly ProjectSnapshotManagerAccessor _projectSnapshotManagerAccessor;

    [ImportingConstructor]
    public CSharpVirtualDocumentFactory(
        IContentTypeRegistryService contentTypeRegistry,
        ITextBufferFactoryService textBufferFactory,
        ITextDocumentFactoryService textDocumentFactory,
        FileUriProvider fileUriProvider,
        DocumentFilePathProvider documentFilePathProvider,
        ProjectSnapshotManagerAccessor projectSnapshotManagerAccessor)
        : base(contentTypeRegistry, textBufferFactory, textDocumentFactory, fileUriProvider)
    {
        _fileUriProvider = fileUriProvider;
        _documentFilePathProvider = documentFilePathProvider;
        _projectSnapshotManagerAccessor = projectSnapshotManagerAccessor;
    }

    protected override IContentType LanguageContentType
    {
        get
        {
            if (s_csharpContentType is null)
            {
                var contentType = ContentTypeRegistry.GetContentType(RazorLSPConstants.CSharpContentTypeName);
                s_csharpContentType = new RemoteContentDefinitionType(contentType);
            }

            return s_csharpContentType;
        }
    }

    protected override string HostDocumentContentTypeName => RazorConstants.RazorLSPContentTypeName;
    protected override IReadOnlyDictionary<object, object>? LanguageBufferProperties => s_languageBufferProperties;

    protected override string LanguageFileNameSuffix => throw new NotImplementedException("Multiple C# documents per Razor documents are supported, and should be accounted for.");

    protected override VirtualDocument CreateVirtualDocument(Uri uri, ITextBuffer textBuffer)
    {
        throw new NotImplementedException("Multiple C# documents per Razor documents are supported, and should be accounted for.");
    }

    public override bool TryCreateFor(ITextBuffer hostDocumentBuffer, [NotNullWhen(true)] out VirtualDocument? virtualDocument)
    {
        throw new NotImplementedException("Multiple C# documents per Razor documents are supported, and should be accounted for.");
    }

    public override bool TryCreateMultipleFor(ITextBuffer hostDocumentBuffer, [NotNullWhen(true)] out VirtualDocument[]? virtualDocuments)
    {
        if (hostDocumentBuffer is null)
        {
            throw new ArgumentNullException(nameof(hostDocumentBuffer));
        }

        if (!hostDocumentBuffer.ContentType.IsOfType(HostDocumentContentTypeName))
        {
            // Another content type we don't care about.
            virtualDocuments = null;
            return false;
        }

        var newVirtualDocuments = new List<VirtualDocument>();

        var hostDocumentUri = _fileUriProvider.GetOrCreate(hostDocumentBuffer);

        foreach (var projectKey in GetProjectKeys(hostDocumentUri))
        {
            // We just call the base class here, it will call back into us to produce the virtual document uri
            newVirtualDocuments.Add(CreateVirtualDocument(projectKey, hostDocumentUri));
        }

        virtualDocuments = newVirtualDocuments.ToArray();
        return virtualDocuments.Length > 0;
    }

    private IEnumerable<ProjectKey> GetProjectKeys(Uri hostDocumentUri)
    {
        var projects = _projectSnapshotManagerAccessor.Instance.GetProjects();

        var inAny = false;
        var normalizedDocumentPath = FilePathNormalizer.Normalize(hostDocumentUri.GetAbsoluteOrUNCPath());
        foreach (var projectSnapshot in projects)
        {
            // TODO: Can't call projectSnapshot.GetDocument as the paths are not normalized the same. Why?
            foreach (var document in projectSnapshot.DocumentFilePaths)
            {
                if (FilePathNormalizer.FilePathsEquivalent(document, normalizedDocumentPath))
                {
                    inAny = true;
                    yield return projectSnapshot.Key;
                    break;
                }
            }
        }

        if (!inAny)
        {
            // We got called before we know about any projects. Probably just a .razor document being restored in VS from a previous session.
            // All we can do is return a default key and hope for the best.
            // TODO: We should create a Misc Files project on this (VS) side so the nav bar looks nicer
            yield return default;
        }
    }

    private CSharpVirtualDocument CreateVirtualDocument(ProjectKey projectKey, Uri hostDocumentUri)
    {
        var virtualLanguageFilePath = _documentFilePathProvider.GetRazorCSharpFilePath(projectKey, hostDocumentUri.GetAbsoluteOrUNCPath());
        var virtualLanguageUri = new Uri(virtualLanguageFilePath);

        var languageBuffer = CreateVirtualDocumentTextBuffer(virtualLanguageFilePath, virtualLanguageUri);

        return new CSharpVirtualDocument(projectKey, virtualLanguageUri, languageBuffer);
    }

    private class RemoteContentDefinitionType : IContentType
    {
        private static readonly IReadOnlyList<string> s_extendedBaseContentTypes = new[]
        {
            "code-languageserver-base",
            CodeRemoteContentDefinition.CodeRemoteContentTypeName
        };

        private readonly IContentType _innerContentType;

        internal RemoteContentDefinitionType(IContentType innerContentType)
        {
            if (innerContentType is null)
            {
                throw new ArgumentNullException(nameof(innerContentType));
            }

            _innerContentType = innerContentType;
            TypeName = innerContentType.TypeName;
            DisplayName = innerContentType.DisplayName;
        }

        public string TypeName { get; }

        public string DisplayName { get; }

        public IEnumerable<IContentType> BaseTypes => _innerContentType.BaseTypes;

        public bool IsOfType(string type)
        {
            return s_extendedBaseContentTypes.Contains(type) || _innerContentType.IsOfType(type);
        }
    }
}
