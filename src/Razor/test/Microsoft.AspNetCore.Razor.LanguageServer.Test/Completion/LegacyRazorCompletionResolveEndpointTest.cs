// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Tooltip;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.CodeAnalysis.Razor.Tooltip;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    public class LegacyRazorCompletionResolveEndpointTest : LanguageServerTestBase
    {
        public LegacyRazorCompletionResolveEndpointTest()
        {
            LSPTagHelperTooltipFactory = Mock.Of<LSPTagHelperTooltipFactory>(MockBehavior.Strict);
            VSLSPTagHelperTooltipFactory = Mock.Of<VSLSPTagHelperTooltipFactory>(MockBehavior.Strict);
            CompletionListCache = new CompletionListCache();
            CompletionCapability = new VSInternalCompletionSetting()
            {
                CompletionItem = new CompletionItemSetting()
                {
                    DocumentationFormat = new[] { MarkupKind.PlainText, MarkupKind.Markdown },
                }
            };
            DefaultClientCapability = new VSInternalClientCapabilities()
            {
                TextDocument = new TextDocumentClientCapabilities()
                {
                    Completion = CompletionCapability,
                },
            };
            VSClientCapability = new VSInternalClientCapabilities()
            {
                TextDocument = new TextDocumentClientCapabilities()
                {
                    Completion = CompletionCapability,
                },
                SupportsVisualStudioExtensions = true,
            };
        }

        private LSPTagHelperTooltipFactory LSPTagHelperTooltipFactory { get; }

        private VSLSPTagHelperTooltipFactory VSLSPTagHelperTooltipFactory { get; }

        private CompletionListCache CompletionListCache { get; }

        private VSInternalCompletionSetting CompletionCapability { get; }

        private VSInternalClientCapabilities DefaultClientCapability { get; }

        private VSInternalClientCapabilities VSClientCapability { get; }

        [Fact]
        public async Task Handle_Resolve_DirectiveCompletion_ReturnsCompletionItemWithDocumentation()
        {
            // Arrange
            var endpoint = new LegacyRazorCompletionResolveEndpoint(LSPTagHelperTooltipFactory, VSLSPTagHelperTooltipFactory, CompletionListCache, LoggerFactory);
            endpoint.GetRegistration(DefaultClientCapability);
            var razorCompletionItem = new RazorCompletionItem("TestItem", "TestItem", RazorCompletionItemKind.Directive);
            razorCompletionItem.SetDirectiveCompletionDescription(new DirectiveCompletionDescription("Test directive"));
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single();
            var parameters = ConvertToBridgedItem(completionItem);

            // Act
            var newCompletionItem = await endpoint.Handle(parameters, default);

            // Assert
            Assert.NotNull(newCompletionItem.Documentation);
        }

        [Fact]
        public async Task Handle_Resolve_MarkupTransitionCompletion_ReturnsCompletionItemWithDocumentation()
        {
            // Arrange
            var endpoint = new LegacyRazorCompletionResolveEndpoint(LSPTagHelperTooltipFactory, VSLSPTagHelperTooltipFactory, CompletionListCache, LoggerFactory);
            endpoint.GetRegistration(DefaultClientCapability);
            var razorCompletionItem = new RazorCompletionItem("@...", "@", RazorCompletionItemKind.MarkupTransition);
            razorCompletionItem.SetMarkupTransitionCompletionDescription(new MarkupTransitionCompletionDescription("Test description"));
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single();
            var parameters = ConvertToBridgedItem(completionItem);

            // Act
            var newCompletionItem = await endpoint.Handle(parameters, default);

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
            lspDescriptionFactory.Setup(factory => factory.TryCreateTooltip(It.IsAny<AggregateBoundAttributeDescription>(), MarkupKind.Markdown, out markdown))
                .Returns(true);
            var endpoint = new LegacyRazorCompletionResolveEndpoint(lspDescriptionFactory.Object, VSLSPTagHelperTooltipFactory, CompletionListCache, LoggerFactory);
            endpoint.GetRegistration(DefaultClientCapability);
            var razorCompletionItem = new RazorCompletionItem("TestItem", "TestItem", RazorCompletionItemKind.DirectiveAttribute);
            razorCompletionItem.SetAttributeCompletionDescription(new AggregateBoundAttributeDescription(Array.Empty<BoundAttributeDescriptionInfo>()));
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single();
            var parameters = ConvertToBridgedItem(completionItem);

            // Act
            var newCompletionItem = await endpoint.Handle(parameters, default);

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
            descriptionFactory.Setup(factory => factory.TryCreateTooltip(It.IsAny<AggregateBoundAttributeDescription>(), MarkupKind.Markdown, out markdown))
                .Returns(true);
            var endpoint = new LegacyRazorCompletionResolveEndpoint(descriptionFactory.Object, VSLSPTagHelperTooltipFactory, CompletionListCache, LoggerFactory);
            endpoint.GetRegistration(DefaultClientCapability);
            var razorCompletionItem = new RazorCompletionItem("TestItem", "TestItem", RazorCompletionItemKind.DirectiveAttributeParameter);
            razorCompletionItem.SetAttributeCompletionDescription(new AggregateBoundAttributeDescription(Array.Empty<BoundAttributeDescriptionInfo>()));
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single();
            var parameters = ConvertToBridgedItem(completionItem);

            // Act
            var newCompletionItem = await endpoint.Handle(parameters, default);

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
            lspDescriptionFactory.Setup(factory => factory.TryCreateTooltip(It.IsAny<AggregateBoundElementDescription>(), MarkupKind.Markdown, out markdown))
                .Returns(true);
            var endpoint = new LegacyRazorCompletionResolveEndpoint(lspDescriptionFactory.Object, VSLSPTagHelperTooltipFactory, CompletionListCache, LoggerFactory);
            endpoint.GetRegistration(DefaultClientCapability);
            var razorCompletionItem = new RazorCompletionItem("TestItem", "TestItem", RazorCompletionItemKind.TagHelperElement);
            razorCompletionItem.SetTagHelperElementDescriptionInfo(new AggregateBoundElementDescription(Array.Empty<BoundElementDescriptionInfo>()));
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single();
            var parameters = ConvertToBridgedItem(completionItem);

            // Act
            var newCompletionItem = await endpoint.Handle(parameters, default);

            // Assert
            Assert.NotNull(newCompletionItem.Documentation);
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
            lspDescriptionFactory.Setup(factory => factory.TryCreateTooltip(It.IsAny<AggregateBoundAttributeDescription>(), MarkupKind.Markdown, out markdown))
                .Returns(true);
            var endpoint = new LegacyRazorCompletionResolveEndpoint(lspDescriptionFactory.Object, VSLSPTagHelperTooltipFactory, CompletionListCache, LoggerFactory);
            endpoint.GetRegistration(DefaultClientCapability);
            var razorCompletionItem = new RazorCompletionItem("TestItem", "TestItem", RazorCompletionItemKind.TagHelperAttribute);
            razorCompletionItem.SetAttributeCompletionDescription(new AggregateBoundAttributeDescription(Array.Empty<BoundAttributeDescriptionInfo>()));
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single();
            var parameters = ConvertToBridgedItem(completionItem);

            // Act
            var newCompletionItem = await endpoint.Handle(parameters, default);

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
            lspDescriptionFactory.Setup(factory => factory.TryCreateTooltip(It.IsAny<AggregateBoundElementDescription>(), MarkupKind.Markdown, out markdown))
                .Returns(true);
            var endpoint = new LegacyRazorCompletionResolveEndpoint(LSPTagHelperTooltipFactory, VSLSPTagHelperTooltipFactory, CompletionListCache, LoggerFactory);
            endpoint.GetRegistration(DefaultClientCapability);
            var completionItem = new CompletionItem();
            var parameters = ConvertToBridgedItem(completionItem);

            // Act
            var newCompletionItem = await endpoint.Handle(parameters, default);

            // Assert
            Assert.Null(newCompletionItem.Documentation);
        }

        private VSInternalCompletionList CreateLSPCompletionList(IReadOnlyList<RazorCompletionItem> razorCompletionItems)
        {
            var completionList = LegacyRazorCompletionEndpoint.CreateLSPCompletionList(razorCompletionItems, CompletionListCache, DefaultClientCapability);
            return completionList;
        }

        private VSCompletionItemBridge ConvertToBridgedItem(CompletionItem completionItem)
        {
            var serialized = Serializer.SerializeObject(completionItem);
            var bridgedItem = Serializer.DeserializeObject<VSCompletionItemBridge>(serialized);
            return bridgedItem;
        }
    }
}
