// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.AspNetCore.Razor.Language;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators;

internal class SourceGeneratorProjectEngine : DefaultRazorProjectEngine
{
    public SourceGeneratorProjectEngine(DefaultRazorProjectEngine projectEngine)
		: base(projectEngine.Configuration, projectEngine.Engine, projectEngine.FileSystem, projectEngine.ProjectFeatures)
    {
    }

    public SourceGeneratorRazorCodeDocument ProcessInitialParse(RazorProjectItem projectItem)
	{
		if (projectItem == null)
		{
			throw new ArgumentNullException(nameof(projectItem));
		}

		var codeDocument = CreateCodeDocumentCore(projectItem);
		ProcessPartial(codeDocument, 0, 2);
		// record the syntax tree, before the tag helper re-writing occurs
		codeDocument.SetPreTagHelperSyntaxTree(codeDocument.GetSyntaxTree());
		return new SourceGeneratorRazorCodeDocument(codeDocument);
	}

	public SourceGeneratorRazorCodeDocument ProcessTagHelpers(SourceGeneratorRazorCodeDocument sgDocument, IReadOnlyList<TagHelperDescriptor> tagHelpers, bool checkForIdempotency)
	{
		int startIndex = 2;
		var codeDocument = sgDocument.CodeDocument;
		var inputTagHelpers = codeDocument.GetTagHelpers();
		if (checkForIdempotency && inputTagHelpers is not null)
		{
			// compare the input tag helpers with the ones the document last used
			if (Enumerable.SequenceEqual(inputTagHelpers, tagHelpers))
			{
				// tag helpers are the same, nothing to do!
				return sgDocument;
			}
			else
			{
				var oldContextHelpers = codeDocument.GetTagHelperContext().TagHelpers;
				
				// re-run the scope check to figure out which tag helpers this document can see
				codeDocument.SetTagHelpers(tagHelpers);
				ProcessPartial(codeDocument, 2, 3);

				// Check if any new tag helpers were added or ones we previous used were removed
				var newContextHelpers = codeDocument.GetTagHelperContext().TagHelpers;
				var added = newContextHelpers.Except(oldContextHelpers);
				var referencedByRemoved = codeDocument.GetReferencedTagHelpers().Except(newContextHelpers);
				if (!added.Any() && !referencedByRemoved.Any())
				{
					//  Either nothing new, or the one that got removed wasn't used by this document anyway
					return sgDocument;
				}
				
				// We need to re-write the document, but can skip the scoping as we just performed it
				startIndex = 3;
			}
		}
		else
		{
			codeDocument.SetTagHelpers(tagHelpers);
		}

		ProcessPartial(codeDocument, startIndex, 4);
		return new SourceGeneratorRazorCodeDocument(codeDocument);
	}

	public SourceGeneratorRazorCodeDocument ProcessRemaining(SourceGeneratorRazorCodeDocument sgDocument)
	{
		// PROTOTYPE: assert we're at a point that this can process.

		var codeDocument = sgDocument.CodeDocument;
		ProcessPartial(sgDocument.CodeDocument, 4, Engine.Phases.Count);
		return new SourceGeneratorRazorCodeDocument(codeDocument);
	}

	private void ProcessPartial(RazorCodeDocument codeDocument, int startIndex, int endIndex)
	{
		for (var i = startIndex; i < endIndex; i++)
		{
			Engine.Phases[i].Execute(codeDocument);
		}
	}
}
