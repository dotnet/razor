// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.Components;

internal sealed class ComponentReferenceCaptureLoweringPass : ComponentIntermediateNodePassBase, IRazorOptimizationPass
{
    // Run after component lowering pass
    public override int Order => 50;

    protected override void ExecuteCore(
        RazorCodeDocument codeDocument,
        DocumentIntermediateNode documentNode,
        CancellationToken cancellationToken)
    {
        if (!IsComponentDocument(documentNode))
        {
            return;
        }

        var namespaceNode = documentNode.FindPrimaryNamespace();
        var classNode = documentNode.FindPrimaryClass();
        if (namespaceNode == null || classNode == null)
        {
            // Nothing to do, bail. We can't function without the standard structure.
            return;
        }

        var references = documentNode.FindDescendantReferences<TagHelperDirectiveAttributeIntermediateNode>();
        
        // Track field names to avoid generating duplicates
        var generatedFields = new HashSet<string>(StringComparer.Ordinal);

        foreach (var reference in references)
        {
            if (reference.Node.TagHelper.Kind == TagHelperKind.Ref)
            {
                RewriteUsage(classNode, reference, generatedFields);
            }
        }
    }

    private static void RewriteUsage(
        ClassDeclarationIntermediateNode classNode,
        IntermediateNodeReference<TagHelperDirectiveAttributeIntermediateNode> reference,
        HashSet<string> generatedFields)
    {
        var (node, parent) = reference;

        // If we can't get a non-empty attribute name, do nothing because there will
        // already be a diagnostic for empty values
        var identifierToken = DetermineIdentifierToken(node);
        if (identifierToken is null)
        {
            return;
        }

        // Determine whether this is an element capture or a component capture, and
        // if applicable the type name that will appear in the resulting capture code
        var referenceCapture = parent as ComponentIntermediateNode is { Component: { } componentTagHelper }
            ? new ReferenceCaptureIntermediateNode(identifierToken, componentTagHelper.TypeName)
            : new ReferenceCaptureIntermediateNode(identifierToken);

        // Generate field declaration if it doesn't already exist
        var fieldName = identifierToken.Content;
        if (!string.IsNullOrWhiteSpace(fieldName) && generatedFields.Add(fieldName))
        {
            // Check if a field/property with this name already exists in the class
            if (!FieldOrPropertyExists(classNode, fieldName))
            {
                AddFieldDeclaration(classNode, fieldName, referenceCapture.FieldTypeName);
            }
        }

        reference.Replace(referenceCapture);
    }

    private static bool FieldOrPropertyExists(ClassDeclarationIntermediateNode classNode, string name)
    {
        foreach (var child in classNode.Children)
        {
            if (child is FieldDeclarationIntermediateNode field && field.Name == name)
            {
                return true;
            }
            if (child is PropertyDeclarationIntermediateNode property && property.Name == name)
            {
                return true;
            }
        }
        return false;
    }

    private static void AddFieldDeclaration(ClassDeclarationIntermediateNode classNode, string fieldName, string fieldType)
    {
        // Find the insertion point: after any existing fields and design-time setup code
        var children = classNode.Children;
        var index = 0;

        // Skip past design-time directives and initial CSharpCode blocks (like #pragma warning)
        while (index < children.Count && 
               (children[index] is DesignTimeDirectiveIntermediateNode ||
                (children[index] is CSharpCodeIntermediateNode code && 
                 code.Children.OfType<IntermediateToken>().Any(t => t.Content.Contains("#pragma") || t.Content.Contains("__o")))))
        {
            index++;
        }

        // Skip past any existing field declarations to maintain ordering
        while (index < children.Count && children[index] is FieldDeclarationIntermediateNode)
        {
            index++;
        }

        children.Insert(index, new FieldDeclarationIntermediateNode()
        {
            Modifiers = CommonModifiers.Private,
            Name = fieldName,
            Type = fieldType,
        });
    }

    private static IntermediateToken? DetermineIdentifierToken(TagHelperDirectiveAttributeIntermediateNode attributeNode)
    {
        var foundToken = attributeNode.Children switch
        {
            [IntermediateToken token] => token,
            [CSharpExpressionIntermediateNode { Children: [IntermediateToken token] }] => token,
            _ => null,
        };

        if (foundToken is null)
        {
            return null;
        }

        return !foundToken.Content.IsNullOrWhiteSpace()
            ? foundToken
            : null;
    }
}
