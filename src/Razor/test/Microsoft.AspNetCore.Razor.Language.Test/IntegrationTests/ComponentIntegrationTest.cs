// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language.IntegrationTests
{
    public class ComponentIntegrationTest : IntegrationTestBase
    {
        public ComponentIntegrationTest()
            : base(generateBaselines: null)
        {
            Configuration = RazorConfiguration.Default;
            FileExtension = ".razor";
        }

        protected override RazorConfiguration Configuration { get; }

        [Fact]
        public void BasicTest()
        {
            var projectEngine = CreateProjectEngine();

            var projectItem = CreateProjectItemFromFile();
            var imports = Array.Empty<RazorSourceDocument>();
            var parserOptions = projectEngine.GetParserOptions();
            var codeGenerationOptions = RazorCodeGenerationOptions.ForComponents(
                options => options.SuppressChecksum = true);

            var sourceDocument = RazorSourceDocument.ReadFrom(projectItem);
            var codeDocument = RazorCodeDocument.Create(
                sourceDocument,
                imports,
                parserOptions,
                codeGenerationOptions);
            codeDocument.SetInputDocumentKind(InputDocumentKind.Component);


            // Act
            projectEngine.Process(codeDocument);

            // Assert
            AssertDocumentNodeMatchesBaseline(codeDocument.GetDocumentIntermediateNode());
            AssertCSharpDocumentMatchesBaseline(codeDocument.GetCSharpDocument());
        }
    }
}
