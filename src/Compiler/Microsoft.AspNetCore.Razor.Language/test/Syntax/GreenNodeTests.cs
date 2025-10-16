// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language.Syntax;

public class GreenNodeTests
{
    [Fact]
    public void GetEnumerator_EmptyNode_ReturnsNodeAndToken()
    {
        // Tree structure:
        //   MarkupTextLiteral (node)
        //   └── Text: "" (token)

        // Arrange
        var token = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "");
        var node = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token);

        // Act
        var enumerator = node.GetEnumerator();
        var elements = new List<GreenNode>();

        while (enumerator.MoveNext())
        {
            elements.Add(enumerator.Current);
        }

        // Assert
        Assert.Equal(2, elements.Count);
        Assert.Same(node, elements[0]);  // Node first
        Assert.Same(token, elements[1]); // Then token
    }

    [Fact]
    public void GetEnumerator_SingleNode_ReturnsNodeAndToken()
    {
        // Tree structure:
        //   MarkupTextLiteral (node)
        //   └── Text: "Hello" (token)

        // Arrange
        var token = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "Hello");
        var node = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token);

        // Act
        var enumerator = node.GetEnumerator();
        var elements = new List<GreenNode>();

        while (enumerator.MoveNext())
        {
            elements.Add(enumerator.Current);
        }

        // Assert
        Assert.Equal(2, elements.Count);
        Assert.Same(node, elements[0]);  // Node first
        Assert.Same(token, elements[1]); // Then token
    }

    [Fact]
    public void GetEnumerator_NodeWithChildren_PerformsDepthFirstTraversal()
    {
        // Tree structure:
        //   GenericBlock (root)
        //   ├── MarkupTextLiteral (child1)
        //   │   └── Text: "Hello" (token1)
        //   ├── MarkupTextLiteral (child2)
        //   │   └── Whitespace: " " (token2)
        //   └── GenericBlock (child3)
        //       └── MarkupTextLiteral (grandchild)
        //           └── Text: "World" (token3)

        // Arrange
        var token1 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "Hello");
        var child1 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token1);

        var token2 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Whitespace, " ");
        var child2 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token2);

        var token3 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "World");
        var grandchild = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token3);

        var child3 = InternalSyntax.SyntaxFactory.GenericBlock(grandchild);

        var root = InternalSyntax.SyntaxFactory.GenericBlock([child1, child2, child3]);

        // Act
        var enumerator = root.GetEnumerator();
        var elements = new List<GreenNode>();

        while (enumerator.MoveNext())
        {
            elements.Add(enumerator.Current);
        }

        // Assert
        Assert.Equal(8, elements.Count);
        Assert.Same(root, elements[0]);       // Root visited first
        Assert.Same(child1, elements[1]);     // First child
        Assert.Same(token1, elements[2]);     // First child's token
        Assert.Same(child2, elements[3]);     // Second child
        Assert.Same(token2, elements[4]);     // Second child's token
        Assert.Same(child3, elements[5]);     // Third child (parent of grandchild)
        Assert.Same(grandchild, elements[6]); // Grandchild visited after its parent
        Assert.Same(token3, elements[7]);     // Grandchild's token
    }

    [Fact]
    public void GetEnumerator_ComplexTree_MaintainsDepthFirstOrder()
    {
        // Tree structure:
        //   GenericBlock (root)
        //   ├── MarkupTextLiteral (child1)
        //   │   └── Whitespace: " " (token1)
        //   └── GenericBlock (child2)
        //       ├── MarkupTextLiteral (grandchild1)
        //       │   └── Text: "A" (token2)
        //       └── MarkupTextLiteral (grandchild2)
        //           └── Text: "B" (token3)

        // Arrange
        var token1 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Whitespace, " ");
        var child1 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token1);

        var token2 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "A");
        var grandchild1 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token2);

        var token3 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "B");
        var grandchild2 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token3);

        var child2 = InternalSyntax.SyntaxFactory.GenericBlock([grandchild1, grandchild2]);

        var root = InternalSyntax.SyntaxFactory.GenericBlock([child1, child2]);

        // Act
        var enumerator = root.GetEnumerator();
        var elements = new List<GreenNode>();

        while (enumerator.MoveNext())
        {
            elements.Add(enumerator.Current);
        }

        // Assert
        Assert.Equal(8, elements.Count);
        Assert.Same(root, elements[0]);
        Assert.Same(child1, elements[1]);
        Assert.Same(token1, elements[2]);     // child1's token
        Assert.Same(child2, elements[3]);
        Assert.Same(grandchild1, elements[4]);
        Assert.Same(token2, elements[5]);     // grandchild1's token
        Assert.Same(grandchild2, elements[6]);
        Assert.Same(token3, elements[7]);     // grandchild2's token
    }

    [Fact]
    public void GetEnumerator_CanBeUsedInForeachLoop()
    {
        // Tree structure:
        //   GenericBlock (root)
        //   ├── MarkupTextLiteral (child1)
        //   │   └── Text: "Hello" (token1)
        //   └── MarkupTextLiteral (child2)
        //       └── Text: "World" (token2)

        // Arrange
        var token1 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "Hello");
        var child1 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token1);

        var token2 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "World");
        var child2 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token2);

        var root = InternalSyntax.SyntaxFactory.GenericBlock([child1, child2]);

        // Act
        var elements = new List<GreenNode>();
        foreach (var node in root)
        {
            elements.Add(node);
        }

        // Assert
        Assert.Equal(5, elements.Count);
        Assert.Same(root, elements[0]);
        Assert.Same(child1, elements[1]);
        Assert.Same(token1, elements[2]);     // child1's token
        Assert.Same(child2, elements[3]);
        Assert.Same(token2, elements[4]);     // child2's token
    }

    [Fact]
    public void GetEnumerator_MultipleEnumerators_AreIndependent()
    {
        // Tree structure:
        //   GenericBlock (root)
        //   └── MarkupTextLiteral (child)
        //       └── Text: "Test" (token)

        // Arrange
        var token = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "Test");
        var child = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token);

        var root = InternalSyntax.SyntaxFactory.GenericBlock(child);

        // Act
        var enumerator1 = root.GetEnumerator();
        var enumerator2 = root.GetEnumerator();

        var hasNext1 = enumerator1.MoveNext();
        var hasNext2 = enumerator2.MoveNext();

        // Assert
        Assert.True(hasNext1);
        Assert.True(hasNext2);
        Assert.Same(root, enumerator1.Current);
        Assert.Same(root, enumerator2.Current);
    }

    [Fact]
    public void GetEnumerator_TokenNode_ReturnsSelfOnly()
    {
        // Tree structure:
        //   Text: "Hello" (token)

        // Arrange
        var token = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "Hello");

        // Act
        var enumerator = token.GetEnumerator();
        var elements = new List<GreenNode>();

        while (enumerator.MoveNext())
        {
            elements.Add(enumerator.Current);
        }

        // Assert
        Assert.Single(elements);
        Assert.Same(token, elements[0]);
    }

    [Fact]
    public void GetEnumerator_MixedMarkupAndCode_PerformsDepthFirstTraversal()
    {
        // Tree structure:
        //   GenericBlock (root)
        //   ├── MarkupTextLiteral (htmlNode)
        //   │   └── Text: "<div>" (htmlToken)
        //   ├── CSharpTransition (transitionNode)
        //   │   └── Transition: "@" (transitionToken)
        //   └── CSharpExpressionLiteral (codeNode)
        //       └── Identifier: "Model" (codeToken)

        // Arrange
        var htmlToken = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "<div>");
        var htmlNode = InternalSyntax.SyntaxFactory.MarkupTextLiteral(htmlToken);

        var transitionToken = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Transition, "@");
        var transitionNode = InternalSyntax.SyntaxFactory.CSharpTransition(transitionToken);

        var codeToken = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Identifier, "Model");
        var codeNode = InternalSyntax.SyntaxFactory.CSharpExpressionLiteral(codeToken);

        var root = InternalSyntax.SyntaxFactory.GenericBlock([htmlNode, transitionNode, codeNode]);

        // Act
        var enumerator = root.GetEnumerator();
        var elements = new List<GreenNode>();

        while (enumerator.MoveNext())
        {
            elements.Add(enumerator.Current);
        }

        // Assert
        Assert.Equal(7, elements.Count);
        Assert.Same(root, elements[0]);
        Assert.Same(htmlNode, elements[1]);
        Assert.Same(htmlToken, elements[2]);        // htmlNode's token
        Assert.Same(transitionNode, elements[3]);
        Assert.Same(transitionToken, elements[4]);  // transitionNode's token
        Assert.Same(codeNode, elements[5]);
        Assert.Same(codeToken, elements[6]);        // codeNode's token
    }

    [Fact]
    public void GetEnumerator_EnumeratesTokensAndNodes_InDepthFirstOrder()
    {
        // Tree structure:
        //   MarkupTextLiteral (node)
        //   └── Text: "Test" (token)
        //
        // Note: This test demonstrates that both nodes and tokens are enumerated

        // Arrange
        var token = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "Test");
        var node = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token);

        // Act
        var enumerator = node.GetEnumerator();
        var elements = new List<GreenNode>();
        var nodeTypes = new List<bool>(); // true for node, false for token

        while (enumerator.MoveNext())
        {
            elements.Add(enumerator.Current);
            nodeTypes.Add(!enumerator.Current.IsToken);
        }

        // Assert
        Assert.Equal(2, elements.Count);
        Assert.Same(node, elements[0]);
        Assert.Same(token, elements[1]);
        Assert.True(nodeTypes[0]);   // First element is a node
        Assert.False(nodeTypes[1]);  // Second element is a token
    }
}
