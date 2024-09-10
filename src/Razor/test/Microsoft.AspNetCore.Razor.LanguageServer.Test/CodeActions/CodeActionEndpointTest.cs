﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.CodeActions;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;

public class CodeActionEndpointTest(ITestOutputHelper testOutput) : LanguageServerTestBase(testOutput)
{
    private static readonly LinePositionSpan s_defaultRange = new(new(5, 2), new(5, 2));

    [Fact]
    public async Task Handle_NoDocument()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/Page.razor");
        var codeActionEndpoint = CreateEndpoint();

        var request = new VSCodeActionParams()
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = documentPath },
            Range = VsLspFactory.CreateZeroWidthRange(0, 1),
            Context = new VSInternalCodeActionContext()
        };

        var requestContext = CreateRazorRequestContext(documentContext: null);

        // Act
        var commandOrCodeActionContainer = await codeActionEndpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.Null(commandOrCodeActionContainer);
    }

    [Fact]
    public async Task Handle_UnsupportedDocument()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/Page.razor");
        var codeDocument = CreateCodeDocument("@code {}");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);
        codeDocument.SetUnsupported();
        var codeActionEndpoint = CreateEndpoint();

        var request = new VSCodeActionParams()
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = documentPath },
            Range = VsLspFactory.CreateZeroWidthRange(0, 1),
            Context = new VSInternalCodeActionContext()
        };

        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var commandOrCodeActionContainer = await codeActionEndpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.Null(commandOrCodeActionContainer);
    }

    [Fact]
    public async Task Handle_NoProviders()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/Page.razor");
        var codeDocument = CreateCodeDocument("@code {}");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);
        var codeActionEndpoint = CreateEndpoint();

        var request = new VSCodeActionParams()
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = documentPath },
            Range = VsLspFactory.CreateZeroWidthRange(0, 1),
            Context = new VSInternalCodeActionContext()
        };

        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var commandOrCodeActionContainer = await codeActionEndpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(commandOrCodeActionContainer);
        Assert.Empty(commandOrCodeActionContainer);
    }

    [Fact]
    public async Task Handle_OneRazorCodeActionProvider()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/Page.razor");
        var codeDocument = CreateCodeDocument("@code {}");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);
        var codeActionEndpoint = CreateEndpoint(razorCodeActionProviders: [CreateRazorCodeActionProvider()]);

        var request = new VSCodeActionParams()
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = documentPath },
            Range = VsLspFactory.CreateZeroWidthRange(0, 1),
            Context = new VSInternalCodeActionContext()
        };

        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var commandOrCodeActionContainer = await codeActionEndpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(commandOrCodeActionContainer);
        Assert.Single(commandOrCodeActionContainer);
    }

    [Fact]
    public async Task Handle_OneCSharpCodeActionProvider()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/Page.razor");
        var codeDocument = CreateCodeDocument("@code {}");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);

        var codeActionEndpoint = CreateEndpoint(
            documentMappingService: CreateDocumentMappingService(s_defaultRange),
            csharpCodeActionProviders: [CreateCSharpCodeActionProvider()],
            clientConnection: TestClientConnection.Instance);

        var request = new VSCodeActionParams()
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = documentPath },
            Range = VsLspFactory.CreateZeroWidthRange(0, 1),
            Context = new VSInternalCodeActionContext()
        };

        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var commandOrCodeActionContainer = await codeActionEndpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(commandOrCodeActionContainer);
        Assert.Single(commandOrCodeActionContainer);
    }

    [Fact]
    public async Task Handle_OneCodeActionProviderWithMultipleCodeActions()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/Page.razor");
        var codeDocument = CreateCodeDocument("@code {}");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);
        var codeActionEndpoint = CreateEndpoint(razorCodeActionProviders: [CreateMultipleRazorCodeActionProvider()]);

        var request = new VSCodeActionParams()
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = documentPath },
            Range = VsLspFactory.CreateZeroWidthRange(0, 1),
            Context = new VSInternalCodeActionContext()
        };

        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var commandOrCodeActionContainer = await codeActionEndpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(commandOrCodeActionContainer);
        Assert.Equal(2, commandOrCodeActionContainer.Length);
    }

    [Fact]
    public async Task Handle_MultipleCodeActionProvidersWithMultipleCodeActions()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/Page.razor");
        var codeDocument = CreateCodeDocument("@code {}");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);

        var codeActionEndpoint = CreateEndpoint(
            documentMappingService: CreateDocumentMappingService(s_defaultRange),
            razorCodeActionProviders: [
                CreateMultipleRazorCodeActionProvider(),
                CreateMultipleRazorCodeActionProvider(),
                CreateRazorCodeActionProvider()],
            csharpCodeActionProviders: [
                CreateCSharpCodeActionProvider(),
                CreateCSharpCodeActionProvider()],
            clientConnection: TestClientConnection.Instance);

        var request = new VSCodeActionParams()
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = documentPath },
            Range = VsLspFactory.CreateZeroWidthRange(0, 1),
            Context = new VSInternalCodeActionContext()
        };
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var commandOrCodeActionContainer = await codeActionEndpoint.HandleRequestAsync(request, requestContext, default);

        // Assert
        Assert.NotNull(commandOrCodeActionContainer);
        Assert.Equal(7, commandOrCodeActionContainer.Length);
    }

    [Fact]
    public async Task Handle_MultipleProviders()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/Page.razor");
        var codeDocument = CreateCodeDocument("@code {}");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);

        var codeActionEndpoint = CreateEndpoint(
            documentMappingService: CreateDocumentMappingService(s_defaultRange),
            razorCodeActionProviders: [
                CreateRazorCodeActionProvider(),
                CreateRazorCodeActionProvider(),
                CreateRazorCodeActionProvider()],
            csharpCodeActionProviders: [
                CreateCSharpCodeActionProvider(),
                CreateCSharpCodeActionProvider()],
            clientConnection: TestClientConnection.Instance);

        var request = new VSCodeActionParams()
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = documentPath },
            Range = VsLspFactory.CreateZeroWidthRange(0, 1),
            Context = new VSInternalCodeActionContext()
        };

        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var commandOrCodeActionContainer = await codeActionEndpoint.HandleRequestAsync(request, requestContext, default);

        // Assert
        Assert.NotNull(commandOrCodeActionContainer);
        Assert.Equal(5, commandOrCodeActionContainer.Length);
    }

    [Fact]
    public async Task Handle_OneNullReturningProvider()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/Page.razor");
        var codeDocument = CreateCodeDocument("@code {}");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);
        var codeActionEndpoint = CreateEndpoint(razorCodeActionProviders: [CreateEmptyRazorCodeActionProvider()]);

        var request = new VSCodeActionParams()
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = documentPath },
            Range = VsLspFactory.CreateZeroWidthRange(0, 1),
            Context = new VSInternalCodeActionContext()
        };

        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var commandOrCodeActionContainer = await codeActionEndpoint.HandleRequestAsync(request, requestContext, default);

        // Assert
        Assert.NotNull(commandOrCodeActionContainer);
        Assert.Empty(commandOrCodeActionContainer);
    }

    [Fact]
    public async Task Handle_MultipleMixedProvider()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/Page.razor");
        var codeDocument = CreateCodeDocument("@code {}");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);

        var codeActionEndpoint = CreateEndpoint(
            documentMappingService: CreateDocumentMappingService(s_defaultRange),
            razorCodeActionProviders: [
                CreateRazorCodeActionProvider(),
                CreateEmptyRazorCodeActionProvider(),
                CreateRazorCodeActionProvider(),
                CreateEmptyRazorCodeActionProvider()],
            csharpCodeActionProviders: [
                CreateCSharpCodeActionProvider(),
                CreateCSharpCodeActionProvider()],
            clientConnection: TestClientConnection.Instance);

        var request = new VSCodeActionParams()
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = documentPath },
            Range = VsLspFactory.CreateZeroWidthRange(0, 1),
            Context = new VSInternalCodeActionContext()
        };

        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var commandOrCodeActionContainer = await codeActionEndpoint.HandleRequestAsync(request, requestContext, default);

        // Assert
        Assert.NotNull(commandOrCodeActionContainer);
        Assert.Equal(4, commandOrCodeActionContainer.Length);
    }

    [Fact]
    public async Task Handle_MultipleMixedProvider_SupportsCodeActionResolveTrue()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/Page.razor");
        var codeDocument = CreateCodeDocument("@code {}");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);

        var codeActionEndpoint = CreateEndpoint(
            razorCodeActionProviders: [
                CreateRazorCodeActionProvider(),
                CreateRazorCommandProvider(),
                CreateEmptyRazorCodeActionProvider()],
            supportsCodeActionResolve: true);

        var request = new VSCodeActionParams()
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = documentPath },
            Range = VsLspFactory.CreateZeroWidthRange(0, 1),
            Context = new VSInternalCodeActionContext()
        };

        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var commandOrCodeActionContainer = await codeActionEndpoint.HandleRequestAsync(request, requestContext, default);

        // Assert
        Assert.NotNull(commandOrCodeActionContainer);
        Assert.Collection(commandOrCodeActionContainer,
            c =>
            {
                Assert.True(c.TryGetSecond(out var codeAction));
                Assert.True(codeAction is VSInternalCodeAction);
            },
            c =>
            {
                Assert.True(c.TryGetSecond(out var codeAction));
                Assert.True(codeAction is VSInternalCodeAction);
            });
    }

    [Fact]
    public async Task Handle_MixedProvider_SupportsCodeActionResolveTrue_UsesGroups()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/Page.razor");
        var codeDocument = CreateCodeDocument("@code {}");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);

        var codeActionEndpoint = CreateEndpoint(
            documentMappingService: CreateDocumentMappingService(s_defaultRange),
            razorCodeActionProviders: [CreateRazorCodeActionProvider()],
            csharpCodeActionProviders: [CreateCSharpCodeActionProvider()],
            clientConnection: TestClientConnection.Instance,
            supportsCodeActionResolve: true);

        var request = new VSCodeActionParams()
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = documentPath },
            Range = VsLspFactory.CreateZeroWidthRange(0, 1),
            Context = new VSInternalCodeActionContext()
        };

        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var commandOrCodeActionContainer = await codeActionEndpoint.HandleRequestAsync(request, requestContext, default);

        // Assert
        Assert.NotNull(commandOrCodeActionContainer);
        Assert.Collection(commandOrCodeActionContainer,
            c =>
            {
                Assert.True(c.TryGetSecond(out var codeAction));
                Assert.True(codeAction is VSInternalCodeAction);
                Assert.Equal("A-Razor", ((VSInternalCodeAction)codeAction).Group);
            },
            c =>
            {
                Assert.True(c.TryGetSecond(out var codeAction));
                Assert.True(codeAction is VSInternalCodeAction);
                Assert.Equal("B-Delegated", ((VSInternalCodeAction)codeAction).Group);
            });
    }

    [Fact]
    public async Task Handle_MultipleMixedProvider_SupportsCodeActionResolveFalse()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/Page.razor");
        var codeDocument = CreateCodeDocument("@code {}");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);

        var codeActionEndpoint = CreateEndpoint(
            razorCodeActionProviders: [
                CreateRazorCodeActionProvider(),
                CreateRazorCommandProvider(),
                CreateEmptyRazorCodeActionProvider()]);

        var request = new VSCodeActionParams()
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = documentPath },
            Range = VsLspFactory.CreateZeroWidthRange(0, 1),
            Context = new VSInternalCodeActionContext()
        };

        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var commandOrCodeActionContainer = await codeActionEndpoint.HandleRequestAsync(request, requestContext, default);

        // Assert
        Assert.NotNull(commandOrCodeActionContainer);
        Assert.Collection(commandOrCodeActionContainer,
            c =>
            {
                Assert.True(c.TryGetFirst(out var first));
                var command = Assert.IsType<Command>(first);
                Assert.NotNull(command.Arguments);
                var codeActionParamsToken = (JsonObject)command.Arguments.First();
                var codeActionParams = codeActionParamsToken.Deserialize<RazorCodeActionResolutionParams>();
                Assert.NotNull(codeActionParams);
                Assert.Equal(LanguageServerConstants.CodeActions.EditBasedCodeActionCommand, codeActionParams.Action);
            },
            c => Assert.True(c.TryGetFirst(out _)));
    }

    [Fact]
    public async Task GenerateRazorCodeActionContextAsync_WithSelectionRange()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/Page.razor");
        var codeDocument = CreateCodeDocument("@code {}");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);
        var codeActionEndpoint = CreateEndpoint(razorCodeActionProviders: [CreateRazorCodeActionProvider()]);

        var initialRange = VsLspFactory.CreateZeroWidthRange(0, 1);
        var selectionRange = VsLspFactory.CreateZeroWidthRange(0, 5);
        var request = new VSCodeActionParams()
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = documentPath },
            Range = initialRange,
            Context = new VSInternalCodeActionContext()
            {
                SelectionRange = selectionRange,
            }
        };

        // Act
        var razorCodeActionContext = await codeActionEndpoint.GenerateRazorCodeActionContextAsync(request, documentContext.Snapshot);

        // Assert
        Assert.NotNull(razorCodeActionContext);
        Assert.Equal(selectionRange, razorCodeActionContext.Request.Range);
    }

    [Fact]
    public async Task GenerateRazorCodeActionContextAsync_WithoutSelectionRange()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/Page.razor");
        var codeDocument = CreateCodeDocument("@code {}");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);
        var codeActionEndpoint = CreateEndpoint(razorCodeActionProviders: [CreateRazorCodeActionProvider()]);

        var initialRange = VsLspFactory.CreateZeroWidthRange(0, 1);
        var request = new VSCodeActionParams()
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = documentPath },
            Range = initialRange,
            Context = new VSInternalCodeActionContext()
            {
                SelectionRange = null
            }
        };

        // Act
        var razorCodeActionContext = await codeActionEndpoint.GenerateRazorCodeActionContextAsync(request, documentContext.Snapshot);

        // Assert
        Assert.NotNull(razorCodeActionContext);
        Assert.Equal(initialRange, razorCodeActionContext.Request.Range);
    }

    [Fact]
    public async Task GetCSharpCodeActionsFromLanguageServerAsync_InvalidRangeMapping()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/Page.razor");
        var codeDocument = CreateCodeDocument("@code {}");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);

        var codeActionEndpoint = CreateEndpoint(csharpCodeActionProviders: [CreateCSharpCodeActionProvider()]);

        var initialRange = VsLspFactory.CreateZeroWidthRange(0, 1);
        var request = new VSCodeActionParams()
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = documentPath },
            Range = initialRange,
            Context = new VSInternalCodeActionContext()
        };

        var context = await codeActionEndpoint.GenerateRazorCodeActionContextAsync(request, documentContext.Snapshot);
        Assert.NotNull(context);

        // Act
        var results = await codeActionEndpoint.GetCodeActionsFromLanguageServerAsync(RazorLanguageKind.CSharp, documentContext, context, Guid.Empty, cancellationToken: default);

        // Assert
        Assert.Empty(results);
        Assert.Equal(initialRange, context.Request.Range);
    }

    [Fact]
    public async Task GetCSharpCodeActionsFromLanguageServerAsync_ReturnsCodeActions()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/Page.razor");
        var codeDocument = CreateCodeDocument("@code {}");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);
        var projectedRange = VsLspFactory.CreateZeroWidthRange(15, 2);

        var codeActionEndpoint = CreateEndpoint(
            documentMappingService: CreateDocumentMappingService(projectedRange.ToLinePositionSpan()),
            csharpCodeActionProviders: [CreateCSharpCodeActionProvider()],
            clientConnection: TestClientConnection.Instance);

        var initialRange = VsLspFactory.CreateZeroWidthRange(0, 1);
        var request = new VSCodeActionParams()
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = documentPath },
            Range = initialRange,
            Context = new VSInternalCodeActionContext()
            {
                SelectionRange = initialRange
            }
        };

        var context = await codeActionEndpoint.GenerateRazorCodeActionContextAsync(request, documentContext.Snapshot);
        Assert.NotNull(context);

        // Act
        var results = await codeActionEndpoint.GetCodeActionsFromLanguageServerAsync(RazorLanguageKind.CSharp, documentContext, context, Guid.Empty, cancellationToken: DisposalToken);

        // Assert
        var result = Assert.Single(results);
        Assert.NotNull(result.Diagnostics);
        var diagnostics = result.Diagnostics.ToArray();
        Assert.Equal(2, diagnostics.Length);

        // Diagnostic ranges contain the projected range for
        // 1. Range
        // 2. SelectionRange
        //
        // This helps verify that the CodeActionEndpoint is mapping the
        // ranges correctly using the mapping service
        Assert.Equal(projectedRange, diagnostics[0].Range);
        Assert.Equal(projectedRange, diagnostics[1].Range);
    }

    private CodeActionEndpoint CreateEndpoint(
        IDocumentMappingService? documentMappingService = null,
        ImmutableArray<IRazorCodeActionProvider> razorCodeActionProviders = default,
        ImmutableArray<ICSharpCodeActionProvider> csharpCodeActionProviders = default,
        ImmutableArray<IHtmlCodeActionProvider> htmlCodeActionProviders = default,
        IClientConnection? clientConnection = null,
        LanguageServerFeatureOptions? languageServerFeatureOptions = null,
        bool supportsCodeActionResolve = false)
    {
        return new CodeActionEndpoint(
            documentMappingService ?? CreateDocumentMappingService(),
            razorCodeActionProviders.NullToEmpty(),
            csharpCodeActionProviders.NullToEmpty(),
            htmlCodeActionProviders.NullToEmpty(),
            clientConnection ?? StrictMock.Of<IClientConnection>(),
            languageServerFeatureOptions ?? StrictMock.Of<LanguageServerFeatureOptions>(x => x.SupportsFileManipulation == true),
            LoggerFactory,
            telemetryReporter: null)
        {
            _supportsCodeActionResolve = supportsCodeActionResolve
        };
    }

    private static IDocumentMappingService CreateDocumentMappingService(LinePositionSpan? projectedRange = null)
    {
        var mock = new StrictMock<IDocumentMappingService>();

        // If a range was provided, use that and return true; otherwise, return false.
        var (outRange, result) = projectedRange is LinePositionSpan
            ? (projectedRange.GetValueOrDefault(), true)
            : (It.Ref<LinePositionSpan>.IsAny, false);

        mock.Setup(x => x.TryMapToGeneratedDocumentRange(It.IsAny<IRazorGeneratedDocument>(), It.IsAny<LinePositionSpan>(), out outRange))
            .Returns(result);

        return mock.Object;
    }

    private static RazorCodeDocument CreateCodeDocument(string text)
    {
        var sourceDocument = TestRazorSourceDocument.Create(text);
        var projectEngine = RazorProjectEngine.Create(builder => { });

        return projectEngine.ProcessDesignTime(sourceDocument, "mvc", importSources: [], tagHelpers: []);
    }

    private static IRazorCodeActionProvider CreateEmptyRazorCodeActionProvider()
        => CreateRazorCodeActionProvider([]);

    private static IRazorCodeActionProvider CreateRazorCodeActionProvider()
        => CreateRazorCodeActionProvider(new RazorVSInternalCodeAction());

    private static IRazorCodeActionProvider CreateMultipleRazorCodeActionProvider()
        => CreateRazorCodeActionProvider(
            new RazorVSInternalCodeAction(),
            new RazorVSInternalCodeAction());

    private static IRazorCodeActionProvider CreateRazorCommandProvider()
        => CreateRazorCodeActionProvider(
            new RazorVSInternalCodeAction()
            {
                Title = "SomeTitle",
                Data = JsonSerializer.SerializeToElement(new AddUsingsCodeActionParams()
                {
                    Namespace = "Test",
                    Uri = new Uri("C:/path/to/Page.razor")
                })
            });

    private static IRazorCodeActionProvider CreateRazorCodeActionProvider(params ImmutableArray<RazorVSInternalCodeAction> codeActions)
    {
        var mock = new StrictMock<IRazorCodeActionProvider>();

        mock.Setup(x => x.ProvideAsync(It.IsAny<RazorCodeActionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => codeActions);

        return mock.Object;
    }

    private static ICSharpCodeActionProvider CreateCSharpCodeActionProvider()
        => CreateCSharpCodeActionProvider([new RazorVSInternalCodeAction()]);

    private static ICSharpCodeActionProvider CreateCSharpCodeActionProvider(params ImmutableArray<RazorVSInternalCodeAction> codeActions)
    {
        var mock = new StrictMock<ICSharpCodeActionProvider>();

        mock.Setup(x => x.ProvideAsync(It.IsAny<RazorCodeActionContext>(), It.IsAny<ImmutableArray<RazorVSInternalCodeAction>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => codeActions);

        return mock.Object;
    }

    private sealed class TestClientConnection : IClientConnection
    {
        public static readonly IClientConnection Instance = new TestClientConnection();

        private static readonly string[] s_customTags = ["CodeActionName"];

        private TestClientConnection()
        {
        }

        public Task SendNotificationAsync<TParams>(string method, TParams @params, CancellationToken cancellationToken)
        {
            Assert.Equal(CustomMessageNames.RazorProvideCodeActionsEndpoint, method);

            return Task.CompletedTask;
        }

        public Task SendNotificationAsync(string method, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<TResponse> SendRequestAsync<TParams, TResponse>(string method, TParams @params, CancellationToken cancellationToken)
        {
            Assert.Equal(CustomMessageNames.RazorProvideCodeActionsEndpoint, method);

            Assert.NotNull(@params);
            var delegatedCodeActionParams = Assert.IsType<DelegatedCodeActionParams>(@params);

            Assert.NotNull(delegatedCodeActionParams.CodeActionParams);
            Assert.NotNull(delegatedCodeActionParams.CodeActionParams.Context);

            var diagnostics = new List<Diagnostic>
            {
                new()
                {
                    Range = delegatedCodeActionParams.CodeActionParams.Range,
                    Message = "Range"
                }
            };

            if (delegatedCodeActionParams.CodeActionParams.Context.SelectionRange is { } selectionRange)
            {
                diagnostics.Add(new()
                {
                    Range = selectionRange,
                    Message = "Selection Range"
                });
            }

            // Create a code action specifically with diagnostics that
            // contain the contextual information for it's creation. This is
            // a hacky way to verify that data transmitted to the language server
            // is correct rather than providing specific test hooks in the CodeActionEndpoint
            var result = new[]
            {
                new RazorVSInternalCodeAction()
                {
                    Data = JsonSerializer.SerializeToElement(new { CustomTags = s_customTags }),
                    Diagnostics = [.. diagnostics]
                }
            };

            return Task.FromResult((TResponse)(object)result);
        }
    }
}
