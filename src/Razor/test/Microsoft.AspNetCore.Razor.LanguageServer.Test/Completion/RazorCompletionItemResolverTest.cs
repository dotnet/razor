// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Tooltip;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.CodeAnalysis.Razor.Tooltip;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    public class RazorCompletionItemResolverTest : LanguageServerTestBase
    {
        public RazorCompletionItemResolverTest()
        {
            LSPTagHelperTooltipFactory = new DefaultLSPTagHelperTooltipFactory();
            VSLSPTagHelperTooltipFactory = new DefaultVSLSPTagHelperTooltipFactory();
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
            var attributeDescriptionInfo = new BoundAttributeDescriptionInfo("System.DateTime", "System.DateTime", "DateTime", "Returns the time.");
            AttributeDescription = new AggregateBoundAttributeDescription(new[] { attributeDescriptionInfo });
            var elementDescriptionInfo = new BoundElementDescriptionInfo("System.SomeTagHelper", "This is some TagHelper.");
            ElementDescription = new AggregateBoundElementDescription(new[] { elementDescriptionInfo });
        }

        private LSPTagHelperTooltipFactory LSPTagHelperTooltipFactory { get; }

        private VSLSPTagHelperTooltipFactory VSLSPTagHelperTooltipFactory { get; }

        private VSInternalCompletionSetting CompletionCapability { get; }

        private VSInternalClientCapabilities DefaultClientCapability { get; }

        private VSInternalClientCapabilities VSClientCapability { get; }

        private AggregateBoundAttributeDescription AttributeDescription { get; }

        private AggregateBoundElementDescription ElementDescription { get; }

        [Fact]
        public async Task ResolveAsync_DirectiveCompletion_ReturnsCompletionItemWithDocumentation()
        {
            // Arrange
            var resolver = new RazorCompletionItemResolver(LSPTagHelperTooltipFactory, VSLSPTagHelperTooltipFactory);
            var razorCompletionItem = new RazorCompletionItem("TestItem", "TestItem", RazorCompletionItemKind.Directive);
            razorCompletionItem.SetDirectiveCompletionDescription(new DirectiveCompletionDescription("Test directive"));
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single() as VSInternalCompletionItem;

            // Act
            var resolvedCompletionItem = await resolver.ResolveAsync(completionItem, completionList, new[] { razorCompletionItem }, DefaultClientCapability, CancellationToken.None);

            // Assert
            Assert.NotNull(resolvedCompletionItem.Documentation);
        }

        [Fact]
        public async Task ResolveAsync_MarkupTransitionCompletion_ReturnsCompletionItemWithDocumentation()
        {
            // Arrange
            var resolver = new RazorCompletionItemResolver(LSPTagHelperTooltipFactory, VSLSPTagHelperTooltipFactory);
            var razorCompletionItem = new RazorCompletionItem("@...", "@", RazorCompletionItemKind.MarkupTransition);
            razorCompletionItem.SetMarkupTransitionCompletionDescription(new MarkupTransitionCompletionDescription("Test description"));
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single() as VSInternalCompletionItem;

            // Act
            var resolvedCompletionItem = await resolver.ResolveAsync(completionItem, completionList, new[] { razorCompletionItem }, DefaultClientCapability, CancellationToken.None);

            // Assert
            Assert.NotNull(resolvedCompletionItem.Documentation);
        }

        [Fact]
        public async Task ResolveAsync_DirectiveAttributeCompletion_ReturnsCompletionItemWithDocumentation()
        {
            // Arrange
            var resolver = new RazorCompletionItemResolver(LSPTagHelperTooltipFactory, VSLSPTagHelperTooltipFactory);
            var razorCompletionItem = new RazorCompletionItem("TestItem", "TestItem", RazorCompletionItemKind.DirectiveAttribute);
            razorCompletionItem.SetAttributeCompletionDescription(AttributeDescription);
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single() as VSInternalCompletionItem;

            // Act
            var resolvedCompletionItem = await resolver.ResolveAsync(completionItem, completionList, new[] { razorCompletionItem }, DefaultClientCapability, CancellationToken.None);

            // Assert
            Assert.NotNull(resolvedCompletionItem.Documentation);
        }

        [Fact]
        public async Task ResolveAsync_DirectiveAttributeParameterCompletion_ReturnsCompletionItemWithDocumentation()
        {
            // Arrange
            var resolver = new RazorCompletionItemResolver(LSPTagHelperTooltipFactory, VSLSPTagHelperTooltipFactory);
            var razorCompletionItem = new RazorCompletionItem("TestItem", "TestItem", RazorCompletionItemKind.DirectiveAttributeParameter);
            razorCompletionItem.SetAttributeCompletionDescription(AttributeDescription);
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single() as VSInternalCompletionItem;

            // Act
            var resolvedCompletionItem = await resolver.ResolveAsync(completionItem, completionList, new[] { razorCompletionItem }, DefaultClientCapability, CancellationToken.None);

            // Assert
            Assert.NotNull(resolvedCompletionItem.Documentation);
        }

        [Fact]
        public async Task ResolveAsync_TagHelperElementCompletion_ReturnsCompletionItemWithDocumentation()
        {
            // Arrange
            var resolver = new RazorCompletionItemResolver(LSPTagHelperTooltipFactory, VSLSPTagHelperTooltipFactory);
            var razorCompletionItem = new RazorCompletionItem("TestItem", "TestItem", RazorCompletionItemKind.TagHelperElement);
            razorCompletionItem.SetTagHelperElementDescriptionInfo(ElementDescription);
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single() as VSInternalCompletionItem;

            // Act
            var resolvedCompletionItem = await resolver.ResolveAsync(completionItem, completionList, new[] { razorCompletionItem }, DefaultClientCapability, CancellationToken.None);

            // Assert
            Assert.NotNull(resolvedCompletionItem.Documentation);
        }

        [Fact]
        public async Task ResolveAsync_TagHelperAttribute_ReturnsCompletionItemWithDocumentation()
        {
            // Arrange
            var resolver = new RazorCompletionItemResolver(LSPTagHelperTooltipFactory, VSLSPTagHelperTooltipFactory);
            var razorCompletionItem = new RazorCompletionItem("TestItem", "TestItem", RazorCompletionItemKind.TagHelperAttribute);
            razorCompletionItem.SetAttributeCompletionDescription(AttributeDescription);
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single() as VSInternalCompletionItem;

            // Act
            var resolvedCompletionItem = await resolver.ResolveAsync(completionItem, completionList, new[] { razorCompletionItem }, DefaultClientCapability, CancellationToken.None);

            // Assert
            Assert.NotNull(resolvedCompletionItem.Documentation);
        }

        [Fact]
        public async Task ResolveAsync_VS_DirectiveAttributeCompletion_ReturnsCompletionItemWithDescription()
        {
            // Arrange
            var resolver = new RazorCompletionItemResolver(LSPTagHelperTooltipFactory, VSLSPTagHelperTooltipFactory);
            var razorCompletionItem = new RazorCompletionItem("TestItem", "TestItem", RazorCompletionItemKind.DirectiveAttribute);
            razorCompletionItem.SetAttributeCompletionDescription(AttributeDescription);
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single() as VSInternalCompletionItem;

            // Act
            var resolvedCompletionItem = await resolver.ResolveAsync(completionItem, completionList, new[] { razorCompletionItem }, VSClientCapability, CancellationToken.None);

            // Assert
            Assert.NotNull(resolvedCompletionItem.Description);
        }

        [Fact]
        public async Task ResolveAsync_VS_DirectiveAttributeParameterCompletion_ReturnsCompletionItemWithDescription()
        {
            // Arrange
            var resolver = new RazorCompletionItemResolver(LSPTagHelperTooltipFactory, VSLSPTagHelperTooltipFactory);
            var razorCompletionItem = new RazorCompletionItem("TestItem", "TestItem", RazorCompletionItemKind.DirectiveAttributeParameter);
            razorCompletionItem.SetAttributeCompletionDescription(AttributeDescription);
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single() as VSInternalCompletionItem;

            // Act
            var resolvedCompletionItem = await resolver.ResolveAsync(completionItem, completionList, new[] { razorCompletionItem }, VSClientCapability, CancellationToken.None);

            // Assert
            Assert.NotNull(resolvedCompletionItem.Description);
        }

        [Fact]
        public async Task ResolveAsync_VS_TagHelperElementCompletion_ReturnsCompletionItemWithDescription()
        {
            // Arrange
            var resolver = new RazorCompletionItemResolver(LSPTagHelperTooltipFactory, VSLSPTagHelperTooltipFactory);
            var razorCompletionItem = new RazorCompletionItem("TestItem", "TestItem", RazorCompletionItemKind.TagHelperElement);
            razorCompletionItem.SetTagHelperElementDescriptionInfo(ElementDescription);
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single() as VSInternalCompletionItem;

            // Act
            var resolvedCompletionItem = await resolver.ResolveAsync(completionItem, completionList, new[] { razorCompletionItem }, VSClientCapability, CancellationToken.None);

            // Assert
            Assert.NotNull(resolvedCompletionItem.Description);
        }

        [Fact]
        public async Task ResolveAsync_VS_TagHelperAttribute_ReturnsCompletionItemWithDescription()
        {
            // Arrange
            var resolver = new RazorCompletionItemResolver(LSPTagHelperTooltipFactory, VSLSPTagHelperTooltipFactory);
            var razorCompletionItem = new RazorCompletionItem("TestItem", "TestItem", RazorCompletionItemKind.TagHelperAttribute);
            razorCompletionItem.SetAttributeCompletionDescription(AttributeDescription);
            var completionList = CreateLSPCompletionList(new[] { razorCompletionItem });
            var completionItem = completionList.Items.Single() as VSInternalCompletionItem;

            // Act
            var resolvedCompletionItem = await resolver.ResolveAsync(completionItem, completionList, new[] { razorCompletionItem }, VSClientCapability, CancellationToken.None);

            // Assert
            Assert.NotNull(resolvedCompletionItem.Description);
        }

        [Fact]
        public async Task ResolveAsync_NonTagHelperCompletion_Noops()
        {
            // Arrange
            var resolver = new RazorCompletionItemResolver(LSPTagHelperTooltipFactory, VSLSPTagHelperTooltipFactory);
            var completionItem = new VSInternalCompletionItem();
            var completionList = new VSInternalCompletionList() { Items = new[] { completionItem } };

            // Act
            var resolvedCompletionItem = await resolver.ResolveAsync(completionItem, completionList, Array.Empty<RazorCompletionItem>(), DefaultClientCapability, CancellationToken.None);

            // Assert
            Assert.Null(resolvedCompletionItem);
        }

        private VSInternalCompletionList CreateLSPCompletionList(IReadOnlyList<RazorCompletionItem> razorCompletionItems)
        {
            var completionList = RazorCompletionListProvider.CreateLSPCompletionList(razorCompletionItems, DefaultClientCapability);
            return completionList;
        }
    }
}
