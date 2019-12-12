/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as assert from 'assert';
import { afterEach, before, beforeEach } from 'mocha';
import * as path from 'path';
import * as vscode from 'vscode';
import {
    pollUntil,
    simpleMvc22Root,
    waitForProjectReady,
} from './TestUtil';

let cshtmlDoc: vscode.TextDocument;
let editor: vscode.TextEditor;

suite('Hover 2.2', () => {
    before(async () => {
        await waitForProjectReady(simpleMvc22Root);
    });

    beforeEach(async () => {
        const filePath = path.join(simpleMvc22Root, 'Views', 'Home', 'Index.cshtml');
        cshtmlDoc = await vscode.workspace.openTextDocument(filePath);
        editor = await vscode.window.showTextDocument(cshtmlDoc);
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

    test('Hovers over tags with multiple possible TagHelpers should return both', async () => {
        const firstLine = new vscode.Position(0, 0);
        await editor.edit(edit => edit.insert(firstLine, '<environment exclude="d" />\n'));
        await editor.edit(edit => edit.insert(firstLine, '@addTagHelper *, SimpleMvc22\n'));
        let hoverResult = await vscode.commands.executeCommand<vscode.Hover[]>(
            'vscode.executeHoverProvider',
            cshtmlDoc.uri,
            new vscode.Position(1, 3));

        assert.ok(hoverResult, 'Should have returned a result');
        assert.equal(hoverResult!.length, 1, 'Should only have one hover result since the markdown is presented as one.');
        let mdString = hoverResult![0].contents[0] as vscode.MarkdownString;
        assert.ok(mdString.value.includes('elements that conditionally renders'));
        assert.ok(mdString.value.includes('I made it!'));

        hoverResult = await vscode.commands.executeCommand<vscode.Hover[]>(
            'vscode.executeHoverProvider',
            cshtmlDoc.uri,
            new vscode.Position(1, 15));

        assert.ok(hoverResult, 'Should have returned a result');
        assert.equal(hoverResult!.length, 1, 'Should have a hover result for both EnvironmentTagHelpers');
        mdString = hoverResult![0].contents[0] as vscode.MarkdownString;
        assert.ok(mdString.value.includes('A comma separated list of environment names in'));
        assert.ok(mdString.value.includes('Exclude it!'));
    });

    test('Can perform hovers on TagHelpers that need attributes', async () => {
        const firstLine = new vscode.Position(0, 0);
        await editor.edit(edit => edit.insert(firstLine, '<input class="someName" />\n'));
        let hoverResult = await vscode.commands.executeCommand<vscode.Hover[]>(
            'vscode.executeHoverProvider',
            cshtmlDoc.uri,
            new vscode.Position(0, 3));

        assert.ok(hoverResult, 'Should have returned a result');
        assert.equal(hoverResult!.length, 1, 'Should not have a hover result for InputTagHelper because it does not have the correct attrs yet.');

        await editor.edit(edit => edit.insert(firstLine, '<input asp-for="D" class="someName" />\n'));
        hoverResult = await vscode.commands.executeCommand<vscode.Hover[]>(
            'vscode.executeHoverProvider',
            cshtmlDoc.uri,
            new vscode.Position(0, 3));

        assert.ok(hoverResult, 'Should have a hover result for InputTagHelper.');
        if (hoverResult) {
            assert.equal(hoverResult.length, 2, 'Something else may be providing hover results');

            const envResult = hoverResult[0];
            const expectedRange = new vscode.Range(
                new vscode.Position(0, 3),
                new vscode.Position(0, 3));
            assert.deepEqual(envResult.range, expectedRange, 'TagHelper range should be <input>');
            const mStr = envResult.contents[0] as vscode.MarkdownString;
            assert.ok(mStr.value.includes('InputTagHelper'), `InputTagHelper not included in '${mStr.value}'`);
        }

        hoverResult = await vscode.commands.executeCommand<vscode.Hover[]>(
            'vscode.executeHoverProvider',
            cshtmlDoc.uri,
            new vscode.Position(0, 8));

        assert.ok(hoverResult, 'Should have a hover result for asp-for');
        if (hoverResult) {
            assert.equal(hoverResult.length, 1, 'Something else may be providing hover results');

            const envResult = hoverResult[0];
            const expectedRange = new vscode.Range(
                new vscode.Position(0, 8),
                new vscode.Position(0, 8));
            assert.deepEqual(envResult.range, expectedRange, 'asp-for should be selected');
            const mStr = envResult.contents[0] as vscode.MarkdownString;
            assert.ok(mStr.value.includes('InputTagHelper.**For**'), `InputTagHelper.For not included in '${mStr.value}'`);
        }

        hoverResult = await vscode.commands.executeCommand<vscode.Hover[]>(
            'vscode.executeHoverProvider',
            cshtmlDoc.uri,
            new vscode.Position(0, 19));

        assert.ok(hoverResult, 'Should have a hover result for class');
        if (hoverResult) {
            assert.equal(hoverResult.length, 1, 'Something else may be providing hover results');

            const result = hoverResult[0];
            const expectedRange = new vscode.Range(
                new vscode.Position(0, 19),
                new vscode.Position(0, 24));
            assert.deepEqual(result.range, expectedRange, 'class should be selected');
            const mStr = result.contents[0] as vscode.MarkdownString;
            assert.ok(mStr.value.includes('class'), `class not included in ${mStr.value}`);
        }
    });

    // MvcWithComponents doesn't find TagHelpers because of test setup foibles.
    test('Can perform hovers on TagHelpers', async () => {
        const firstLine = new vscode.Position(0, 0);
        await editor.edit(edit => edit.insert(firstLine, '<environment class="someName"></environment>\n'));
        const hoverResult = await vscode.commands.executeCommand<vscode.Hover[]>(
            'vscode.executeHoverProvider',
            cshtmlDoc.uri,
            new vscode.Position(0, 3));
        const expectedRange = new vscode.Range(
            new vscode.Position(0, 3),
            new vscode.Position(0, 3));

        assert.ok(hoverResult, 'Should have a hover result for EnvironmentTagHelper');
        if (hoverResult) {
            assert.equal(hoverResult.length, 1, 'Something else may be providing hover results');

            const envResult = hoverResult[0];
            assert.deepEqual(envResult.range, expectedRange, 'TagHelper range should be <environment>');
            const mStr = envResult.contents[0] as vscode.MarkdownString;
            assert.ok(mStr.value.includes('**EnvironmentTagHelper**'), `EnvironmentTagHelper not included in '${mStr.value}'`);
        }
    });
});
