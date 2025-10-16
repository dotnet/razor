// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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

    [Fact]
    public void Tokens_SingleToken_ReturnsOnlyToken()
    {
        // Tree structure:
        //   Text: "Hello" (token)

        // Arrange
        var token = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "Hello");

        // Act
        var tokens = new List<InternalSyntax.SyntaxToken>();
        foreach (var t in token.Tokens())
        {
            tokens.Add(t);
        }

        // Assert
        Assert.Single(tokens);
        Assert.Same(token, tokens[0]);
    }

    [Fact]
    public void Tokens_NodeWithSingleToken_ReturnsOnlyToken()
    {
        // Tree structure:
        //   MarkupTextLiteral (node)
        //   └── Text: "Hello" (token)

        // Arrange
        var token = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "Hello");
        var node = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token);

        // Act
        var tokens = new List<InternalSyntax.SyntaxToken>();
        foreach (var t in node.Tokens())
        {
            tokens.Add(t);
        }

        // Assert
        Assert.Single(tokens);
        Assert.Same(token, tokens[0]);
    }

    [Fact]
    public void Tokens_ComplexTree_ReturnsOnlyTokensInOrder()
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
        var tokens = new List<InternalSyntax.SyntaxToken>();
        foreach (var token in root.Tokens())
        {
            tokens.Add(token);
        }

        // Assert
        Assert.Equal(3, tokens.Count);
        Assert.Same(token1, tokens[0]);  // "Hello"
        Assert.Same(token2, tokens[1]);  // " "
        Assert.Same(token3, tokens[2]);  // "World"
    }

    [Fact]
    public void Tokens_MixedMarkupAndCode_ReturnsAllTokensInDepthFirstOrder()
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
        var tokens = new List<InternalSyntax.SyntaxToken>();
        foreach (var token in root.Tokens())
        {
            tokens.Add(token);
        }

        // Assert
        Assert.Equal(3, tokens.Count);
        Assert.Same(htmlToken, tokens[0]);      // "<div>"
        Assert.Same(transitionToken, tokens[1]); // "@"
        Assert.Same(codeToken, tokens[2]);      // "Model"
    }

    [Fact]
    public void Tokens_EmptyToken_ReturnsEmptyToken()
    {
        // Tree structure:
        //   MarkupTextLiteral (node)
        //   └── Text: "" (token)

        // Arrange
        var token = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "");
        var node = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token);

        // Act
        var tokens = new List<InternalSyntax.SyntaxToken>();
        foreach (var t in node.Tokens())
        {
            tokens.Add(t);
        }

        // Assert
        Assert.Single(tokens);
        Assert.Same(token, tokens[0]);
        Assert.Equal("", tokens[0].Content);
    }

    [Fact]
    public void Tokens_CanBeEnumeratedMultipleTimes()
    {
        // Tree structure:
        //   GenericBlock (root)
        //   ├── MarkupTextLiteral (child1)
        //   │   └── Text: "A" (token1)
        //   └── MarkupTextLiteral (child2)
        //       └── Text: "B" (token2)

        // Arrange
        var token1 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "A");
        var child1 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token1);

        var token2 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "B");
        var child2 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token2);

        var root = InternalSyntax.SyntaxFactory.GenericBlock([child1, child2]);

        // Act - enumerate twice
        var firstEnumeration = new List<InternalSyntax.SyntaxToken>();
        foreach (var token in root.Tokens())
        {
            firstEnumeration.Add(token);
        }

        var secondEnumeration = new List<InternalSyntax.SyntaxToken>();
        foreach (var token in root.Tokens())
        {
            secondEnumeration.Add(token);
        }

        // Assert
        Assert.Equal(2, firstEnumeration.Count);
        Assert.Equal(2, secondEnumeration.Count);
        
        Assert.Same(token1, firstEnumeration[0]);
        Assert.Same(token2, firstEnumeration[1]);
        
        Assert.Same(token1, secondEnumeration[0]);
        Assert.Same(token2, secondEnumeration[1]);
    }

    [Fact]
    public void Tokens_WithManualEnumerator_WorksCorrectly()
    {
        // Tree structure:
        //   MarkupTextLiteral (node)
        //   └── Text: "Test" (token)

        // Arrange
        var token = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "Test");
        var node = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token);

        // Act
        var tokenEnumerable = node.Tokens();
        var enumerator = tokenEnumerable.GetEnumerator();
        
        var tokens = new List<InternalSyntax.SyntaxToken>();
        while (enumerator.MoveNext())
        {
            tokens.Add(enumerator.Current);
        }

        // Assert
        Assert.Single(tokens);
        Assert.Same(token, tokens[0]);
    }

    [Fact]
    public void Tokens_FilterOutNodesAndKeepOnlyTokens()
    {
        // Tree structure:
        //   GenericBlock (root) <- filtered out
        //   └── MarkupTextLiteral (child) <- filtered out
        //       └── Text: "OnlyThis" (token) <- kept

        // Arrange
        var token = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "OnlyThis");
        var child = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token);
        var root = InternalSyntax.SyntaxFactory.GenericBlock(child);

        // Act
        var tokens = new List<InternalSyntax.SyntaxToken>();
        foreach (var t in root.Tokens())
        {
            tokens.Add(t);
        }

        // Assert
        Assert.Single(tokens);
        Assert.Same(token, tokens[0]);
        Assert.Equal("OnlyThis", tokens[0].Content);
        Assert.True(tokens[0].IsToken);
    }

    [Fact]
    public void TokenEnumerable_Enumerator_Current_ThrowsBeforeFirstMoveNext()
    {
        // Arrange
        var token = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "Test");
        var node = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token);
        var enumerator = node.Tokens().GetEnumerator();

        // Act & Assert
        try
        {
            _ = enumerator.Current;
        }
        catch (Exception ex)
        {
            // Note: We can't use Assert.Throws because enumerator is a ref-struct
            // and can't be captured in a lambda.
            Assert.IsType<NullReferenceException>(ex);
        }
    }

    [Fact]
    public void TokenEnumerable_Enumerator_Current_ReturnsCorrectToken()
    {
        // Tree structure:
        //   GenericBlock (root)
        //   ├── MarkupTextLiteral (child1)
        //   │   └── Text: "First" (token1)
        //   └── MarkupTextLiteral (child2)
        //       └── Text: "Second" (token2)

        // Arrange
        var token1 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "First");
        var child1 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token1);

        var token2 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "Second");
        var child2 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token2);

        var root = InternalSyntax.SyntaxFactory.GenericBlock([child1, child2]);
        var enumerator = root.Tokens().GetEnumerator();

        // Act & Assert
        Assert.True(enumerator.MoveNext());
        Assert.Same(token1, enumerator.Current);
        Assert.Equal("First", enumerator.Current.Content);

        Assert.True(enumerator.MoveNext());
        Assert.Same(token2, enumerator.Current);
        Assert.Equal("Second", enumerator.Current.Content);

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void ToString_SingleToken_ReturnsTokenContent()
    {
        // Tree structure:
        //   Text: "Hello" (token)

        // Arrange
        var token = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "Hello");

        // Act
        var result = token.ToString();

        // Assert
        Assert.Equal("Hello", result);
    }

    [Fact]
    public void ToString_EmptyToken_ReturnsEmptyString()
    {
        // Tree structure:
        //   Text: "" (token)

        // Arrange
        var token = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "");

        // Act
        var result = token.ToString();

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void ToString_NodeWithSingleToken_ReturnsTokenContent()
    {
        // Tree structure:
        //   MarkupTextLiteral (node)
        //   └── Text: "Hello World" (token)

        // Arrange
        var token = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "Hello World");
        var node = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token);

        // Act
        var result = node.ToString();

        // Assert
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void ToString_ComplexTree_ConcatenatesAllTokensInDepthFirstOrder()
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
        var result = root.ToString();

        // Assert
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void ToString_MixedMarkupAndCode_ConcatenatesAllTokenContent()
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
        var result = root.ToString();

        // Assert
        Assert.Equal("<div>@Model", result);
    }

    [Fact]
    public void ToString_MultipleNestedNodes_ConcatenatesInCorrectOrder()
    {
        // Tree structure:
        //   GenericBlock (root)
        //   ├── MarkupTextLiteral (child1)
        //   │   └── Text: "Start" (token1)
        //   └── GenericBlock (child2)
        //       ├── MarkupTextLiteral (grandchild1)
        //       │   └── Text: "Middle" (token2)
        //       └── MarkupTextLiteral (grandchild2)
        //           └── Text: "End" (token3)

        // Arrange
        var token1 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "Start");
        var child1 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token1);

        var token2 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "Middle");
        var grandchild1 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token2);

        var token3 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "End");
        var grandchild2 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token3);

        var child2 = InternalSyntax.SyntaxFactory.GenericBlock([grandchild1, grandchild2]);
        var root = InternalSyntax.SyntaxFactory.GenericBlock([child1, child2]);

        // Act
        var result = root.ToString();

        // Assert
        Assert.Equal("StartMiddleEnd", result);
    }

    [Fact]
    public void ToString_WithWhitespaceTokens_PreservesWhitespace()
    {
        // Tree structure:
        //   GenericBlock (root)
        //   ├── MarkupTextLiteral (child1)
        //   │   └── Text: "Hello" (token1)
        //   ├── MarkupTextLiteral (child2)
        //   │   └── Whitespace: "   " (token2)
        //   └── MarkupTextLiteral (child3)
        //       └── Text: "World" (token3)

        // Arrange
        var token1 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "Hello");
        var child1 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token1);

        var token2 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Whitespace, "   ");
        var child2 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token2);

        var token3 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "World");
        var child3 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token3);

        var root = InternalSyntax.SyntaxFactory.GenericBlock([child1, child2, child3]);

        // Act
        var result = root.ToString();

        // Assert
        Assert.Equal("Hello   World", result);
    }

    [Fact]
    public void ToString_WithSpecialCharacters_PreservesAllCharacters()
    {
        // Tree structure:
        //   GenericBlock (root)
        //   ├── MarkupTextLiteral (child1)
        //   │   └── Text: "Line1\n" (token1)
        //   └── MarkupTextLiteral (child2)
        //       └── Text: "Line2\t\r" (token2)

        // Arrange
        var token1 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "Line1\n");
        var child1 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token1);

        var token2 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "Line2\t\r");
        var child2 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token2);

        var root = InternalSyntax.SyntaxFactory.GenericBlock([child1, child2]);

        // Act
        var result = root.ToString();

        // Assert
        Assert.Equal("Line1\nLine2\t\r", result);
    }

    [Fact]
    public void ToString_WithUnicodeCharacters_PreservesUnicode()
    {
        // Tree structure:
        //   MarkupTextLiteral (node)
        //   └── Text: "Hello 🌍 World! ñáéíóú" (token)

        // Arrange
        var token = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "Hello 🌍 World! ñáéíóú");
        var node = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token);

        // Act
        var result = node.ToString();

        // Assert
        Assert.Equal("Hello 🌍 World! ñáéíóú", result);
    }

    [Fact]
    public void ToString_EmptyNodeWithEmptyTokens_ReturnsEmptyString()
    {
        // Tree structure:
        //   GenericBlock (root)
        //   ├── MarkupTextLiteral (child1)
        //   │   └── Text: "" (token1)
        //   └── MarkupTextLiteral (child2)
        //       └── Text: "" (token2)

        // Arrange
        var token1 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "");
        var child1 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token1);

        var token2 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "");
        var child2 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token2);

        var root = InternalSyntax.SyntaxFactory.GenericBlock([child1, child2]);

        // Act
        var result = root.ToString();

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void ToString_ComplexRazorExample_ConcatenatesCorrectly()
    {
        // Tree structure representing something like: "if (condition) { @Model.Name }"
        //   GenericBlock (root)
        //   ├── MarkupTextLiteral
        //   │   └── Text: "if (condition) { " (token1)
        //   ├── CSharpTransition
        //   │   └── Transition: "@" (token2)
        //   ├── CSharpExpressionLiteral
        //   │   └── Identifier: "Model" (token3)
        //   ├── MarkupTextLiteral
        //   │   └── Text: "." (token4)
        //   ├── CSharpExpressionLiteral
        //   │   └── Identifier: "Name" (token5)
        //   └── MarkupTextLiteral
        //       └── Text: " }" (token6)

        // Arrange
        var token1 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "if (condition) { ");
        var child1 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token1);

        var token2 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Transition, "@");
        var child2 = InternalSyntax.SyntaxFactory.CSharpTransition(token2);

        var token3 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Identifier, "Model");
        var child3 = InternalSyntax.SyntaxFactory.CSharpExpressionLiteral(token3);

        var token4 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, ".");
        var child4 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token4);

        var token5 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Identifier, "Name");
        var child5 = InternalSyntax.SyntaxFactory.CSharpExpressionLiteral(token5);

        var token6 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, " }");
        var child6 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token6);

        var root = InternalSyntax.SyntaxFactory.GenericBlock([child1, child2, child3, child4, child5, child6]);

        // Act
        var result = root.ToString();

        // Assert
        Assert.Equal("if (condition) { @Model.Name }", result);
    }

    [Fact]
    public void ToString_WidthMatchesStringLength()
    {
        // Tree structure:
        //   GenericBlock (root)
        //   ├── MarkupTextLiteral (child1)
        //   │   └── Text: "Hello" (token1)
        //   └── MarkupTextLiteral (child2)
        //       └── Text: " World!" (token2)

        // Arrange
        var token1 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, "Hello");
        var child1 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token1);

        var token2 = InternalSyntax.SyntaxFactory.Token(SyntaxKind.Text, " World!");
        var child2 = InternalSyntax.SyntaxFactory.MarkupTextLiteral(token2);

        var root = InternalSyntax.SyntaxFactory.GenericBlock([child1, child2]);

        // Act
        var result = root.ToString();

        // Assert
        Assert.Equal("Hello World!", result);
        Assert.Equal(result.Length, root.Width);
    }
}
