/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as assert from 'assert';
import { before, beforeEach } from 'mocha';
import * as path from 'path';
import * as vscode from 'vscode';
import {
    DoCodeAction,
    GetCodeActions,
    MakeEditAndFindDiagnostic,
    mvcWithComponentsRoot,
} from './TestUtil';

let razorPath: string;
let razorDoc: vscode.TextDocument;
let razorEditor: vscode.TextEditor;

suite('Code Actions', () => {
    before(function(this) {
        if (process.env.ci === 'true') {
            // Skipping on the CI as this consistently fails.
            this.skip();
        }
    });

    beforeEach(async () => {
        razorPath = path.join(mvcWithComponentsRoot, 'Views', 'Shared', 'NavMenu.razor');
        razorDoc = await vscode.workspace.openTextDocument(razorPath);
        razorEditor = await vscode.window.showTextDocument(razorDoc);
    });

    test('Can provide FullQualified CodeAction .razor file', async () => {
        const firstLine = new vscode.Position(0, 0);
        await MakeEditAndFindDiagnostic(razorEditor, '@{ var x = new HtmlString("sdf"); }\n', firstLine);

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
});
