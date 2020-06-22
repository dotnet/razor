// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Editor.Razor;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    internal class RefactorComponentCodeActionProvider : RazorCodeActionProvider
    {
        private readonly TagHelperFactsService _tagHelperFactsServce;
        private readonly FilePathNormalizer _filePathNormalizer;
        private readonly ILogger _logger;

        public RefactorComponentCodeActionProvider(
            TagHelperFactsService tagHelperFactsServce,
            FilePathNormalizer filePathNormalizer,
            ILoggerFactory loggerFactory)
        {
            if (tagHelperFactsServce is null)
            {
                throw new ArgumentNullException(nameof(tagHelperFactsServce));
            }

            if (filePathNormalizer is null)
            {
                throw new ArgumentNullException(nameof(filePathNormalizer));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _filePathNormalizer = filePathNormalizer;
            _tagHelperFactsServce = tagHelperFactsServce;
            _logger = loggerFactory.CreateLogger<ExtractToCodeBehindCodeActionProvider>();
        }

        override public Task<CommandOrCodeActionContainer> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
        {
            var container = new List<CommandOrCodeAction>();
            var change = new SourceChange(context.Location.AbsoluteIndex, length: 0, newText: string.Empty);
            var node = context.CodeDocument.GetSyntaxTree().Root.LocateOwner(change);
            if (node is null)
            {
                return Task.FromResult<CommandOrCodeActionContainer>(null);
            }

            node = node.Ancestors().FirstOrDefault(n => n is MarkupStartTagSyntax);
            if (node == null)
            {
                return Task.FromResult<CommandOrCodeActionContainer>(null);
            }

            var startTag = (MarkupStartTagSyntax)node;
            if (IsTagUnknown(startTag, context))
            {
                AddCreateComponentFromTag(context, startTag, container);
                AddComponentAccessFromTag(context, startTag, container);
            }

            return Task.FromResult(new CommandOrCodeActionContainer(container));
        }

        private void AddCreateComponentFromTag(RazorCodeActionContext context, MarkupStartTagSyntax startTag, List<CommandOrCodeAction> container)
        {
            var path = _filePathNormalizer.Normalize(context.Request.TextDocument.Uri.GetAbsoluteOrUNCPath());
            var newComponentPath = Path.Combine(Path.GetDirectoryName(path), $"{startTag.Name.Content}.razor");
            if (File.Exists(newComponentPath))
            {
                return;
            }

            var actionParams = new RefactorComponentCreateParams()
            {
                Uri = context.Request.TextDocument.Uri,
                Name = startTag.Name.Content,
                Where = newComponentPath,
            };
            var data = JObject.FromObject(actionParams);

            var resolutionParams = new RazorCodeActionResolutionParams()
            {
                Action = LanguageServerConstants.CodeActions.CreateComponentFromTag,
                Data = data,
            };
            var serializedParams = JToken.FromObject(resolutionParams);
            var arguments = new JArray(serializedParams);

            container.Add(new CommandOrCodeAction(new Command()
            {
                Title = "Create component from tag",
                Name = "razor/runCodeAction",
                Arguments = arguments,
            }));
        }

        private void AddComponentAccessFromTag(RazorCodeActionContext context, MarkupStartTagSyntax startTag, List<CommandOrCodeAction> container)
        {
            var tagName = startTag.Name.Content;
            string parentTagName = null;
            if (startTag.Parent.Parent is MarkupElementSyntax parentElement)
            {
                parentTagName = parentElement.StartTag.Name.Content;
            }
            var attributes = _tagHelperFactsServce.StringifyAttributes(startTag.Attributes);

            var matching = new Dictionary<string, TagHelperPair>();
            foreach (var tagHelper in context.DocumentSnapshot.Project.TagHelpers)
            {
                if (tagHelper.TagMatchingRules.All(rule => TagHelperMatchingConventions.SatisfiesRule(tagName, parentTagName, attributes, rule)))
                {
                    matching.Add(tagHelper.Name, new TagHelperPair() { Short = tagHelper });
                }
            }
            foreach (var tagHelper in context.DocumentSnapshot.Project.TagHelpers)
            {
                if (matching.ContainsKey(tagHelper.Name))
                {
                    var tagHelperPair = matching[tagHelper.Name];
                    if (tagHelperPair != null && tagHelper != tagHelperPair.Short)
                    {
                        tagHelperPair.FullyQualified = tagHelper;
                    }
                }
            }
            foreach (var tagHelperPair in matching.Values)
            {
                DefaultRazorTagHelperBinderPhase.ComponentDirectiveVisitor.TrySplitNamespaceAndType(tagHelperPair.Short.Name, out var namespaceSpan, out var typeSpan);
                var namespaceName = tagHelperPair.Short.Name.Substring(namespaceSpan.Start, namespaceSpan.Length);
                var actionParams = new RefactorComponentUsingParams()
                {
                    Uri = context.Request.TextDocument.Uri,
                    Namespaces = new[] { namespaceName },
                };
                var data = JObject.FromObject(actionParams);

                var resolutionParams = new RazorCodeActionResolutionParams()
                {
                    Action = LanguageServerConstants.CodeActions.AddUsing,
                    Data = data,
                };
                var serializedParams = JToken.FromObject(resolutionParams);
                var arguments = new JArray(serializedParams);

                container.Add(new CommandOrCodeAction(new Command()
                {
                    Title = $"@using {namespaceName}",
                    Name = "razor/runCodeAction",
                    Arguments = arguments,
                }));
                container.Add(new CommandOrCodeAction(new CodeAction()
                {
                    Title = $"{tagHelperPair.Short.Name}",
                    Edit = CreateRenameTagEdit(context, startTag, tagHelperPair.Short.Name),
                }));
            }
        }

        private static WorkspaceEdit CreateRenameTagEdit(RazorCodeActionContext context, MarkupStartTagSyntax startTag, string newTagName)
        {
            var changes = new List<TextEdit>
            {
                new TextEdit()
                {
                    Range = startTag.Name.GetRange(context.CodeDocument.Source),
                    NewText = newTagName,
                },
            };
            var endTag = ((MarkupElementSyntax)startTag.Parent).EndTag;
            if (endTag != null)
            {
                changes.Add(new TextEdit()
                {
                    Range = endTag.Name.GetRange(context.CodeDocument.Source),
                    NewText = newTagName,
                });
            }
            return new WorkspaceEdit()
            {
                Changes = new Dictionary<Uri, IEnumerable<TextEdit>> {
                    [context.Request.TextDocument.Uri] = changes,
                }
            };
        }

        private sealed class TagHelperPair
        {
            public TagHelperDescriptor Short = null;
            public TagHelperDescriptor FullyQualified = null;
        }

        private void AddCreateComponentFromTag(RazorCodeActionContext context, MarkupStartTagSyntax startTag, List<CommandOrCodeAction> container)
        {
            var path = context.Request.TextDocument.Uri.GetAbsoluteOrUNCPath();
            var newComponentPath = Path.Combine(Path.GetDirectoryName(path), $"{startTag.Name.Content}.razor");
            if (File.Exists(newComponentPath))
            {
                return;
            }

            var actionParams = new RefactorComponentCreateParams()
            {
                Uri = context.Request.TextDocument.Uri,
                Name = startTag.Name.Content,
                Where = newComponentPath,
            };
            var data = JObject.FromObject(actionParams);

            var resolutionParams = new RazorCodeActionResolutionParams()
            {
                Action = Constants.RefactorComponentCreate,
                Data = data,
            };
            var serializedParams = JToken.FromObject(resolutionParams);
            var arguments = new JArray(serializedParams);

            container.Add(new CommandOrCodeAction(new Command()
            {
                Title = "Create component from tag",
                Name = "razor/runCodeAction",
                Arguments = arguments,
            }));
        }

        private bool IsTagUnknown(MarkupStartTagSyntax startTag, RazorCodeActionContext context)
        {
            foreach (var diagnostic in context.CodeDocument.GetCSharpDocument().Diagnostics)
            {
                if (!(diagnostic.Span.AbsoluteIndex > startTag.Span.End || startTag.Span.Start > diagnostic.Span.AbsoluteIndex + diagnostic.Span.Length))
                {
                    if (diagnostic.Id == "RZ10012")
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
