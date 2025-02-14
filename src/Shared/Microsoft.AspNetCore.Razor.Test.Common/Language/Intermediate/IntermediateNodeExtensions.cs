// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

internal static class IntermediateNodeExtensions
{
    public static NamespaceDeclarationIntermediateNode? FindNamespaceNode(this IntermediateNode node)
    {
        var visitor = new Visitor();
        visitor.Visit(node);

        return visitor.NamespaceNode;
    }

    public static ClassDeclarationIntermediateNode? FindClassNode(this IntermediateNode node)
    {
        var visitor = new Visitor();
        visitor.Visit(node);

        return visitor.ClassNode;
    }

    public static MethodDeclarationIntermediateNode? FindMethodNode(this IntermediateNode node)
    {
        var visitor = new Visitor();
        visitor.Visit(node);

        return visitor.MethodNode;
    }

    public static ExtensionIntermediateNode? FindExtensionNode(this IntermediateNode node)
    {
        var visitor = new Visitor();
        visitor.Visit(node);

        return visitor.ExtensionNode;
    }

    private sealed class Visitor : IntermediateNodeWalker
    {
        public NamespaceDeclarationIntermediateNode? NamespaceNode { get; private set; }
        public ClassDeclarationIntermediateNode? ClassNode { get; private set; }
        public MethodDeclarationIntermediateNode? MethodNode { get; private set; }
        public ExtensionIntermediateNode? ExtensionNode { get; private set; }

        public override void VisitMethodDeclaration(MethodDeclarationIntermediateNode node)
        {
            MethodNode = node;
        }

        public override void VisitNamespaceDeclaration(NamespaceDeclarationIntermediateNode node)
        {
            NamespaceNode = node;
            base.VisitNamespaceDeclaration(node);
        }

        public override void VisitClassDeclaration(ClassDeclarationIntermediateNode node)
        {
            ClassNode = node;
            base.VisitClassDeclaration(node);
        }

        public override void VisitExtension(ExtensionIntermediateNode node)
        {
            ExtensionNode = node;
        }
    }
}
