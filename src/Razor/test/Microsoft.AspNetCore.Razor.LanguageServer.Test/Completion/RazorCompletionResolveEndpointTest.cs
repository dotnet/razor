// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Tooltip;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.CodeAnalysis.Razor.Tooltip;
using Moq;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    public class RazorCompletionResolveEndpointTest : LanguageServerTestBase
    {
        public RazorCompletionResolveEndpointTest()
        {
            LSPTagHelperTooltipFactory = Mock.Of<LSPTagHelperTooltipFactory>(MockBehavior.Strict);
            VSLSPTagHelperTooltipFactory = Mock.Of<VSLSPTagHelperTooltipFactory>(MockBehavior.Strict);
            CompletionListCache = new CompletionListCache();
            CompletionCapability = new PlatformAgnosticCompletionCapability();
            DefaultClientCapability = new PlatformAgnosticClientCapabilities();
            VSClientCapability = new PlatformAgnosticClientCapabilities()
            {
                SupportsVisualStudioExtensions = true,
            };
        }

        private LSPTagHelperTooltipFactory LSPTagHelperTooltipFactory { get; }

        private VSLSPTagHelperTooltipFactory VSLSPTagHelperTooltipFactory { get; }

        private CompletionListCache CompletionListCache { get; }

        private CompletionCapability CompletionCapability { get; }

        private PlatformAgnosticClientCapabilities DefaultClientCapability { get; }

        private PlatformAgnosticClientCapabilities VSClientCapability { get; }

        [Fact]
        public async Task Handle_Resolve_DirectiveCompletion_ReturnsCompletionItemWithDocumentation()
        {
            // Arrange
            var languageServer = new Mock<ClientNotifierServiceBase>(MockBehavior.Strict);
            languageServer.Setup(ls => ls.ClientSettings).Returns(new InitializeParams());
            var endpoint = new RazorCompletionResolveEndpoint(LSPTagHelperTooltipFactory, VSLSPTagHelperTooltipFactory, CompletionListCache, LoggerFactory);
            endpoint.SetCapability(CompletionCapability, DefaultClientCapability);
            var razorCompletionItem = new RazorCompletionItem("TestItem", "TestItem", RazorCompletionItemKind.Directive);
            razorCompletionItem.SetDirectiveCompletionDescription(new DirectiveCompletionDescription("Test directive"));
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single();

            // Act
            var newCompletionItem = await endpoint.Handle(completionItem, default);

            // Assert
            Assert.NotNull(newCompletionItem.Documentation);
        }

        [Fact]
        public async Task Handle_Resolve_MarkupTransitionCompletion_ReturnsCompletionItemWithDocumentation()
        {
            // Arrange
            var languageServer = new Mock<ClientNotifierServiceBase>(MockBehavior.Strict);
            languageServer.Setup(ls => ls.ClientSettings).Returns(new InitializeParams());
            var endpoint = new RazorCompletionResolveEndpoint(LSPTagHelperTooltipFactory, VSLSPTagHelperTooltipFactory, CompletionListCache, LoggerFactory);
            endpoint.SetCapability(CompletionCapability, DefaultClientCapability);
            var razorCompletionItem = new RazorCompletionItem("@...", "@", RazorCompletionItemKind.MarkupTransition);
            razorCompletionItem.SetMarkupTransitionCompletionDescription(new MarkupTransitionCompletionDescription("Test description"));
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single();

            // Act
            var newCompletionItem = await endpoint.Handle(completionItem, default);

            // Assert
            Assert.NotNull(newCompletionItem.Documentation);
        }

        [Fact]
        public async Task Handle_Resolve_DirectiveAttributeCompletion_ReturnsCompletionItemWithDocumentation()
        {
            // Arrange
            var lspDescriptionFactory = new Mock<LSPTagHelperTooltipFactory>(MockBehavior.Strict);
            var markdown = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Some Markdown"
            };
            lspDescriptionFactory.Setup(factory => factory.TryCreateTooltip(It.IsAny<AggregateBoundAttributeDescription>(), out markdown))
                .Returns(true);
            var languageServer = new Mock<ClientNotifierServiceBase>(MockBehavior.Strict);
            languageServer.Setup(ls => ls.ClientSettings).Returns(new InitializeParams());
            var endpoint = new RazorCompletionResolveEndpoint(lspDescriptionFactory.Object, VSLSPTagHelperTooltipFactory, CompletionListCache, LoggerFactory);
            endpoint.SetCapability(CompletionCapability, DefaultClientCapability);
            var razorCompletionItem = new RazorCompletionItem("TestItem", "TestItem", RazorCompletionItemKind.DirectiveAttribute);
            razorCompletionItem.SetAttributeCompletionDescription(new AggregateBoundAttributeDescription(Array.Empty<BoundAttributeDescriptionInfo>()));
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single();

            // Act
            var newCompletionItem = await endpoint.Handle(completionItem, default);

            // Assert
            Assert.NotNull(newCompletionItem.Documentation);
        }

        [Fact]
        public async Task Handle_Resolve_DirectiveAttributeParameterCompletion_ReturnsCompletionItemWithDocumentation()
        {
            // Arrange
            var descriptionFactory = new Mock<LSPTagHelperTooltipFactory>(MockBehavior.Strict);
            var markdown = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Some Markdown"
            };
            descriptionFactory.Setup(factory => factory.TryCreateTooltip(It.IsAny<AggregateBoundAttributeDescription>(), out markdown))
                .Returns(true);
            var endpoint = new RazorCompletionResolveEndpoint(descriptionFactory.Object, VSLSPTagHelperTooltipFactory, CompletionListCache, LoggerFactory);
            endpoint.SetCapability(CompletionCapability, DefaultClientCapability);
            var razorCompletionItem = new RazorCompletionItem("TestItem", "TestItem", RazorCompletionItemKind.DirectiveAttributeParameter);
            razorCompletionItem.SetAttributeCompletionDescription(new AggregateBoundAttributeDescription(Array.Empty<BoundAttributeDescriptionInfo>()));
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single();

            // Act
            var newCompletionItem = await endpoint.Handle(completionItem, default);

            // Assert
            Assert.NotNull(newCompletionItem.Documentation);
        }

        [Fact]
        public async Task Handle_Resolve_TagHelperElementCompletion_ReturnsCompletionItemWithDocumentation()
        {
            // Arrange
            var lspDescriptionFactory = new Mock<LSPTagHelperTooltipFactory>(MockBehavior.Strict);
            var markdown = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Some Markdown"
            };
            lspDescriptionFactory.Setup(factory => factory.TryCreateTooltip(It.IsAny<AggregateBoundElementDescription>(), out markdown))
                .Returns(true);
            var endpoint = new RazorCompletionResolveEndpoint(lspDescriptionFactory.Object, VSLSPTagHelperTooltipFactory, CompletionListCache, LoggerFactory);
            endpoint.SetCapability(CompletionCapability, DefaultClientCapability);
            var razorCompletionItem = new RazorCompletionItem("TestItem", "TestItem", RazorCompletionItemKind.TagHelperElement);
            razorCompletionItem.SetTagHelperElementDescriptionInfo(new AggregateBoundElementDescription(Array.Empty<BoundElementDescriptionInfo>()));
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single();

            // Act
            var newCompletionItem = await endpoint.Handle(completionItem, default);

            // Assert
            Assert.NotNull(newCompletionItem.Documentation);
        }

        [Fact]
        public async Task Handle_Resolve_TagHelperElementCompletion_NullCommitCharacters()
        {
            // Arrange
            var vsLSPDescriptionFactory = new Mock<VSLSPTagHelperTooltipFactory>(MockBehavior.Strict);
            var markdown = new VSClassifiedTextElement(new VSClassifiedTextRun("type", "text"));
            vsLSPDescriptionFactory.Setup(factory => factory.TryCreateTooltip(It.IsAny<AggregateBoundElementDescription>(), out markdown))
                .Returns(true);
            var endpoint = new RazorCompletionResolveEndpoint(LSPTagHelperTooltipFactory, vsLSPDescriptionFactory.Object, CompletionListCache, LoggerFactory);
            endpoint.SetCapability(CompletionCapability, VSClientCapability);
            var razorCompletionItem = new RazorCompletionItem("TestItem", "TestItem", RazorCompletionItemKind.TagHelperElement);
            razorCompletionItem.SetTagHelperElementDescriptionInfo(new AggregateBoundElementDescription(Array.Empty<BoundElementDescriptionInfo>()));
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single();

            // For performance we sometimes return null commit characters, so we need to be sure they resolve safely;
            completionItem = completionItem with { CommitCharacters = null };

            // Act
            var newCompletionItem = (VSCompletionItem)await endpoint.Handle(completionItem, default);

            // Assert
            Assert.NotNull(newCompletionItem.Description);
        }

        [Fact]
        public async Task Handle_Resolve_TagHelperAttribute_ReturnsCompletionItemWithDocumentation()
        {
            // Arrange
            var lspDescriptionFactory = new Mock<LSPTagHelperTooltipFactory>(MockBehavior.Strict);
            var markdown = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Some Markdown"
            };
            lspDescriptionFactory.Setup(factory => factory.TryCreateTooltip(It.IsAny<AggregateBoundAttributeDescription>(), out markdown))
                .Returns(true);
            var endpoint = new RazorCompletionResolveEndpoint(lspDescriptionFactory.Object, VSLSPTagHelperTooltipFactory, CompletionListCache, LoggerFactory);
            endpoint.SetCapability(CompletionCapability, DefaultClientCapability);
            var razorCompletionItem = new RazorCompletionItem("TestItem", "TestItem", RazorCompletionItemKind.TagHelperAttribute);
            razorCompletionItem.SetAttributeCompletionDescription(new AggregateBoundAttributeDescription(Array.Empty<BoundAttributeDescriptionInfo>()));
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single();

            // Act
            var newCompletionItem = await endpoint.Handle(completionItem, default);

            // Assert
            Assert.NotNull(newCompletionItem.Documentation);
        }

        [Fact]
        public async Task Handle_Resolve_NonTagHelperCompletion_Noops()
        {
            // Arrange
            var lspDescriptionFactory = new Mock<LSPTagHelperTooltipFactory>(MockBehavior.Strict);
            var markdown = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = "Some Markdown"
            };
            lspDescriptionFactory.Setup(factory => factory.TryCreateTooltip(It.IsAny<AggregateBoundElementDescription>(), out markdown))
                .Returns(true);
            var endpoint = new RazorCompletionResolveEndpoint(LSPTagHelperTooltipFactory, VSLSPTagHelperTooltipFactory, CompletionListCache, LoggerFactory);
            endpoint.SetCapability(CompletionCapability, DefaultClientCapability);
            var completionItem = new CompletionItem();

            // Act
            var newCompletionItem = await endpoint.Handle(completionItem, default);

            // Assert
            Assert.Null(newCompletionItem.Documentation);
        }

        private CompletionList CreateLSPCompletionList(IReadOnlyList<RazorCompletionItem> razorCompletionItems)
        {
            var completionList = RazorCompletionEndpoint.CreateLSPCompletionList(razorCompletionItems, CompletionListCache, supportedItemKinds: null, completionCapability: null);
            return completionList;
        }
    }
}
