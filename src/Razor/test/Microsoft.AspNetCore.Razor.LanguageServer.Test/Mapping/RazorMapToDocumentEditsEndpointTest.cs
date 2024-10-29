// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Mapping;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.DocumentMapping;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.Mapping;

public class RazorMapToDocumentEditsEndpointTest : LanguageServerTestBase
{
    private readonly IDocumentMappingService _documentMappingService;

    public RazorMapToDocumentEditsEndpointTest(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
        _documentMappingService = new LspDocumentMappingService(
            FilePathService,
            new TestDocumentContextFactory(),
            LoggerFactory);
    }

    [Fact]
    public Task SimpleEdits_Apply()
        => TestAsync(
            """
            class MyComponent : ComponentBase
            {
                {|map1:public int Counter { get; set; }|}
            }
            """,
            """
            @code {
                {|map1:public int Counter { get; set; }|} 
            }
            """,
            """
            class MyComponent : ComponentBase
            {
                public int NewCounter { get; set; }
            }
            """,
            """
            @code {
                public int NewCounter { get; set; } 
            }
            """);

    [Fact]
    public Task MappedAndUnmappedEdits_Apply()
        => TestAsync(
            """
            class MyComponent : ComponentBase
            {
                {|map1:public int Counter { get; set; }|}

                void UnmappedMethod()
                {
                }
            }
            """,
            """
            @code {
                {|map1:public int Counter { get; set; }|} 
            }
            """,
            """
            class MyComponent : ComponentBase
            {
                public int NewCounter { get; set; }

                void Method()
                {
                }
            }
            """,
            """
            @code {
                public int NewCounter { get; set; } 
            }
            """);

    [Fact]
    public Task NewUsing_TopOfFile()
        => TestAsync(
            """
            class MyComponent : ComponentBase
            {
                {|map1:public int Counter { get; set; }|}
            }
            """,
            """
            @code {
                {|map1:public int Counter { get; set; }|} 
            }
            """,
            """
            using System;

            class MyComponent : ComponentBase
            {
                public int NewCounter { get; set; }
            }
            """,
            """
            @using System
            @code {
                public int NewCounter { get; set; } 
            }
            """);

    [Fact]
    public Task RemovedUsing_IsRemoved()
    => TestAsync(
        """
        {|mapUsing:using System;|}

        class MyComponent : ComponentBase
        {
            {|map1:public int Counter { get; set; }|}
        }
        """,
        """
        {|mapUsing:@using System|}

        @code {
            {|map1:public int Counter { get; set; }|} 
        }
        """,
        """
        class MyComponent : ComponentBase
        {
            public int Counter { get; set; }
        }
        """,
        """


        @code {
            public int Counter { get; set; } 
        }
        """);

    [Fact]
    public Task NewUsing_AfterExisting()
        => TestAsync(
        """
        {|mapUsing:using System;|}

        class MyComponent : ComponentBase
        {
            {|map1:public int Counter { get; set; }|}
        }
        """,
        """
        {|mapUsing:@using System|}

        @code {
            {|map1:public int Counter { get; set; }|} 
        }
        """,
        """
        using System;
        using System.Collections.Generic;

        class MyComponent : ComponentBase
        {
            public int NewCounter { get; set; }
        }
        """,
        """
        @using System
        @using System.Collections.Generic

        @code {
            public int NewCounter { get; set; } 
        }
        """);

    [Fact]
    public Task NewUsing_AfterPage()
        => TestAsync(
        """
        class MyComponent : ComponentBase
        {
            {|map1:public int Counter { get; set; }|}
        }
        """,
        """
        @page "/counter"

        @code {
            {|map1:public int Counter { get; set; }|} 
        }
        """,
        """
        using System;
        using System.Collections.Generic;

        class MyComponent : ComponentBase
        {
            public int NewCounter { get; set; }
        }
        """,
        """
        @page "/counter"
        @using System
        @using System.Collections.Generic

        @code {
            public int NewCounter { get; set; } 
        }
        """);

    [Fact]
    public Task NewUsing_AfterPage2()
        => TestAsync(
        """
        class MyComponent : ComponentBase
        {
            {|map1:public int Counter { get; set; }|}
        }
        """,
        """
        @page "/counter"

        <h3>Counter</h3>
        <p>Current count: @Counter</p>

        @code {
            {|map1:public int Counter { get; set; }|} 
        }
        """,
        """
        using System;
        using System.Collections.Generic;

        class MyComponent : ComponentBase
        {
            public int NewCounter { get; set; }
        }
        """,
        """
        @page "/counter"
        @using System
        @using System.Collections.Generic

        <h3>Counter</h3>
        <p>Current count: @Counter</p>

        @code {
            public int NewCounter { get; set; } 
        }
        """);

    [Fact]
    public Task RenamedUsing_Applies()
        => TestAsync(
        """
        {|mapUsing:using System;|}
        {|mapUsing2:using System.Collections.Generic;|}
        class MyComponent : ComponentBase
        {
            {|map1:public int Counter { get; set; }|}
        }
        """,
        """
        @page "/counter"
        {|mapUsing:@using System|}
        {|mapUsing2:@using System.Collections.Generic|}

        @code {
            {|map1:public int Counter { get; set; }|} 
        }
        """,
        """
        using Renamed;

        class MyComponent : ComponentBase
        {
            public int NewCounter { get; set; }
        }
        """,
        """
        @page "/counter"
        @using Renamed



        @code {
            public int NewCounter { get; set; } 
        }
        """);

    [Fact]
    public Task AddUsing_AfterSystem()
        => TestAsync(
        """
        {|mapUsing:using System;|}
        {|mapUsing2:using System.Collections.Generic;|}
        class MyComponent : ComponentBase
        {
            {|map1:public int Counter { get; set; }|}
        }
        """,
        """
        @page "/counter"
        {|mapUsing:@using System|}
        {|mapUsing2:@using System.Collections.Generic|}

        @code {
            {|map1:public int Counter { get; set; }|} 
        }
        """,
        """
        using OtherNamespace;
        using System;

        class MyComponent : ComponentBase
        {
            public int NewCounter { get; set; }
        }
        """,
        """
        @page "/counter"
        @using System

        @using OtherNamespace

        @code {
            public int NewCounter { get; set; } 
        }
        """);

    [Fact]
    public Task AddUsing_OrdersSystemCorrectly()
    => TestAsync(
    """
        {|mapUsing:using System;|}
        {|mapUsing2:using MyNamespace;|}
        class MyComponent : ComponentBase
        {
            {|map1:public int Counter { get; set; }|}
        }
        """,
    """
        @page "/counter"
        {|mapUsing:@using System|}
        {|mapUsing2:@using MyNamespace|}

        @code {
            {|map1:public int Counter { get; set; }|} 
        }
        """,
    """
        using OtherNamespace;
        using System;
        using System.Collections.Generic;

        class MyComponent : ComponentBase
        {
            public int NewCounter { get; set; }
        }
        """,
    """
        @page "/counter"
        @using System
        @using System.Collections.Generic

        @using OtherNamespace

        @code {
            public int NewCounter { get; set; } 
        }
        """);

    [Fact]
    public Task UsingIndentation_DoesNotApply()
        => TestAsync(
        """
        {|mapUsing:using System;|}
        {|mapUsing2:using System.Collections.Generic;|}
        class MyComponent : ComponentBase
        {
            {|map1:public int Counter { get; set; }|}
        }
        """,
        """
        @page "/counter"
        {|mapUsing:@using System|}
        {|mapUsing2:@using System.Collections.Generic|}

        @code {
            {|map1:public int Counter { get; set; }|} 
        }
        """,
        """
            using System;
            using System.Collections.Generic;

        class MyComponent : ComponentBase
        {
            public int NewCounter { get; set; }
        }
        """,
        """
        @page "/counter"
        @using System
        @using System.Collections.Generic

        @code {
            public int NewCounter { get; set; } 
        }
        """);

    private async Task TestAsync(
        string annotatedCsharpSource,
        string annotatedRazorSource,
        string newCSharpSource,
        string newRazorSource)
    {
        TestFileMarkupParser.GetSpans(annotatedCsharpSource, out var csharpSource, out ImmutableDictionary<string, ImmutableArray<TextSpan>> csharpSpans);
        TestFileMarkupParser.GetSpans(annotatedRazorSource, out var razorSource, out ImmutableDictionary<string, ImmutableArray<TextSpan>> razorSpans);

        AssertEx.SetEqual(csharpSpans.Keys.OrderAsArray(), razorSpans.Keys.OrderAsArray());

        var csharpPath = @"C:\path\to\document.razor.g.cs";
        var razorPath = @"C:\path\to\document.razor"; 
        var csharpSourceText = SourceText.From(csharpSource);
        var razorSourceText = SourceText.From(razorSource);

        var sourceMappings = new List<SourceMapping>();
        foreach (var key in csharpSpans.Keys)
        {
            var csharpSpan = csharpSpans[key].Single();
            var razorSpan = razorSpans[key].Single();

            var csharpLinePosition = csharpSourceText.GetLinePositionSpan(csharpSpan);
            var razorLinePosition = razorSourceText.GetLinePositionSpan(razorSpan);

            var csharpSourceSpan = new SourceSpan(
                csharpPath,
                csharpSpan.Start,
                csharpLinePosition.Start.Line,
                csharpLinePosition.Start.Character,
                csharpSpan.Length);

            var razorSourceSpan = new SourceSpan(
                razorPath,
                razorSpan.Start,
                razorLinePosition.Start.Line,
                razorLinePosition.Start.Character,
                razorSpan.Length);

            sourceMappings.Add(new SourceMapping(razorSourceSpan, csharpSourceSpan));
        }

        var newCsharpSourceText = SourceText.From(newCSharpSource);
        var changes = GetChanges(csharpSource, newCSharpSource);

        var codeDocument = CreateCodeDocument(razorSource, tagHelpers: [], filePath: razorPath);
        var csharpDocument = new RazorCSharpDocument(
            codeDocument,
            csharpSource,
            RazorCodeGenerationOptions.Default,
            diagnostics: [],
            sourceMappings.OrderByAsArray(s => s.GeneratedSpan.AbsoluteIndex),
            linePragmas: []);

        codeDocument.SetCSharpDocument(csharpDocument);
        var documentContext = CreateDocumentContext(new Uri(razorPath), codeDocument);
        var languageEndpoint = new RazorMapToDocumentEditsEndpoint(_documentMappingService, FailingTelemetryReporter.Instance);
        var request = new RazorMapToDocumentEditsParams()
        {
            Kind = RazorLanguageKind.CSharp,
            TextEdits = changes.ToArray(),
            RazorDocumentUri = new Uri(razorPath),
        };

        var requestContext = CreateRazorRequestContext(documentContext);

        var response = await languageEndpoint.HandleRequestAsync(request, requestContext, CancellationToken.None);

        Assert.NotNull(response);
        Assert.NotEmpty(response.Edits);

        var newRazorSourceText = razorSourceText.WithChanges(response.Edits);
        Assert.Equal(newRazorSource, newRazorSourceText.ToString());
    }

    private ImmutableArray<TextChange> GetChanges(string csharpSource, string newCSharpSource)
    {
        var tree = CSharpSyntaxTree.ParseText(csharpSource);
        var newTree = CSharpSyntaxTree.ParseText(newCSharpSource);

        return newTree.GetChanges(tree).ToImmutableArray();
    }

    private class FailingTelemetryReporter : NoOpTelemetryReporter
    {
        public static readonly new FailingTelemetryReporter Instance = new FailingTelemetryReporter();

        new public void ReportFault(Exception _, string? message, params object?[] @params)
        {
            Assert.Fail($"Did not expect to report a fault. :: {message} :: {string.Join(",", @params ?? [])}");
        }
    }
}
