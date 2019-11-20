/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as assert from 'assert';
import { afterEach, before, beforeEach } from 'mocha';
import * as path from 'path';
import * as vscode from 'vscode';
import {
    mvcWithComponentsRoot,
    pollUntil,
    waitForProjectReady,
} from './TestUtil';

let cshtmlDoc: vscode.TextDocument;
let editor: vscode.TextEditor;
let cshtmlPath: string;

suite('References', () => {
    before(async () => {
        await waitForProjectReady(mvcWithComponentsRoot);
    });

    beforeEach(async () => {
        cshtmlPath = path.join(mvcWithComponentsRoot, 'Views', 'Home', 'Index.cshtml');
        cshtmlDoc = await vscode.workspace.openTextDocument(cshtmlPath);
        editor = await vscode.window.showTextDocument(cshtmlDoc);
    });

    afterEach(async () => {
        await vscode.commands.executeCommand('workbench.action.revertAndCloseActiveEditor');
        await pollUntil(() => vscode.window.visibleTextEditors.length === 0, 1000);
    });

    // test('Reference inside file works', async () => {
    //     const firstLine = new vscode.Position(0, 0);
    //     await editor.edit(edit => edit.insert(firstLine, '@{\nTester();\n}\n'));
    //     await editor.edit(edit => edit.insert(firstLine, '@functions{\nvoid Tester()\n{\n}}\n'));
    //     const references = await vscode.commands.executeCommand<vscode.Location[]>(
    //         'vscode.executeReferenceProvider',
    //         cshtmlDoc.uri,
    //         new vscode.Position(1, 6));

    //     assert.equal(references!.length, 1, 'Should have had exactly one result');
    //     const reference = references![0];
    //     assert.ok(reference.uri.path.endsWith(''), `Expected ref to point to "${cshtmlDoc.uri}", but it pointed to ${reference.uri.path}`);
    //     assert.equal(reference.range.start.line, 5);
    // });

    // test('Reference outside file works', async () => {
    //     assert.fail('Not implemented!');
    // });

    test('Reference background file works', async () => {
        const firstLine = new vscode.Position(0, 0);
        await editor.edit(edit => edit.insert(firstLine, `@{
MvcWithComponents.Views.Shared.NavMenu.Tester();
MvcWithComponents.Views.Shared.NavMenu.Tester();
}\n`));

        vscode.commands.executeCommand('workbench.action.closeActiveEditor');

        const razorPath = path.join(mvcWithComponentsRoot, 'Views', 'Shared', 'NavMenu.razor');
        const razorDoc = await vscode.workspace.openTextDocument(razorPath);
        const razorEditor = await vscode.window.showTextDocument(razorDoc);
        await razorEditor.edit(edit => edit.insert(firstLine, `@functions{
void Tester() {
}
}`));

        const references = await vscode.commands.executeCommand<vscode.Location[]>(
            'vscode.executeReferenceProvider',
            razorDoc.uri,
            new vscode.Position(1, 9));

        assert.equal(references!.length, 2, 'Should have had exactly one result');
        const reference = references![0];
        assert.ok(reference.uri.path.endsWith('Index.cshtml'), `Expected ref to point to "${cshtmlDoc.uri}", but it pointed to ${reference.uri.path}`);
        assert.equal(reference.range.start.line, 1);
    });

    test('Reference razor-to-razor works', async () => {
        const firstLine = new vscode.Position(0, 0);
        await editor.edit(edit => edit.insert(firstLine, `@{
MvcWithComponents.Views.Shared.NavMenu.Tester();
MvcWithComponents.Views.Shared.NavMenu.Tester();
}\n`));

        const razorPath = path.join(mvcWithComponentsRoot, 'Views', 'Shared', 'NavMenu.razor');
        const razorDoc = await vscode.workspace.openTextDocument(razorPath);
        const razorEditor = await vscode.window.showTextDocument(razorDoc);
        await razorEditor.edit(edit => edit.insert(firstLine, `@functions{
void Tester() {
}
}`));

        const references = await vscode.commands.executeCommand<vscode.Location[]>(
            'vscode.executeReferenceProvider',
            razorDoc.uri,
            new vscode.Position(1, 9));

        assert.equal(references!.length, 2, 'Should have had exactly one result');
        const reference = references![0];
        assert.ok(reference.uri.path.endsWith('Index.cshtml'), `Expected ref to point to "${cshtmlDoc.uri}", but it pointed to ${reference.uri.path}`);
        assert.equal(reference.range.start.line, 1);
    });
});
