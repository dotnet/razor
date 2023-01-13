﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Xunit;

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

// Basic Tests for C# Statements:
//  * Basic case for each statement
//  * Basic case for ALL clauses

// This class DOES NOT contain
//  * Error cases
//  * Tests for various types of nested statements
//  * Comment tests

public class CSharpStatementTest : ParserTestBase
{
    [Fact]
    public void ForStatement()
    {
        ParseDocumentTest("@for(int i = 0; i++; i < length) { foo(); }");
    }

    [Fact]
    public void ForEachStatement()
    {
        ParseDocumentTest("@foreach(var foo in bar) { foo(); }");
    }

    [Fact]
    public void AwaitForEachStatement()
    {
        ParseDocumentTest("@await foreach(var foo in bar) { foo(); }");
    }

    [Fact]
    public void MalformedAwaitForEachStatement()
    {
        ParseDocumentTest("@await foreach(var foo in bar { foo(); ");
    }

    [Fact]
    public void WhileStatement()
    {
        ParseDocumentTest("@while(true) { foo(); }");
    }

    [Fact]
    public void SwitchStatement()
    {
        ParseDocumentTest("@switch(foo) { foo(); }");
    }

    [Fact]
    public void SwitchStatement_RecursivePattern()
    {
        ParseDocumentTest(@"
@switch (DateTimeOffset.UtcNow)
{
    case { Year: 2022 }:
        <strong>Property expressions are supported by the razor syntax in the year 2022.</strong>
        break;
}
");
    }

    [Fact]
    public void SwitchStatement_TuplePattern()
    {
        ParseDocumentTest(@"
@switch ((1, 2))
{
    case ITuple (1, 2):
        <strong>Property expressions are supported by the razor syntax in the year 2022.</strong>
        break;
}
");
    }

    [Fact]
    public void SwitchStatement_ListPattern()
    {
        ParseDocumentTest(@"
@switch (new int[0])
{
    case [.., 3, 4]:
        <strong>Property expressions are supported by the razor syntax in the year 2022.</strong>
        break;
}
");
    }

    [Fact]
    public void SwitchStatement_DoubleColonQualifiedTypeName()
    {
        ParseDocumentTest(@"
@switch (new int[0])
{
    case global::Test:
        <strong>Property expressions are supported by the razor syntax in the year 2022.</strong>
        break;
}
");
    }

    [Fact]
    public void LockStatement()
    {
        ParseDocumentTest("@lock(baz) { foo(); }");
    }

    [Fact]
    public void IfStatement()
    {
        ParseDocumentTest("@if(true) { foo(); }");
    }

    [Fact]
    public void ElseIfClause()
    {
        ParseDocumentTest("@if(true) { foo(); } else if(false) { foo(); } else if(!false) { foo(); }");
    }

    [Fact]
    public void ElseClause()
    {
        ParseDocumentTest("@if(true) { foo(); } else { foo(); }");
    }

    [Fact]
    public void TryStatement()
    {
        ParseDocumentTest("@try { foo(); }");
    }

    [Fact]
    public void CatchClause()
    {
        ParseDocumentTest("@try { foo(); } catch(IOException ioex) { handleIO(); } catch(Exception ex) { handleOther(); }");
    }

    [Fact]
    public void ExceptionFilter_TryCatchWhenComplete_SingleLine()
    {
        ParseDocumentTest("@try { someMethod(); } catch(Exception) when (true) { handleIO(); }");
    }

    [Fact]
    public void ExceptionFilter_TryCatchWhenFinallyComplete_SingleLine()
    {
        ParseDocumentTest("@try { A(); } catch(Exception) when (true) { B(); } finally { C(); }");
    }

    [Fact]
    public void ExceptionFilter_TryCatchWhenCatchWhenComplete_SingleLine()
    {
        ParseDocumentTest("@try { A(); } catch(Exception) when (true) { B(); } catch(IOException) when (false) { C(); }");
    }

    [Fact]
    public void ExceptionFilter_MultiLine()
    {
        ParseDocumentTest(
@"@try
{
A();
}
catch(Exception) when (true)
{
B();
}
catch(IOException) when (false)
{
C();
}");
    }

    [Fact]
    public void ExceptionFilter_NestedTryCatchWhen()
    {
        ParseDocumentTest("@{try { someMethod(); } catch(Exception) when (true) { handleIO(); }}");
    }

    [Fact]
    public void ExceptionFilter_IncompleteTryCatchWhen()
    {
        ParseDocumentTest("@try { someMethod(); } catch(Exception) when");
    }

    [Fact]
    public void ExceptionFilter_IncompleteTryWhen()
    {
        ParseDocumentTest("@try { someMethod(); } when");
    }

    [Fact]
    public void ExceptionFilter_IncompleteTryCatchNoBodyWhen()
    {
        ParseDocumentTest("@try { someMethod(); } catch(Exception) when { anotherMethod(); }");
    }

    [Fact]
    public void ExceptionFilter_IncompleteTryCatchWhenNoBodies()
    {
        ParseDocumentTest("@try { someMethod(); } catch(Exception) when (true)");
    }

    [Fact]
    public void ExceptionFilterError_TryCatchWhen_InCompleteCondition()
    {
        ParseDocumentTest("@try { someMethod(); } catch(Exception) when (");
    }

    [Fact]
    public void ExceptionFilterError_TryCatchWhen_InCompleteBody()
    {
        ParseDocumentTest("@try { someMethod(); } catch(Exception) when (true) {");
    }

    [Fact]
    public void FinallyClause()
    {
        ParseDocumentTest("@try { foo(); } finally { Dispose(); }");
    }

    [Fact]
    public void Using_VariableDeclaration_Simple()
    {
        ParseDocumentTest("@{ using var foo = someDisposable; }");
    }

    [Fact]
    public void Using_VariableDeclaration_Complex()
    {
        ParseDocumentTest("@{ using Some.Disposable.TypeName foo = GetDisposable<Some.Disposable.TypeName>(() => { using var bar = otherDisposable; }); }");
    }

    [Fact]
    public void StaticUsing_NoUsing()
    {
        ParseDocumentTest("@using static");
    }

    [Fact]
    public void StaticUsing_SingleIdentifier()
    {
        ParseDocumentTest("@using static System");
    }

    [Fact]
    public void StaticUsing_MultipleIdentifiers()
    {
        ParseDocumentTest("@using static System.Console");
    }

    [Fact]
    public void StaticUsing_GlobalPrefix()
    {
        ParseDocumentTest("@using static global::System.Console");
    }

    [Fact]
    public void StaticUsing_Complete_Spaced()
    {
        ParseDocumentTest("@using   static   global::System.Console  ");
    }

    [Fact]
    public void UsingStatement()
    {
        ParseDocumentTest("@using(var foo = new Foo()) { foo.Bar(); }");
    }

    [Fact]
    public void UsingTypeAlias()
    {
        ParseDocumentTest("@using StringDictionary = System.Collections.Generic.Dictionary<string, string>");
    }

    [Fact]
    public void UsingNamespaceImport()
    {
        ParseDocumentTest("@using System.Text.Encoding.ASCIIEncoding");
    }

    [Fact]
    public void DoStatement()
    {
        ParseDocumentTest("@do { foo(); } while(true);");
    }

    [Fact]
    public void NonBlockKeywordTreatedAsImplicitExpression()
    {
        ParseDocumentTest("@is foo");
    }
}
