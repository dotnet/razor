﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.CodeAnalysis.Razor.Tooltip;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Editor.Razor.Completion;

public class RazorDirectiveAttributeCompletionSourceTest(ITestOutputHelper testOutput) : ProjectSnapshotManagerDispatcherTestBase(testOutput)
{
    [Fact]
    public async Task GetDescriptionAsync_NoDescriptionData_ReturnsEmptyString()
    {
        // Arrange
        var source = CreateCompletionSource();
        var completionSessionSource = Mock.Of<IAsyncCompletionSource>(MockBehavior.Strict);
        var completionItem = new CompletionItem("@random", completionSessionSource);

        // Act
        var result = await source.GetDescriptionAsync(session: null, completionItem, DisposalToken);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GetDescriptionAsync_DescriptionData_AsksFactoryForDescription()
    {
        // Arrange
        var expectedResult = new ContainerElement(ContainerElementStyle.Wrapped);
        var description = AggregateBoundAttributeDescription.Empty;
        var descriptionFactory = Mock.Of<IVisualStudioDescriptionFactory>(factory => factory.CreateClassifiedDescription(description) == expectedResult, MockBehavior.Strict);
        var source = new RazorDirectiveAttributeCompletionSource(
            Dispatcher,
            Mock.Of<VisualStudioRazorParser>(MockBehavior.Strict),
            Mock.Of<IRazorCompletionFactsService>(MockBehavior.Strict),
            Mock.Of<ICompletionBroker>(MockBehavior.Strict),
            descriptionFactory,
            JoinableTaskFactory);
        var completionSessionSource = Mock.Of<IAsyncCompletionSource>(MockBehavior.Strict);
        var completionItem = new CompletionItem("@random", completionSessionSource);
        completionItem.Properties.AddProperty(RazorDirectiveAttributeCompletionSource.DescriptionKey, description);

        // Act
        var result = await source.GetDescriptionAsync(session: null, completionItem, DisposalToken);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void InitializeCompletion_PageDirective_ReturnsParticipateWithCorrectSpan()
    {
        // Arrange
        var source = CreateCompletionSource();
        var snapshot = new StringTextSnapshot("@page");
        var trigger = new CompletionTrigger(CompletionTriggerReason.Invoke, snapshot);
        var triggerLocation = new SnapshotPoint(snapshot, 1);
        var expectedApplicableToSpan = new SnapshotSpan(snapshot, new Span(1, 4));

        // Act
        var result = source.InitializeCompletion(trigger, triggerLocation, DisposalToken);

        // Assert
        Assert.Equal(expectedApplicableToSpan, result.ApplicableToSpan);
    }

    [Theory]
    [InlineData("@Value[0]")]
    [InlineData("@DateTime.Now")]
    [InlineData("@SomeMethod()")]
    [InlineData("@(DateTime.Now)")]
    [InlineData("@{SomeProperty;}")]
    public void InitializeCompletion_InvalidCharactersInExpressions_ReturnsDoesNotParticipate(string expression)
    {
        // Arrange
        var source = CreateCompletionSource();
        var snapshot = new StringTextSnapshot(expression);
        var trigger = new CompletionTrigger(CompletionTriggerReason.Invoke, snapshot);
        var triggerLocation = new SnapshotPoint(snapshot, 1);

        // Act
        var result = source.InitializeCompletion(trigger, triggerLocation, DisposalToken);

        // Assert
        Assert.Equal(CompletionStartData.DoesNotParticipateInCompletion, result);
    }

    [Fact]
    public void InitializeCompletion_SingleTransition_ReturnsDoesNotParticipate()
    {
        // Arrange
        var source = CreateCompletionSource();
        var snapshot = new StringTextSnapshot("@");
        var trigger = new CompletionTrigger(CompletionTriggerReason.Invoke, snapshot);
        var triggerLocation = new SnapshotPoint(snapshot, 1);

        // Act
        var result = source.InitializeCompletion(trigger, triggerLocation, DisposalToken);

        // Assert
        Assert.Equal(CompletionStartData.DoesNotParticipateInCompletion, result);
    }

    [Fact]
    public void InitializeCompletion_CSSEscapedTransition_ReturnsDoesNotParticipate()
    {
        // Arrange
        var source = CreateCompletionSource();
        var snapshot = new StringTextSnapshot("<style>@@</style");
        var trigger = new CompletionTrigger(CompletionTriggerReason.Invoke, snapshot);
        var triggerLocation = new SnapshotPoint(snapshot, 9);

        // Act
        var result = source.InitializeCompletion(trigger, triggerLocation, DisposalToken);

        // Assert
        Assert.Equal(CompletionStartData.DoesNotParticipateInCompletion, result);
    }

    [Fact]
    public void InitializeCompletion_EmptySnapshot_ReturnsDoesNotParticipate()
    {
        // Arrange
        var source = CreateCompletionSource();
        var emptySnapshot = new StringTextSnapshot(string.Empty);
        var trigger = new CompletionTrigger(CompletionTriggerReason.Invoke, emptySnapshot);
        var triggerLocation = new SnapshotPoint(emptySnapshot, 0);

        // Act
        var result = source.InitializeCompletion(trigger, triggerLocation, DisposalToken);

        // Assert
        Assert.Equal(CompletionStartData.DoesNotParticipateInCompletion, result);
    }

    [Fact]
    public void InitializeCompletion_TriggeredAtStartOfDocument_ReturnsDoesNotParticipate()
    {
        // Arrange
        var source = CreateCompletionSource();
        var snapshot = new StringTextSnapshot("<p class='foo'></p>");
        var trigger = new CompletionTrigger(CompletionTriggerReason.Invoke, snapshot);
        var triggerLocation = new SnapshotPoint(snapshot, 0);

        // Act
        var result = source.InitializeCompletion(trigger, triggerLocation, DisposalToken);

        // Assert
        Assert.Equal(CompletionStartData.DoesNotParticipateInCompletion, result);
    }

    [Fact]
    public void InitializeCompletion_TriggeredAtInvalidLocation_ReturnsDoesNotParticipate()
    {
        // Arrange
        var source = CreateCompletionSource();
        var snapshot = new StringTextSnapshot("<p class='foo'></p>");
        var trigger = new CompletionTrigger(CompletionTriggerReason.Invoke, snapshot);

        // Act & Assert
        for (var i = 0; i < snapshot.Length; i++)
        {
            var triggerLocation = new SnapshotPoint(snapshot, i);
            var result = source.InitializeCompletion(trigger, triggerLocation, DisposalToken);
            Assert.Equal(CompletionStartData.DoesNotParticipateInCompletion, result);
        }
    }

    [Fact]
    public void InitializeCompletion_TriggeredAtPossibleDirectiveAttribute_ReturnsParticipate()
    {
        // Arrange
        var source = CreateCompletionSource();
        var snapshot = new StringTextSnapshot("<input @bind='@foo' />");
        var trigger = new CompletionTrigger(CompletionTriggerReason.Invoke, snapshot);
        var triggerLocation = new SnapshotPoint(snapshot, 9);
        var expectedApplicableToSpan = new SnapshotSpan(snapshot, new Span(8, 4));

        // Act
        var result = source.InitializeCompletion(trigger, triggerLocation, DisposalToken);

        // Assert
        Assert.Equal(expectedApplicableToSpan, result.ApplicableToSpan);
    }

    [Fact]
    public void InitializeCompletion_TriggeredAtPossibleDirectiveWithAttributeParameter_ReturnsParticipate()
    {
        // Arrange
        var source = CreateCompletionSource();
        var snapshot = new StringTextSnapshot("<input @bind:format='@foo' />");
        var trigger = new CompletionTrigger(CompletionTriggerReason.Invoke, snapshot);
        var triggerLocation = new SnapshotPoint(snapshot, 9);
        var expectedApplicableToSpan = new SnapshotSpan(snapshot, new Span(8, 4));

        // Act
        var result = source.InitializeCompletion(trigger, triggerLocation, DisposalToken);

        // Assert
        Assert.Equal(expectedApplicableToSpan, result.ApplicableToSpan);
    }

    [Fact]
    public void InitializeCompletion_TriggeredAtPossibleDirectiveAttributeParameter_ReturnsParticipate()
    {
        // Arrange
        var source = CreateCompletionSource();
        var snapshot = new StringTextSnapshot("<input @bind:format='@foo' />");
        var trigger = new CompletionTrigger(CompletionTriggerReason.Invoke, snapshot);
        var triggerLocation = new SnapshotPoint(snapshot, 13);
        var expectedApplicableToSpan = new SnapshotSpan(snapshot, new Span(13, 6));

        // Act
        var result = source.InitializeCompletion(trigger, triggerLocation, DisposalToken);

        // Assert
        Assert.Equal(expectedApplicableToSpan, result.ApplicableToSpan);
    }

    private RazorDirectiveAttributeCompletionSource CreateCompletionSource()
    {
        var source = new RazorDirectiveAttributeCompletionSource(
            Dispatcher,
            Mock.Of<VisualStudioRazorParser>(MockBehavior.Strict),
            Mock.Of<IRazorCompletionFactsService>(MockBehavior.Strict),
            Mock.Of<ICompletionBroker>(MockBehavior.Strict),
            Mock.Of<IVisualStudioDescriptionFactory>(MockBehavior.Strict),
            JoinableTaskFactory);
        return source;
    }
}
