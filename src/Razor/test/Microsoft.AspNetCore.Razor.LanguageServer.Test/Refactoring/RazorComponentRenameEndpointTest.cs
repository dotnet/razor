using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Razor.Extensions.Version2_X;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Moq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring.Test
{
    public class RazorComponentRenameEndpointTest : LanguageServerTestBase
    {
        private RazorProjectEngine _projectEngine;
        private RazorComponentRenameEndpoint _endpoint;

        public RazorComponentRenameEndpointTest()
        {
            CreateEndpoint();
        }

        [Fact]
        public async Task Handle_SimpleRename()
        {
            // Arrange
            var request = new RenameParams
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = new Uri("file:///c:/First/Component1.razor")
                },
                Position = new Position(1, 1),
                NewName = "Component5"
            };

            // Act
            var result = await _endpoint.Handle(request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
        }

        private static TagHelperDescriptor CreateRazorComponentTagHelperDescriptor(string assemblyName, string namespaceName, string tagName)
        {
            var fullyQualifiedName = $"{namespaceName}.{tagName}";
            var builder1 = TagHelperDescriptorBuilder.Create(fullyQualifiedName, assemblyName);
            builder1.TagMatchingRule(rule => rule.TagName = tagName);
            return builder1.Build();
        }

        private static TestRazorProjectItem CreateProjectItem(string text, string filePath)
        {
            var item = new TestRazorProjectItem(filePath, filePath, filePath, "/", FileKinds.Component)
            {
                Content = text
            };
            return item;
        }

        private DocumentSnapshot CreateRazorDocumentSnapshot(TestRazorProjectItem item, string rootNamespaceName)
        {
            var codeDocument = _projectEngine.ProcessDesignTime(item);

            var namespaceNode = (NamespaceDeclarationIntermediateNode)codeDocument
                .GetDocumentIntermediateNode()
                .FindDescendantNodes<IntermediateNode>()
                .FirstOrDefault(n => n is NamespaceDeclarationIntermediateNode);
            namespaceNode.Content = rootNamespaceName;

            var sourceText = SourceText.From(new string(item.Content));
            var documentSnapshot = Mock.Of<DocumentSnapshot>(d =>
                d.GetGeneratedOutputAsync() == Task.FromResult(codeDocument) &&
                d.FilePath == item.FilePath &&
                d.FileKind == FileKinds.Component &&
                d.GetTextAsync() == Task.FromResult(sourceText));
            return documentSnapshot;
        }

        private void CreateEndpoint()
        {
            var tag1 = CreateRazorComponentTagHelperDescriptor("First", "First.Components", "Component1");
            var tag2 = CreateRazorComponentTagHelperDescriptor("First", "Test", "Component2");
            var tag3 = CreateRazorComponentTagHelperDescriptor("Second", "Second.Components", "Component3");
            var tag4 = CreateRazorComponentTagHelperDescriptor("Second", "Second.Components", "Component4");
            var tagHelperDescriptors = new[] { tag1, tag2, tag3, tag4 };

            var item1 = CreateProjectItem("@using Test\n<Component2></Component2>", "c:/First/Component1.razor");
            var item2 = CreateProjectItem("@namespace Test", "c:/First/Component2.razor");
            var item3 = CreateProjectItem("<Component3></Component3>", "c:/Second/Component3.razor");
            var item4 = CreateProjectItem("<Component3></Component3>", "c:/Second/Component4.razor");
            var fileSystem = new TestRazorProjectFileSystem(new[] { item1, item2, item3, item4 });

            _projectEngine = RazorProjectEngine.Create(RazorConfiguration.Default, fileSystem, builder => {
                builder.AddDirective(NamespaceDirective.Directive);
                builder.AddTagHelpers(tagHelperDescriptors);
            });
            _projectEngine.ProcessDeclarationOnly(item1);
            _projectEngine.ProcessDeclarationOnly(item2);
            _projectEngine.ProcessDeclarationOnly(item3);
            _projectEngine.ProcessDeclarationOnly(item4);

            var component1 = CreateRazorDocumentSnapshot(item1, "First.Components");
            var component2 = CreateRazorDocumentSnapshot(item2, "Test");
            var component3 = CreateRazorDocumentSnapshot(item3, "Second.Components");
            var component4 = CreateRazorDocumentSnapshot(item4, "Second.Components");

            var firstProject = Mock.Of<ProjectSnapshot>(p =>
                p.FilePath == "c:/First/First.csproj" &&
                p.DocumentFilePaths == new[] { "c:/First/Component1.razor", "c:/First/Component2.razor" } &&
                p.GetDocument("c:/First/Component1.razor") == component1 &&
                p.GetDocument("c:/First/Component2.razor") == component2);

            var secondProject = Mock.Of<ProjectSnapshot>(p =>
                p.FilePath == "c:/Second/Second.csproj" &&
                p.DocumentFilePaths == new[] { "c:/Second/Component3.razor" } &&
                p.GetDocument("c:/Second/Component3.razor") == component3 &&
                p.GetDocument("c:/Second/Component4.razor") == component4);

            var projectSnapshotManager = Mock.Of<ProjectSnapshotManagerBase>(p => p.Projects == new[] { firstProject, secondProject });
            var projectSnapshotManagerAccessor = new TestProjectSnapshotManagerAccessor(projectSnapshotManager);

            var documentResolver = Mock.Of<DocumentResolver>(d =>
                d.TryResolveDocument("c:/First/Component1.razor", out component1) == true &&
                d.TryResolveDocument("c:/First/Component2.razor", out component2) == true &&
                d.TryResolveDocument("c:/Second/Component3.razor", out component3) == true &&
                d.TryResolveDocument("c:/Second/Component4.razor", out component4) == true);

            var searchEngine = new DefaultRazorComponentSearchEngine(Dispatcher, projectSnapshotManagerAccessor);
            _endpoint = new RazorComponentRenameEndpoint(Dispatcher, documentResolver, searchEngine, projectSnapshotManagerAccessor);
        }

        internal class TestProjectSnapshotManagerAccessor : ProjectSnapshotManagerAccessor
        {
            public TestProjectSnapshotManagerAccessor(ProjectSnapshotManagerBase instance)
            {
                Instance = instance;
            }

            public override ProjectSnapshotManagerBase Instance { get; }
        }
    }
}
