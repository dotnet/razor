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
        await pollUntil(() => vscode.window.visibleTextEditors.length === 0, 1000);
    });

    test('Can perform hovers on TagHelpers with attributes', async () => {
        const firstLine = new vscode.Position(0, 0);
        await editor.edit(edit => edit.insert(firstLine, '<input asp-for="SomeModel" />\n'));
        const hoverResult = await vscode.commands.executeCommand<vscode.Hover[]>(
            'vscode.executeHoverProvider',
            cshtmlDoc.uri,
            new vscode.Position(0, 3));
        const expectedRange = new vscode.Range(
            new vscode.Position(0, 0),
            new vscode.Position(0, 26));

        assert.ok(hoverResult, 'Should have a hover result for InputTagHelper');
        if (hoverResult) {
            assert.equal(hoverResult.length, 1, 'Something else may be providing hover results');

            const envResult = hoverResult[0];
            assert.deepEqual(envResult.range, expectedRange, 'TagHelper range should be <input asp-for="SomeModel" />');
            assert.fail('Check the content');
        }

        assert.fail('hover over the attribute and confirm that it is reported as a TagHelper');
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
            new vscode.Position(0, 0),
            new vscode.Position(0, 26));

        assert.ok(hoverResult, 'Should have a hover result for EnvironmentTagHelper');
        if (hoverResult) {
            assert.equal(hoverResult.length, 1, 'Something else may be providing hover results');

            const envResult = hoverResult[0];
            assert.deepEqual(envResult.range, expectedRange, 'TagHelper range should be <environment>');
            assert.fail('Check the content');
        }

        assert.fail('hover over the attribute and confirm that it is not reported as a TagHelper');
    });
});
