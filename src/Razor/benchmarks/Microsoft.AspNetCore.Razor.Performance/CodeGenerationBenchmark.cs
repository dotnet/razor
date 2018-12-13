// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Extensions;

namespace Microsoft.AspNetCore.Razor.Performance
{
    public class CodeGenerationBenchmark
    {
        public CodeGenerationBenchmark()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while  (current != null && !File.Exists(Path.Combine(current.FullName, "MSN.cshtml")))
            {
                current = current.Parent;
            }

            var root = current;
            var fileSystem = RazorProjectFileSystem.Create(root.FullName);
            
            ProjectEngine = RazorProjectEngine.Create(RazorConfiguration.Default, fileSystem, b => RazorExtensions.Register(b)); ;

            var projectItem = fileSystem.GetItem(Path.Combine(root.FullName, "MSN.cshtml"));
            var sourceDocument = RazorSourceDocument.ReadFrom(projectItem);
            var imports = MvcImportItems.GetImportItems(fileSystem, projectItem).Select(RazorSourceDocument.ReadFrom);

            MSN = RazorCodeDocument.Create(
                sourceDocument,
                imports,
                RazorParserOptions.CreateDefault(),
                RazorCodeGenerationOptions.CreateDefault());

            MSN_DesignTime = RazorCodeDocument.Create(
                sourceDocument,
                imports,
                RazorParserOptions.CreateDesignTime(_ => { }),
                RazorCodeGenerationOptions.CreateDesignTimeDefault());
        }

        public RazorProjectEngine ProjectEngine { get; }

        public RazorCodeDocument MSN { get; }

        public RazorCodeDocument MSN_DesignTime { get; }

        [Benchmark(Description = "Razor Design Time Code Generation of MSN.com")]
        public void CodeGeneration_DesignTime_LargeStaticFile()
        {
            ProjectEngine.Process(MSN_DesignTime);
            var generated = MSN_DesignTime.GetCSharpDocument();

            if (generated.Diagnostics.Count != 0)
            {
                throw new Exception("Error!" + Environment.NewLine + string.Join(Environment.NewLine, generated.Diagnostics));
            }
        }

        [Benchmark(Description = "Razor Runtime Code Generation of MSN.com")]
        public void CodeGeneration_Runtime_LargeStaticFile()
        {
            ProjectEngine.Process(MSN);
            var generated = MSN.GetCSharpDocument();

            if (generated.Diagnostics.Count != 0)
            {
                throw new Exception("Error!" + Environment.NewLine + string.Join(Environment.NewLine, generated.Diagnostics));
            }
        }
    }
}
