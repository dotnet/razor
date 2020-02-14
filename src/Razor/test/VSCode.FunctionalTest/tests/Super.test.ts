/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as assert from 'assert';
import { afterEach, before } from 'mocha';
import * as path from 'path';
import * as vscode from 'vscode';
import {
    assertHasCompletion,
    assertHasNoCompletion,
    mvcWithComponentsRoot,
    pollUntil,
    waitForDocumentUpdate,
    waitForProjectReady,
} from './TestUtil';

const homeDirectory = path.join(mvcWithComponentsRoot, 'Views', 'Home');

suite('SUPER', () => {
    before(async () => {
        await waitForProjectReady(mvcWithComponentsRoot);
    });

    afterEach(async () => {
        await vscode.commands.executeCommand('workbench.action.revertAndCloseActiveEditor');
        await pollUntil(async () => {
            await vscode.commands.executeCommand('workbench.action.closeAllEditors');
            if (vscode.window.visibleTextEditors.length === 0) {
                return true;
            }

            return false;
        }, /* timeout */ 3000, /* pollInterval */ 500, true /* suppress timeout */);
    });

    test('Can rename symbol within .razor', async () => {
        const razorPath = path.join(mvcWithComponentsRoot, 'Views', 'Shared', 'NavMenu.razor');
        const razorDoc = await vscode.workspace.openTextDocument(razorPath);
        const razorEditor = await vscode.window.showTextDocument(razorDoc);
        const expectedNewText = 'World';
        const firstLine = new vscode.Position(0, 0);
        await razorEditor.edit(edit => edit.insert(firstLine, '@hello\n'));
        await razorEditor.edit(edit => edit.insert(firstLine, '@{ var hello = "Hello"; }\n'));

        await new Promise(r => setTimeout(r, 3000));
        const renames = await vscode.commands.executeCommand<vscode.WorkspaceEdit>(
            'vscode.executeDocumentRenameProvider',
            razorDoc.uri,
            new vscode.Position(1, 2),
            expectedNewText);

        const entries = renames!.entries();
        assert.equal(entries.length, 1, 'Should only rename within the document.');
        const uri = entries[0][0];
        assert.equal(uri.path, razorDoc.uri.path);
        const edits = entries[0][1];
        assert.equal(edits.length, 2);
    });

    test('Can rename symbol within .cshtml', async () => {
        const cshtmlPath = path.join(mvcWithComponentsRoot, 'Views', 'Home', 'Index.cshtml');
        const cshtmlDoc = await vscode.workspace.openTextDocument(cshtmlPath);
        const cshtmlEditor = await vscode.window.showTextDocument(cshtmlDoc);
        const expectedNewText = 'World';
        const firstLine = new vscode.Position(0, 0);
        await cshtmlEditor.edit(edit => edit.insert(firstLine, '@hello\n'));
        await cshtmlEditor.edit(edit => edit.insert(firstLine, '@{ var hello = "Hello"; }\n'));

        await new Promise(r => setTimeout(r, 3000));
        const renames = await vscode.commands.executeCommand<vscode.WorkspaceEdit>(
            'vscode.executeDocumentRenameProvider',
            cshtmlDoc.uri,
            new vscode.Position(1, 2),
            expectedNewText);

        const entries = renames!.entries();
        assert.equal(entries.length, 1, 'Should only rename within the document.');
        const uri = entries[0][0];
        assert.equal(uri.path, cshtmlDoc.uri.path);
        const edits = entries[0][1];
        assert.equal(edits.length, 2);
    });

    test('Rename symbol in .razor also changes .cs', async () => {
        const razorPath = path.join(mvcWithComponentsRoot, 'Views', 'Shared', 'NavMenu.razor');
        const csPath = path.join(mvcWithComponentsRoot, 'Test.cs');
        const razorDoc = await vscode.workspace.openTextDocument(razorPath);
        const razorEditor = await vscode.window.showTextDocument(razorDoc);
        const expectedNewText = 'Oof';
        const firstLine = new vscode.Position(0, 0);
        await razorEditor.edit(edit => edit.insert(firstLine, '@Test.Bar\n'));

        await new Promise(r => setTimeout(r, 3000));
        const renames = await vscode.commands.executeCommand<vscode.WorkspaceEdit>(
            'vscode.executeDocumentRenameProvider',
            razorDoc.uri,
            new vscode.Position(0, 7),
            expectedNewText);

        const entries = renames!.entries();
        assert.equal(entries.length, 2, 'Should have renames in two documents.');

        // Razor file
        const uri1 = entries[0][0];
        assert.equal(uri1.path, vscode.Uri.file(csPath).path);
        const edits1 = entries[0][1];
        assert.equal(edits1.length, 1);

        // cs file
        const uri2 = entries[1][0];
        assert.equal(uri2.path, razorDoc.uri.path);
        const edits2 = entries[1][1];
        assert.equal(edits2.length, 1);
    });

    test('Rename symbol in .cs also changes .razor', async () => {
        const razorPath = path.join(mvcWithComponentsRoot, 'Views', 'Shared', 'NavMenu.razor');
        const csPath = path.join(mvcWithComponentsRoot, 'Test.cs');
        const expectedNewText = 'Oof';
        const csDoc = await vscode.workspace.openTextDocument(csPath);

        await new Promise(r => setTimeout(r, 3000));
        const renames = await vscode.commands.executeCommand<vscode.WorkspaceEdit>(
            'vscode.executeDocumentRenameProvider',
            csDoc.uri,
            new vscode.Position(4, 30), // Position `public static string F|oo { get; set; }`
            expectedNewText);

        const entries = renames!.entries();
        assert.equal(entries.length, 2, 'Should have renames in two documents.');

        // Razor file
        const uri1 = entries[0][0];
        assert.equal(uri1.path, csDoc.uri.path);
        const edits1 = entries[0][1];
        assert.equal(edits1.length, 1);

        // cs file
        const uri2 = entries[1][0];
        assert.equal(uri2.path, vscode.Uri.file(razorPath).path);
        const edits2 = entries[1][1];
        assert.equal(edits2.length, 1);
    });

    // ------- Code Actions -------------

    test('Can provide FullQualified CodeAction .razor file', async () => {
        const razorPath = path.join(mvcWithComponentsRoot, 'Views', 'Shared', 'NavMenu.razor');
        const razorDoc = await vscode.workspace.openTextDocument(razorPath);
        const firstLine = new vscode.Position(0, 0);
        await MakeEditAndFindDiagnostic('@{ var x = new HtmlString("sdf"); }\n', firstLine);

        const position = new vscode.Position(0, 21);
        const codeActions = await GetCodeActions(razorDoc.uri, new vscode.Range(position, position));

        assert.equal(codeActions.length, 1);
        const codeAction = codeActions[0];
        assert.equal(codeAction.title, 'Microsoft.AspNetCore.Html.HtmlString');

        await DoCodeAction(razorDoc.uri, codeAction);
        const reloadedDoc = await vscode.workspace.openTextDocument(razorDoc.uri);
        const editedText = reloadedDoc.getText();
        assert.ok(editedText.includes('var x = new Microsoft.AspNetCore.Html.HtmlString("sdf");'));
    });

    async function DoCodeAction(fileUri: vscode.Uri, codeAction: vscode.Command) {
        let diagnosticsChanged = false;
        vscode.languages.onDidChangeDiagnostics(diagnosticsChangedEvent => {
            const diagnostics = vscode.languages.getDiagnostics(fileUri);
            if (diagnostics.length === 0) {
                diagnosticsChanged = true;
            }
        });

        if (codeAction.command && codeAction.arguments) {
            const result = await vscode.commands.executeCommand<boolean | string>(codeAction.command, codeAction.arguments[0]);
            console.log(result);
        }

        await pollUntil(() => {
            return diagnosticsChanged;
        }, /* timeout */ 20000, /* pollInterval */ 1000, false /* suppress timeout */);
    }

    async function MakeEditAndFindDiagnostic(editText: string, position: vscode.Position) {
        const razorPath = path.join(mvcWithComponentsRoot, 'Views', 'Shared', 'NavMenu.razor');
        const razorDoc = await vscode.workspace.openTextDocument(razorPath);
        const razorEditor = await vscode.window.showTextDocument(razorDoc);
        let diagnosticsChanged = false;
        vscode.languages.onDidChangeDiagnostics(diagnosticsChangedEvent => {
            const diagnostics = vscode.languages.getDiagnostics(razorDoc.uri);
            if (diagnostics.length > 0) {
                diagnosticsChanged = true;
            }
        });

        for (let i = 0; i < 3; i++) {
            await razorEditor.edit(edit => edit.insert(position, editText));
            await pollUntil(() => {
                return diagnosticsChanged;
            }, /* timeout */ 5000, /* pollInterval */ 1000, true /* suppress timeout */);
            if (diagnosticsChanged) {
                break;
            }
        }
    }

    async function GetCodeActions(fileUri: vscode.Uri, position: vscode.Range): Promise<vscode.Command[]> {
        return await vscode.commands.executeCommand('vscode.executeCodeActionProvider', fileUri, position) as vscode.Command[];
    }

    // ----------- Code Lens Tests

    test('Can provide CodeLens in .razor file', async () => {
        const razorPath = path.join(mvcWithComponentsRoot, 'Views', 'Shared', 'NavMenu.razor');
        const razorDoc = await vscode.workspace.openTextDocument(razorPath);
        const razorEditor = await vscode.window.showTextDocument(razorDoc);

        const firstLine = new vscode.Position(0, 0);
        await razorEditor.edit(edit => edit.insert(firstLine, '@{ var x = typeof(MyClass); }\n'));
        await razorEditor.edit(edit => edit.insert(firstLine, '@code { public class MyClass { } }\n'));

        const codeLenses = await GetCodeLenses(razorDoc.uri);

        assert.equal(codeLenses.length, 1);
        assert.equal(codeLenses[0].isResolved, false);
        assert.equal(codeLenses[0].command, undefined);
    });

    test('Can resolve CodeLens in .razor file', async () => {
        const razorPath = path.join(mvcWithComponentsRoot, 'Views', 'Shared', 'NavMenu.razor');
        const razorDoc = await vscode.workspace.openTextDocument(razorPath);
        const razorEditor = await vscode.window.showTextDocument(razorDoc);

        const firstLine = new vscode.Position(0, 0);
        await razorEditor.edit(edit => edit.insert(firstLine, '@{ var x = typeof(MyClass); }\n'));
        await razorEditor.edit(edit => edit.insert(firstLine, '@code { public class MyClass { } }\n'));

        // Second argument makes sure the CodeLens we expect is resolved.
        const codeLenses = await GetCodeLenses(razorDoc.uri, 100);

        assert.equal(codeLenses.length, 1);
        assert.equal(codeLenses[0].isResolved, true);
        assert.notEqual(codeLenses[0].command, undefined);
        assert.equal(codeLenses[0].command!.title, '1 reference');
    });

    async function GetCodeLenses(fileUri: vscode.Uri, resolvedItemCount?: number) {
        return await vscode.commands.executeCommand('vscode.executeCodeLensProvider', fileUri, resolvedItemCount) as vscode.CodeLens[];
    }

    // ------------- Completions Tests

    test('Can complete Razor directive in .razor', async () => {
        const filePath = path.join(homeDirectory, 'Index.cshtml');
        const razorFilePath = path.join(mvcWithComponentsRoot, 'Views', 'Shared', 'NavMenu.razor');
        const razorDoc = await vscode.workspace.openTextDocument(razorFilePath);
        const razorEditor = await vscode.window.showTextDocument(razorDoc);
        const firstLine = new vscode.Position(0, 0);
        await razorEditor.edit(edit => edit.insert(firstLine, '@\n'));
        const completions = await vscode.commands.executeCommand<vscode.CompletionList>(
            'vscode.executeCompletionItemProvider',
            razorDoc.uri,
            new vscode.Position(0, 1));

        assertHasCompletion(completions, 'page');
        assertHasCompletion(completions, 'inject');
        assertHasNoCompletion(completions, 'div');
    });

    test('Can complete Razor directive in .cshtml', async () => {
        const filePath = path.join(homeDirectory, 'Index.cshtml');
        const cshtmlDoc = await vscode.workspace.openTextDocument(filePath);
        const editor = await vscode.window.showTextDocument(cshtmlDoc);
        const firstLine = new vscode.Position(0, 0);
        await editor.edit(edit => edit.insert(firstLine, '@\n'));
        const completions = await vscode.commands.executeCommand<vscode.CompletionList>(
            'vscode.executeCompletionItemProvider',
            cshtmlDoc.uri,
            new vscode.Position(0, 1));

        assertHasCompletion(completions, 'page');
        assertHasCompletion(completions, 'inject');
        assertHasNoCompletion(completions, 'div');
    });

    test('Can complete C# code blocks in .cshtml', async () => {
        const filePath = path.join(homeDirectory, 'Index.cshtml');
        const cshtmlDoc = await vscode.workspace.openTextDocument(filePath);
        const editor = await vscode.window.showTextDocument(cshtmlDoc);
        const lastLine = new vscode.Position(cshtmlDoc.lineCount - 1, 0);
        await editor.edit(edit => edit.insert(lastLine, '@{}'));
        await waitForDocumentUpdate(cshtmlDoc.uri, document => document.getText().indexOf('@{}') >= 0);

        const completions = await vscode.commands.executeCommand<vscode.CompletionList>(
            'vscode.executeCompletionItemProvider',
            cshtmlDoc.uri,
            new vscode.Position(cshtmlDoc.lineCount - 1, 2));

        assertHasCompletion(completions, 'DateTime');
        assertHasCompletion(completions, 'DateTimeKind');
        assertHasCompletion(completions, 'DateTimeOffset');
    });

    test('Can complete C# implicit expressions in .cshtml', async () => {
        const filePath = path.join(homeDirectory, 'Index.cshtml');
        const cshtmlDoc = await vscode.workspace.openTextDocument(filePath);
        const editor = await vscode.window.showTextDocument(cshtmlDoc);
        const lastLine = new vscode.Position(cshtmlDoc.lineCount - 1, 0);
        await editor.edit(edit => edit.insert(lastLine, '@'));
        await waitForDocumentUpdate(cshtmlDoc.uri, document => document.lineAt(document.lineCount - 1).text === '@');

        const completions = await vscode.commands.executeCommand<vscode.CompletionList>(
            'vscode.executeCompletionItemProvider',
            cshtmlDoc.uri,
            new vscode.Position(lastLine.line, 1));

        assertHasCompletion(completions, 'DateTime');
        assertHasCompletion(completions, 'DateTimeKind');
        assertHasCompletion(completions, 'DateTimeOffset');
    });

    test('Can complete imported C# in .cshtml', async () => {
        const filePath = path.join(homeDirectory, 'Index.cshtml');
        const cshtmlDoc = await vscode.workspace.openTextDocument(filePath);
        const editor = await vscode.window.showTextDocument(cshtmlDoc);
        const lastLine = new vscode.Position(cshtmlDoc.lineCount - 1, 0);
        await editor.edit(edit => edit.insert(lastLine, '@'));
        await waitForDocumentUpdate(cshtmlDoc.uri, document => document.lineAt(document.lineCount - 1).text === '@');

        const completions = await vscode.commands.executeCommand<vscode.CompletionList>(
            'vscode.executeCompletionItemProvider',
            cshtmlDoc.uri,
            new vscode.Position(cshtmlDoc.lineCount - 1, 1));

        assertHasCompletion(completions, 'TheTime');
    });

    test('Can complete HTML tag in .cshtml', async () => {
        const filePath = path.join(homeDirectory, 'Index.cshtml');
        const cshtmlDoc = await vscode.workspace.openTextDocument(filePath);
        const editor = await vscode.window.showTextDocument(cshtmlDoc);
        const lastLine = new vscode.Position(0, 0);
        await editor.edit(edit => edit.insert(lastLine, '<str'));
        const completions = await vscode.commands.executeCommand<vscode.CompletionList>(
            'vscode.executeCompletionItemProvider',
            cshtmlDoc.uri,
            new vscode.Position(0, 4));

        assertHasCompletion(completions, 'strong');
    });
});
