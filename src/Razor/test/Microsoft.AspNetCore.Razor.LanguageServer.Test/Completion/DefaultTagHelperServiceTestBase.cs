// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Editor.Razor;
using Moq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;
using Xunit;
using Xunit.Sdk;
using DefaultRazorTagHelperCompletionService = Microsoft.VisualStudio.Editor.Razor.DefaultTagHelperCompletionService;
using RazorTagHelperCompletionService = Microsoft.VisualStudio.Editor.Razor.TagHelperCompletionService;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{

    public abstract class DefaultTagHelperServiceTestBase : LanguageServerTestBase
    {
        private static readonly AsyncLocal<string?> _fileName = new AsyncLocal<string?>();

        private static readonly string _projectPath = TestProject.GetProjectDirectory(typeof(DefaultTagHelperServiceTestBase));

        protected const string CSHtmlFile = "test.cshtml";
        protected const string RazorFile = "test.razor";

        // Used by the test framework to set the 'base' name for test files.
        public static string? FileName
        {
            get { return _fileName.Value; }
            set { _fileName.Value = value; }
        }

        public DefaultTagHelperServiceTestBase()
        {
            var builder1 = TagHelperDescriptorBuilder.Create("Test1TagHelper", "TestAssembly");
            builder1.TagMatchingRule(rule => rule.TagName = "test1");
            builder1.SetTypeName("Test1TagHelper");
            builder1.BindAttribute(attribute =>
            {
                attribute.Name = "bool-val";
                attribute.SetPropertyName("BoolVal");
                attribute.TypeName = typeof(bool).FullName;
            });
            builder1.BindAttribute(attribute =>
            {
                attribute.Name = "int-val";
                attribute.SetPropertyName("IntVal");
                attribute.TypeName = typeof(int).FullName;
            });

            var builder2 = TagHelperDescriptorBuilder.Create("Test2TagHelper", "TestAssembly");
            builder2.TagMatchingRule(rule => rule.TagName = "test2");
            builder2.SetTypeName("Test2TagHelper");
            builder2.BindAttribute(attribute =>
            {
                attribute.Name = "bool-val";
                attribute.SetPropertyName("BoolVal");
                attribute.TypeName = typeof(bool).FullName;
            });
            builder2.BindAttribute(attribute =>
            {
                attribute.Name = "int-val";
                attribute.SetPropertyName("IntVal");
                attribute.TypeName = typeof(int).FullName;
            });

            var builder3 = TagHelperDescriptorBuilder.Create(ComponentMetadata.Component.TagHelperKind, "Component1TagHelper", "TestAssembly");
            builder3.TagMatchingRule(rule => rule.TagName = "Component1");
            builder3.SetTypeName("Component1");
            builder3.Metadata[ComponentMetadata.Component.NameMatchKey] = ComponentMetadata.Component.FullyQualifiedNameMatch;
            builder3.BindAttribute(attribute =>
            {
                attribute.Name = "bool-val";
                attribute.SetPropertyName("BoolVal");
                attribute.TypeName = typeof(bool).FullName;
            });
            builder3.BindAttribute(attribute =>
            {
                attribute.Name = "int-val";
                attribute.SetPropertyName("IntVal");
                attribute.TypeName = typeof(int).FullName;
            });

            var directiveAttribute1 = TagHelperDescriptorBuilder.Create(ComponentMetadata.Component.TagHelperKind, "TestDirectiveAttribute", "TestAssembly");
            directiveAttribute1.TagMatchingRule(rule =>
            {
                rule.TagName = "*";
                rule.RequireAttributeDescriptor(b =>
                {
                    b.Name = "@test";
                    b.NameComparisonMode = RequiredAttributeDescriptor.NameComparisonMode.PrefixMatch;
                });
            });
            directiveAttribute1.TagMatchingRule(rule =>
            {
                rule.TagName = "*";
                rule.RequireAttributeDescriptor(b =>
                {
                    b.Name = "@test";
                    b.NameComparisonMode = RequiredAttributeDescriptor.NameComparisonMode.FullMatch;
                });
            });
            directiveAttribute1.BindAttribute(attribute =>
            {
                attribute.Metadata[ComponentMetadata.Common.DirectiveAttribute] = bool.TrueString;
                attribute.Name = "@test";
                attribute.SetPropertyName("Test");
                attribute.TypeName = typeof(string).FullName;

                attribute.BindAttributeParameter(parameter =>
                {
                    parameter.Name = "something";
                    parameter.TypeName = typeof(string).FullName;

                    parameter.SetPropertyName("Something");
                });
            });
            directiveAttribute1.Metadata[ComponentMetadata.Component.NameMatchKey] = ComponentMetadata.Component.FullyQualifiedNameMatch;
            directiveAttribute1.SetTypeName("TestDirectiveAttribute");

            var directiveAttribute2 = TagHelperDescriptorBuilder.Create(ComponentMetadata.Component.TagHelperKind, "MinimizedDirectiveAttribute", "TestAssembly");
            directiveAttribute2.TagMatchingRule(rule =>
            {
                rule.TagName = "*";
                rule.RequireAttributeDescriptor(b =>
                {
                    b.Name = "@minimized";
                    b.NameComparisonMode = RequiredAttributeDescriptor.NameComparisonMode.PrefixMatch;
                });
            });
            directiveAttribute2.TagMatchingRule(rule =>
            {
                rule.TagName = "*";
                rule.RequireAttributeDescriptor(b =>
                {
                    b.Name = "@minimized";
                    b.NameComparisonMode = RequiredAttributeDescriptor.NameComparisonMode.FullMatch;
                });
            });
            directiveAttribute2.BindAttribute(attribute =>
            {
                attribute.Metadata[ComponentMetadata.Common.DirectiveAttribute] = bool.TrueString;
                attribute.Name = "@minimized";
                attribute.SetPropertyName("Minimized");
                attribute.TypeName = typeof(bool).FullName;

                attribute.BindAttributeParameter(parameter =>
                {
                    parameter.Name = "something";
                    parameter.TypeName = typeof(string).FullName;

                    parameter.SetPropertyName("Something");
                });
            });
            directiveAttribute2.Metadata[ComponentMetadata.Component.NameMatchKey] = ComponentMetadata.Component.FullyQualifiedNameMatch;
            directiveAttribute2.SetTypeName("TestDirectiveAttribute");

            DefaultTagHelpers = new[] { builder1.Build(), builder2.Build(), builder3.Build(), directiveAttribute1.Build(), directiveAttribute2.Build() };

            HtmlFactsService = new DefaultHtmlFactsService();
            TagHelperFactsService = new DefaultTagHelperFactsService();
            RazorTagHelperCompletionService = new DefaultRazorTagHelperCompletionService(TagHelperFactsService);
        }

        protected TagHelperDescriptor[] DefaultTagHelpers { get; }

        protected RazorTagHelperCompletionService RazorTagHelperCompletionService { get; }

        internal HtmlFactsService HtmlFactsService { get; }

        protected TagHelperFactsService TagHelperFactsService { get; }

        internal static RazorCodeDocument CreateCodeDocument(string text, params TagHelperDescriptor[] tagHelpers)
        {
            return CreateCodeDocument(text, CSHtmlFile, tagHelpers);
        }

        protected TextDocumentIdentifier GetIdentifier(bool isRazor)
        {
            var file = isRazor ? RazorFile : CSHtmlFile;
            return new TextDocumentIdentifier(new Uri($"c:\\${file}"));
        }

        internal (Queue<DocumentSnapshot>, Queue<TextDocumentIdentifier>) CreateDocumentSnapshot(string?[] textArray, bool[] isRazorArray, params TagHelperDescriptor[] tagHelpers)
        {
            var documentSnapshots = new Queue<DocumentSnapshot>();
            var identifiers = new Queue<TextDocumentIdentifier>();
            foreach (var (text, isRazor) in textArray.Zip(isRazorArray, (t, r) => (t, r)))
            {
                var file = isRazor ? RazorFile : CSHtmlFile;
                var document = CreateCodeDocument(text, file, tagHelpers);
                var documentSnapshot = new Mock<DocumentSnapshot>(MockBehavior.Strict);
                documentSnapshot.Setup(d => d.GetGeneratedOutputAsync())
                    .ReturnsAsync(document);

                var version = VersionStamp.Create();
                documentSnapshot.Setup(d => d.GetTextVersionAsync())
                    .ReturnsAsync(version);

                documentSnapshots.Enqueue(documentSnapshot.Object);
                var identifier = GetIdentifier(isRazor);
                identifiers.Enqueue(identifier);
            }

            return (documentSnapshots, identifiers);
        }

        internal static RazorCodeDocument CreateCodeDocument(string text, string filePath, params TagHelperDescriptor[] tagHelpers)
        {
            tagHelpers ??= Array.Empty<TagHelperDescriptor>();
            var sourceDocument = TestRazorSourceDocument.Create(text, filePath: filePath, relativePath: filePath);
            var projectEngine = RazorProjectEngine.Create(builder => { });
            var fileKind = filePath.EndsWith(".razor", StringComparison.Ordinal) ? FileKinds.Component : FileKinds.Legacy;
            var codeDocument = projectEngine.ProcessDesignTime(sourceDocument, fileKind, Array.Empty<RazorSourceDocument>(), tagHelpers);

            return codeDocument;
        }

#if GENERATE_BASELINES
        protected bool GenerateBaselines { get; set; } = true;
#else
        protected bool GenerateBaselines { get; set; } = true;
#endif

        protected int BaselineTestCount { get; set; }
        protected int BaselineEditTestCount { get; set; }

        internal void AssertSemanticTokensMatchesBaseline(IEnumerable<int>? actualSemanticTokens)
        {
            if (FileName is null)
            {
                var message = $"{nameof(AssertSemanticTokensMatchesBaseline)} should only be called from a Semantic test ({nameof(FileName)} is null).";
                throw new InvalidOperationException(message);
            }

            var fileName = BaselineTestCount > 0 ? FileName + $"_{BaselineTestCount}" : FileName;
            var baselineFileName = Path.ChangeExtension(fileName, ".semantic.txt");
            var actual = actualSemanticTokens?.ToArray();

            BaselineTestCount++;
            if (GenerateBaselines)
            {
                GenerateSemanticBaseline(actual, baselineFileName);
            }

            var semanticFile = TestFile.Create(baselineFileName, GetType().GetTypeInfo().Assembly);
            if (!semanticFile.Exists())
            {
                throw new XunitException($"The resource {baselineFileName} was not found.");
            }
            var semanticIntStr = semanticFile.ReadAllText();
            var semanticArray = ParseSemanticBaseline(semanticIntStr);

            Assert.Equal(semanticArray, actual);
        }

#pragma warning disable CS0618 // Type or member is obsolete
        internal void AssertSemanticTokensEditsMatchesBaseline(SemanticTokensFullOrDelta edits)
#pragma warning restore CS0618 // Type or member is obsolete
        {
            if (FileName is null)
            {
                var message = $"{nameof(AssertSemanticTokensEditsMatchesBaseline)} should only be called from a Semantic test ({nameof(FileName)} is null).";
                throw new InvalidOperationException(message);
            }

            var fileName = BaselineEditTestCount > 0 ? FileName + $"_{BaselineEditTestCount}" : FileName;
            var baselineFileName = Path.ChangeExtension(fileName, ".semanticedit.txt");

            BaselineEditTestCount++;
            if (GenerateBaselines)
            {
                GenerateSemanticEditBaseline(edits, baselineFileName);
            }

            var semanticEditFile = TestFile.Create(baselineFileName, GetType().GetTypeInfo().Assembly);
            if (!semanticEditFile.Exists())
            {
                throw new XunitException($"The resource {baselineFileName} was not found.");
            }
            var semanticEditStr = semanticEditFile.ReadAllText();
            var semanticEdits = ParseSemanticEditBaseline(semanticEditStr);

            if (semanticEdits!.Value.IsDelta && edits.IsDelta)
            {
                // We can't compare the ResultID because it's from a previous run
                Assert.Equal(semanticEdits.Value.Delta?.Edits, edits.Delta?.Edits, SemanticEditComparer.Instance);
            }
            else if (semanticEdits.Value.IsFull && edits.IsFull)
            {
                Assert.Equal(semanticEdits.Value.Full, edits.Full);
            }
            else
            {
                Assert.True(false, $"Expected and actual semantic edits did not match.");
            }
        }

#pragma warning disable CS0618 // Type or member is obsolete
        private static void GenerateSemanticEditBaseline(SemanticTokensFullOrDelta edits, string baselineFileName)
#pragma warning restore CS0618 // Type or member is obsolete
        {
            var builder = new StringBuilder();
            if (edits.IsDelta)
            {
                builder.AppendLine("Delta ");
                foreach (var edit in edits.Delta!.Edits)
                {
                    builder.Append(edit.Start).Append(' ');
                    builder.Append(edit.DeleteCount).Append(" [ ");

                    foreach (var i in edit.Data!)
                    {
                        builder.Append(i).Append(' ');
                    }
                    builder.AppendLine("] ");
                }
                builder.Append(edits.Delta.ResultId);
            }
            else
            {
                foreach (var d in edits.Full!.Data)
                {
                    builder.Append(d).Append(' ');
                }
                builder.Append(edits.Full!.ResultId);
                throw new NotImplementedException();
            }

            var semanticBaselineEditPath = Path.Combine(_projectPath, baselineFileName);
            File.WriteAllText(semanticBaselineEditPath, builder.ToString());
        }

        private static void GenerateSemanticBaseline(IEnumerable<int>? actual, string baselineFileName)
        {
            var builder = new StringBuilder();
            if (actual != null)
            {
                var actualArray = actual.ToArray();
                builder.AppendLine("//line,characterPos,length,tokenType,modifier");
                var legendArray = RazorSemanticTokensLegend.TokenTypes.ToArray();
                for (var i = 0; i < actualArray.Length; i += 5)
                {
                    var typeString = legendArray[actualArray[i + 3]];
                    builder.Append(actualArray[i]).Append(' ');
                    builder.Append(actualArray[i + 1]).Append(' ');
                    builder.Append(actualArray[i + 2]).Append(' ');
                    builder.Append(actualArray[i + 3]).Append(' ');
                    builder.Append(actualArray[i + 4]).Append(" //").Append(typeString);
                    builder.AppendLine();
                }
            }

            var semanticBaselinePath = Path.Combine(_projectPath, baselineFileName);
            File.WriteAllText(semanticBaselinePath, builder.ToString());
        }

        private static IEnumerable<int>? ParseSemanticBaseline(string semanticIntStr)
        {
            if (string.IsNullOrEmpty(semanticIntStr))
            {
                return null;
            }

            var strArray = semanticIntStr.Split(new string[] { " ", Environment.NewLine }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var results = new List<int>();
            foreach (var str in strArray)
            {
                if (str.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                var intResult = int.Parse(str, Thread.CurrentThread.CurrentCulture);
                results.Add(intResult);
            }

            return results;
        }

#pragma warning disable CS0618 // Type or member is obsolete
        private static SemanticTokensFullOrDelta? ParseSemanticEditBaseline(string semanticEditStr)
        {
            if (string.IsNullOrEmpty(semanticEditStr))
            {
                return null;
            }

            var strArray = semanticEditStr.Split(new string[] { " ", Environment.NewLine }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (strArray[0].Equals("Delta", StringComparison.Ordinal))
            {
                var delta = new SemanticTokensDelta();
                var edits = new List<SemanticTokensEdit>();
                var i = 1;
                while (i < strArray.Length - 1)
                {
                    var edit = new SemanticTokensEdit
                    {
                        Start = int.Parse(strArray[i], Thread.CurrentThread.CurrentCulture),
                        DeleteCount = int.Parse(strArray[i + 1], Thread.CurrentThread.CurrentCulture)
                    };
                    i += 3;
                    var inArray = true;
                    var data = new List<int>();
                    while (inArray)
                    {
                        var str = strArray[i];
                        if (str.Equals("]", StringComparison.Ordinal))
                        {
                            inArray = false;
                        }
                        else
                        {
                            data.Add(int.Parse(str, Thread.CurrentThread.CurrentCulture));
                        }

                        i++;
                    }
                    edit.Data = data.ToImmutableArray();
                    edits.Add(edit);
                }
                delta.Edits = edits;
                delta.ResultId = strArray.Last();

                return new SemanticTokensFullOrDelta(delta);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private class SemanticEditComparer : IEqualityComparer<SemanticTokensEdit>
        {
            public static SemanticEditComparer Instance = new SemanticEditComparer();

            public bool Equals(SemanticTokensEdit? x, SemanticTokensEdit? y)
            {
                if (x == null && y == null)
                {
                    return true;
                }
                else if (x is null || y is null)
                {
                    return false;
                }

                Assert.Equal(x.DeleteCount, y.DeleteCount);
                Assert.Equal(x.Start, y.Start);
                Assert.Equal(x.Data!.Value, y.Data!.Value, ImmutableArrayIntComparer.Instance);

                return x.DeleteCount == y.DeleteCount &&
                    x.Start == y.Start;
            }

            public int GetHashCode(SemanticTokensEdit obj)
            {
                throw new NotImplementedException();
            }
        }

        private class ImmutableArrayIntComparer : IEqualityComparer<ImmutableArray<int>>
        {
            public static ImmutableArrayIntComparer Instance = new ImmutableArrayIntComparer();

            public bool Equals(ImmutableArray<int> x, ImmutableArray<int> y)
            {
                for (var i = 0; i < Math.Min(x.Length, y.Length); i++)
                {
                    Assert.True(x[i] == y[i], $"x {x[i]} y {y[i]} i {i}");
                }
                Assert.Equal(x.Length, y.Length);

                return true;
            }

            public int GetHashCode(ImmutableArray<int> obj)
            {
                throw new NotImplementedException();
            }
        }
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
