// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.VisualStudio.Razor.Integration.Test.InProcess;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Xunit;

namespace Microsoft.VisualStudio.Razor.Integration.Test
{
    [IntializeTestFile]
    public class RazorSemanticTokensTests : AbstractRazorEditorTest
    {
        private static readonly AsyncLocal<string?> s_fileName = new();

        private static readonly string s_projectPath = TestProject.GetProjectDirectory(typeof(RazorSemanticTokensTests), useCurrentDirectory: true);

        protected bool GenerateBaselines { get; set; } = false;

        // Used by the test framework to set the 'base' name for test files.
        public static string? FileName
        {
            get { return s_fileName.Value; }
            set { s_fileName.Value = value; }
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            await TestServices.Workspace.WaitForAsyncOperationsAsync(FeatureAttribute.Classification, HangMitigatingCancellationToken);
        }

        [IdeFact]
        public async Task Components_AreColored()
        {
            // Arrange
            await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, MainLayoutFile, HangMitigatingCancellationToken);

            // Act
            await TestServices.Editor.WaitForClassificationAsync(HangMitigatingCancellationToken, "RazorComponentElement");

            // Assert
            var expectedClassifications = await GetExpectedClassificationSpansAsync(nameof(Components_AreColored), HangMitigatingCancellationToken);
            await TestServices.Editor.VerifyGetClassificationsAsync(expectedClassifications, HangMitigatingCancellationToken);
        }

        [IdeFact]
        public async Task Directives_AreColored()
        {
            await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, CounterRazorFile, HangMitigatingCancellationToken);
            var expectedClassifications = await GetExpectedClassificationSpansAsync(nameof(Directives_AreColored), HangMitigatingCancellationToken);

            await TestServices.Editor.VerifyGetClassificationsAsync(expectedClassifications, HangMitigatingCancellationToken);
        }

        private async Task<IEnumerable<ClassificationSpan>> GetExpectedClassificationSpansAsync(string testName, CancellationToken cancellationToken)
        {
            var snapshot = await TestServices.Editor.GetActiveSnapshotAsync(HangMitigatingCancellationToken);

            if (GenerateBaselines)
            {
                var actual = await TestServices.Editor.GetClassificationsAsync(cancellationToken);
                GenerateSemanticBaseline(actual, testName);
            }

            var expectedClassifications = await ReadSemanticBaselineAsync(snapshot, cancellationToken);

            return expectedClassifications;
        }

        private async Task<IEnumerable<ClassificationSpan>> ReadSemanticBaselineAsync(ITextSnapshot snapshot, CancellationToken cancellationToken)
        {
            var baselinePath = Path.ChangeExtension(FileName, ".txt");
            var assembly = GetType().GetTypeInfo().Assembly;
            var semanticFile = TestFile.Create(baselinePath, assembly);

            var semanticStr = await semanticFile.ReadAllTextAsync(cancellationToken);

            return ParseSemanticBaseline(semanticStr, snapshot);

            static IEnumerable<ClassificationSpan> ParseSemanticBaseline(string semanticStr, ITextSnapshot snapshot)
            {
                var result = new List<ClassificationSpan>();
                var strArray = semanticStr.Split(new[] { Separator.ToString(), Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                for (var i = 0; i < strArray.Length; i += 3)
                {
                    if (!int.TryParse(strArray[i], out var position))
                    {
                        throw new InvalidOperationException($"{strArray[i]} was not an int {i}");
                    }

                    if (!int.TryParse(strArray[i + 1], out var length))
                    {
                        throw new InvalidOperationException($"{strArray[i + 1]} was not an int {i}");
                    }

                    var snapshotSpan = new SnapshotSpan(snapshot, position, length);

                    var classification = strArray[i + 2];
                    var classificationType = new ClassificationType(classification);

                    result.Add(new ClassificationSpan(snapshotSpan, classificationType));
                }

                return result;
            }
        }
        private const char Separator = ',';

        private static void GenerateSemanticBaseline(IEnumerable<ClassificationSpan> actual, string baselineFileName)
        {
            var builder = new StringBuilder();
            foreach (var baseline in actual)
            {
                builder.Append(baseline.Span.Start.Position).Append(Separator);
                builder.Append(baseline.Span.Length).Append(Separator);

                var classification = baseline.ClassificationType;
                string? classificationStr = null;
                if (classification.BaseTypes.Count() > 1)
                {
                    foreach (ILayeredClassificationType baseType in classification.BaseTypes)
                    {
                        if (baseType.Layer == ClassificationLayer.Semantic)
                        {
                            classificationStr = baseType.Classification;
                            break;
                        }
                    }

                    if (classificationStr is null)
                    {
                        Assert.True(false, "Tried to write layered classifications without Semantic layer");
                        throw new Exception();
                    }
                }
                else
                {
                    classificationStr = classification.Classification;
                }

                builder.Append(classificationStr).Append(Separator);
                builder.AppendLine();
            }

            var semanticBaselinePath = GetBaselineFileName(baselineFileName);
            File.WriteAllText(semanticBaselinePath, builder.ToString());
        }

        private static string GetBaselineFileName(string testName)
        {
            var semanticBaselinePath = Path.Combine(s_projectPath, "Semantic", "TestFiles", nameof(RazorSemanticTokensTests), testName + ".txt");
            return semanticBaselinePath;
        }

        private class ClassificationType : IClassificationType
        {
            public ClassificationType(string classification)
            {
                Classification = classification;
            }

            public string Classification { get; }

            public IEnumerable<IClassificationType> BaseTypes => throw new System.NotImplementedException();

            public bool IsOfType(string type)
            {
                throw new System.NotImplementedException();
            }
        }
    }

}
