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
    componentRoot,
    ensureNoChangesFor,
    getEditorForFile,
    mvcWithComponentsRoot,
    pollUntil,
    simpleMvc11Root,
    simpleMvc21Root,
    simpleMvc22Root,
    waitForDocumentUpdate,
    waitForProjectsReady,
} from './TestUtil';

const mvcWithComponentsIndex = path.join(mvcWithComponentsRoot, 'Views', 'Home', 'Index.cshtml');
const simpleMvc21Index = path.join(simpleMvc21Root, 'Views', 'Home', 'Index.cshtml');
const simpleMvc22Index = path.join(simpleMvc22Root, 'Views', 'Home', 'Index.cshtml');

suite('SUPER', () => {
    before(async () => {
        const projectList = [
            componentRoot,
            simpleMvc11Root,
            simpleMvc21Root,
            simpleMvc22Root,
            mvcWithComponentsRoot,
        ];
        await waitForProjectsReady(projectList);
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

    // --------- Find References ------------

    test('Reference for javascript', async () => {
        const firstLine = new vscode.Position(0, 0);
        const razorPath = path.join(mvcWithComponentsRoot, 'Components', 'Counter.razor');
        const razorDoc = await vscode.workspace.openTextDocument(razorPath);
        const razorEditor = await vscode.window.showTextDocument(razorDoc);
        await razorEditor.edit(edit => edit.insert(firstLine, `<script>
    var abc = 1;
    abc.toString();
</script>
`));
        const references = await vscode.commands.executeCommand<vscode.Location[]>(
            'vscode.executeReferenceProvider',
            razorDoc.uri,
            new vscode.Position(1, 10));

        assert.equal(references!.length, 2, 'Should have had exactly two results');
        const definition = references![1];
        assert.ok(definition.uri.path.endsWith('Counter.razor'), `Expected 'Counter.razor', but got ${definition.uri.path}`);
        assert.equal(definition.range.start.line, 2);
    });

    // test('Reference outside file works', async () => {
    //     let {doc: cshtmlDoc, editor: editor} = await getEditorForFile(mvcWithComponentsIndex);

    //     const programLine = new vscode.Position(7, 0);
    //     const programPath = path.join(mvcWithComponentsRoot, 'Program.cs');
    //     const programDoc = await vscode.workspace.openTextDocument(programPath);
    //     const programEditor = await vscode.window.showTextDocument(programDoc);
    //     await programEditor.edit(edit => edit.insert(programLine, `var x = typeof(Program);`));

    //     const firstLine = new vscode.Position(0, 0);
    //     cshtmlDoc = await vscode.workspace.openTextDocument(mvcWithComponentsIndex);
    //     editor = await vscode.window.showTextDocument(cshtmlDoc);
    //     await editor.edit(edit => edit.insert(firstLine, '@{\nvar x = typeof(Program);\n}\n'));

    //     const references = await vscode.commands.executeCommand<vscode.Location[]>(
    //         'vscode.executeReferenceProvider',
    //         cshtmlDoc.uri,
    //         new vscode.Position(1, 17));

    //     assert.equal(references!.length, 2 , 'Should have had exactly 2 results');
    //     const programRef = references![0];
    //     assert.ok(programRef.uri.path.endsWith('Program.cs'), `Expected ref to point to "Program.cs" but got ${references![1].uri.path}`);
    //     assert.equal(programRef.range.start.line, 7);

    //     const cshtmlRef = references![1];
    //     assert.ok(cshtmlRef.uri.path.endsWith('Index.cshtml'), `Expected ref to point to "Index.cshtml" but got ${references![1].uri.path}`);
    //     assert.equal(cshtmlRef.range.start.line, 1);

    //     await vscode.commands.executeCommand('workbench.action.revertAndCloseActiveEditor');
    // });

    // test('Reference inside file works', async () => {
    //     const {doc: cshtmlDoc, editor: editor} = await getEditorForFile(mvcWithComponentsIndex);

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

    // --------- Go to Definition -----------

    test('Definition of injection gives nothing', async () => {
        const firstLine = new vscode.Position(0, 0);
        const {doc: cshtmlDoc, editor: editor} = await getEditorForFile(mvcWithComponentsIndex);

        await editor.edit(edit => edit.insert(firstLine, '@inject DateTime SecondTime\n'));
        await editor.edit(edit => edit.insert(firstLine, '@SecondTime\n'));
        const definitions = await vscode.commands.executeCommand<vscode.Location[]>(
            'vscode.executeDefinitionProvider',
            cshtmlDoc.uri,
            new vscode.Position(0, 18));

        assert.equal(definitions!.length, 0, 'Should have had no results');
    });

    // test('Definition inside file works', async () => {
    //     const firstLine = new vscode.Position(0, 0);
    //     const {doc: cshtmlDoc, editor: editor} = await getEditorForFile(mvcWithComponentsIndex);

    //     await editor.edit(edit => edit.insert(firstLine, '@functions{\n void Action()\n{\n}\n}\n'));
    //     await editor.edit(edit => edit.insert(firstLine, '@{\nAction();\n}\n'));
    //     const definitions = await vscode.commands.executeCommand<vscode.Location[]>(
    //         'vscode.executeDefinitionProvider',
    //         cshtmlDoc.uri,
    //         new vscode.Position(1, 2));

    //     assert.equal(definitions!.length, 1, 'Should have had exactly one result');
    //     const definition = definitions![0];
    //     assert.ok(definition.uri.path.endsWith('Index.cshtml'));
    //     assert.equal(definition.range.start.line, 4);
    // });

    // test('Definition outside file works', async () => {
    //     const firstLine = new vscode.Position(0, 0);
    //     const {doc: cshtmlDoc, editor: editor} = await getEditorForFile(mvcWithComponentsIndex);

    //     await editor.edit(edit => edit.insert(firstLine, '@{\nvar x = typeof(Program);\n}\n'));

    //     const definitions = await vscode.commands.executeCommand<vscode.Location[]>(
    //         'vscode.executeDefinitionProvider',
    //         cshtmlDoc.uri,
    //         new vscode.Position(1, 17));

    //     assert.equal(definitions!.length, 1, 'Should have had exactly one result');
    //     const definition = definitions![0];
    //     assert.ok(definition.uri.path.endsWith('Program.cs'), `Expected def to point to "Program.cs", but it pointed to ${definition.uri.path}`);
    //     assert.equal(definition.range.start.line, 3);
    // });

    test('Definition of javascript works in cshtml', async () => {
        const firstLine = new vscode.Position(0, 0);
        const {doc: cshtmlDoc, editor: editor} = await getEditorForFile(mvcWithComponentsIndex);

        await editor.edit(edit => edit.insert(firstLine, `<script>
    var abc = 1;
    abc.toString();
</script>
`));
        const definitions = await vscode.commands.executeCommand<vscode.Location[]>(
            'vscode.executeDefinitionProvider',
            cshtmlDoc.uri,
            new vscode.Position(2, 5));

        assert.equal(definitions!.length, 1, 'Should have had exactly one result');
        const definition = definitions![0];
        assert.ok(definition.uri.path.endsWith('Index.cshtml'), `Expected 'Index.cshtml', but got ${definition.uri.path}`);
        assert.equal(definition.range.start.line, 1);
    });

    test('Definition of javascript works in razor', async () => {
        const firstLine = new vscode.Position(0, 0);
        const razorPath = path.join(mvcWithComponentsRoot, 'Components', 'Counter.razor');
        const razorDoc = await vscode.workspace.openTextDocument(razorPath);
        const razorEditor = await vscode.window.showTextDocument(razorDoc);
        await razorEditor.edit(edit => edit.insert(firstLine, `<script>
    var abc = 1;
    abc.toString();
</script>
`));
        const definitions = await vscode.commands.executeCommand<vscode.Location[]>(
            'vscode.executeDefinitionProvider',
            razorDoc.uri,
            new vscode.Position(2, 5));

        assert.equal(definitions!.length, 1, 'Should have had exactly one result');
        const definition = definitions![0];
        assert.ok(definition.uri.path.endsWith('Counter.razor'), `Expected 'Counter.razor', but got ${definition.uri.path}`);
        assert.equal(definition.range.start.line, 1);
    });

    // --------- Hover ----------------------

    // test('Can perform hovers on C#', async () => {
    //     const {doc: cshtmlDoc, editor: editor} = await getEditorForFile(mvcWithComponentsIndex);
    //     console.log('Before edit');
    //     console.log(cshtmlDoc.getText());
    //     const firstLine = new vscode.Position(0, 0);
    //     await editor.edit(edit => edit.insert(firstLine, '<p>@DateTime.Now</p>\n'));
    //     console.log('After edit');
    //     console.log(cshtmlDoc.getText());
    //     const hoverResult = await WaitForHover(cshtmlDoc.uri, new vscode.Position(0, 6));
    //     const expectedRange = new vscode.Range(
    //         new vscode.Position(0, 4),
    //         new vscode.Position(0, 12));

    //     assert.ok(hoverResult, 'Should have a hover result for DateTime.Now');
    //     if (!hoverResult) {
    //         // Not possible, but strict TypeScript doesn't know about assert.ok above.
    //         return;
    //     }

    //     assert.equal(hoverResult.length, 1, 'Someone else unexpectedly may be providing hover results');
    //     assert.deepEqual(hoverResult[0].range, expectedRange, 'C# hover range should be DateTime.Now');
    // });

    // async function WaitForHover(fileUri: vscode.Uri, position: vscode.Position) {
    //     await pollUntil(async () => {
    //         console.log('polling');
    //         const hover = await vscode.commands.executeCommand<vscode.Hover[]>(
    //             'vscode.executeHoverProvider',
    //             fileUri,
    //             position);

    //         if (hover!.length > 0) {
    //             console.log('returning true');
    //             return true;
    //         } else {
    //             console.log('returning false');
    //             return false;
    //         }
    //     }, /* timeout */ 5000, /* pollInterval */ 1000, /* suppressError */ false);
    //     await new Promise(r => setTimeout(r, 10000));

    //     console.log('Done polling. Returning');
    //     return vscode.commands.executeCommand<vscode.Hover[]>(
    //         'vscode.executeHoverProvider',
    //         fileUri,
    //         position);
    // }

    // --------- Hover 2_2 ------------------

    test('Hover over attribute value does not return TagHelper info', async () => {
        const {doc: cshtmlDoc, editor: editor} = await getEditorForFile(simpleMvc22Index);
        const firstLine = new vscode.Position(0, 0);
        await editor.edit(edit => edit.insert(firstLine, '<environment exclude="drain" />\n'));

        const hoverResult = await vscode.commands.executeCommand<vscode.Hover[]>(
            'vscode.executeHoverProvider',
            cshtmlDoc.uri,
            new vscode.Position(0, 24));

        assert.ok(hoverResult, 'Should have returned a result');
        assert.equal(hoverResult!.length, 0, 'Should only have one hover result since the markdown is presented as one.');
    });

    test('Hover over multiple attributes gives the correct one', async () => {
        const {doc: cshtmlDoc, editor: editor} = await getEditorForFile(simpleMvc22Index);
        const firstLine = new vscode.Position(0, 0);
        await editor.edit(edit => edit.insert(firstLine, '<environment exclude="drain" include="fountain" />\n'));

        let hoverResult = await vscode.commands.executeCommand<vscode.Hover[]>(
            'vscode.executeHoverProvider',
            cshtmlDoc.uri,
            new vscode.Position(0, 16));

        assert.ok(hoverResult, 'Should have returned a result');
        assert.equal(hoverResult!.length, 1, 'Should only have one hover result');

        let mdString = hoverResult![0].contents[0] as vscode.MarkdownString;
        assert.ok(mdString.value.includes('**Exclude**'), `Expected "Exclude" in ${mdString.value}`);
        assert.ok(!mdString.value.includes('**Include**'), `Expected 'Include' not to be in ${mdString.value}`);

        hoverResult = await vscode.commands.executeCommand<vscode.Hover[]>(
            'vscode.executeHoverProvider',
            cshtmlDoc.uri,
            new vscode.Position(0, 32));

        assert.ok(hoverResult, 'Should have returned a result');
        assert.equal(hoverResult!.length, 1, 'Should only have one hover result');

        mdString = hoverResult![0].contents[0] as vscode.MarkdownString;
        assert.ok(!mdString.value.includes('**Exclude**'), `Expected "Exclude" not to be in ${mdString.value}`);
        assert.ok(mdString.value.includes('**Include**'), `Expected 'Include' in ${mdString.value}`);
    });

    test('Hovers over tags with multiple possible TagHelpers should return both', async () => {
        const {doc: cshtmlDoc, editor: editor} = await getEditorForFile(simpleMvc22Index);
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

    test('Can perform hovers on TagHelper Elements and Attribute', async () => {
        const {doc: cshtmlDoc, editor: editor} = await getEditorForFile(simpleMvc22Index);
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
        if (!hoverResult) {
            // This can never happen
            return;
        }

        assert.equal(hoverResult.length, 2, 'Something else may be providing hover results');

        let envResult = hoverResult[0];
        let expectedRange = new vscode.Range(
            new vscode.Position(0, 1),
            new vscode.Position(0, 6));
        assert.deepEqual(envResult.range, expectedRange, 'TagHelper range should be <input>');
        let mStr = envResult.contents[0] as vscode.MarkdownString;
        assert.ok(mStr.value.includes('InputTagHelper'), `InputTagHelper not included in '${mStr.value}'`);

        hoverResult = await vscode.commands.executeCommand<vscode.Hover[]>(
            'vscode.executeHoverProvider',
            cshtmlDoc.uri,
            new vscode.Position(0, 8));

        assert.ok(hoverResult, 'Should have a hover result for asp-for');
        if (!hoverResult) {
            // This can never happen
            return;
        }

        assert.equal(hoverResult.length, 1, 'Something else may be providing hover results');

        envResult = hoverResult[0];
        expectedRange = new vscode.Range(
            new vscode.Position(0, 7),
            new vscode.Position(0, 14));
        assert.deepEqual(envResult.range, expectedRange, 'asp-for should be selected');
        mStr = envResult.contents[0] as vscode.MarkdownString;
        assert.ok(mStr.value.includes('InputTagHelper.**For**'), `InputTagHelper.For not included in '${mStr.value}'`);

        hoverResult = await vscode.commands.executeCommand<vscode.Hover[]>(
            'vscode.executeHoverProvider',
            cshtmlDoc.uri,
            new vscode.Position(0, 19));

        assert.ok(hoverResult, 'Should have a hover result for class');
        if (!hoverResult) {
            // This can never happen
            return;
        }

        assert.equal(hoverResult.length, 1, 'Something else may be providing hover results');

        const result = hoverResult[0];
        expectedRange = new vscode.Range(
            new vscode.Position(0, 19),
            new vscode.Position(0, 24));
        assert.deepEqual(result.range, expectedRange, 'class should be selected');
        mStr = result.contents[0] as vscode.MarkdownString;
        assert.ok(mStr.value.includes('class'), `class not included in ${mStr.value}`);
    });

    // MvcWithComponents doesn't find TagHelpers because of test setup foibles.
    test('Can perform hovers on TagHelpers', async () => {
        const {doc: cshtmlDoc, editor: editor} = await getEditorForFile(simpleMvc22Index);
        const firstLine = new vscode.Position(0, 0);
        await editor.edit(edit => edit.insert(firstLine, '<environment class="someName"></environment>\n'));
        const hoverResult = await vscode.commands.executeCommand<vscode.Hover[]>(
            'vscode.executeHoverProvider',
            cshtmlDoc.uri,
            new vscode.Position(0, 3));
        const expectedRange = new vscode.Range(
            new vscode.Position(0, 1),
            new vscode.Position(0, 12));

        assert.ok(hoverResult, 'Should have a hover result for EnvironmentTagHelper');
        if (!hoverResult) {
            // Not possible, but strict TypeScript doesn't know about assert.ok above.
            return;
        }

        assert.equal(hoverResult.length, 1, 'Something else may be providing hover results');

        const envResult = hoverResult[0];
        assert.deepEqual(envResult.range, expectedRange, 'TagHelper range should be <environment>');
        const mStr = envResult.contents[0] as vscode.MarkdownString;
        assert.ok(mStr.value.includes('**EnvironmentTagHelper**'), `EnvironmentTagHelper not included in '${mStr.value}'`);
    });

    // --------- Hover Components -----------

    test('Can perform hovers on directive attributes', async () => {
        const firstLine = new vscode.Position(1, 0);
        const counterPath = path.join(componentRoot, 'Components', 'Pages', 'Counter.razor');
        const counterDoc = await vscode.workspace.openTextDocument(counterPath);
        const counterEditor = await vscode.window.showTextDocument(counterDoc);
        await counterEditor.edit(edit => edit.insert(firstLine, '<button class="btn btn-primary" @onclick="@IncrementCount">Click me</button>'));

        const hoverResult = await vscode.commands.executeCommand<vscode.Hover[]>(
            'vscode.executeHoverProvider',
            counterDoc.uri,
            new vscode.Position(1, 36));

        assert.ok(hoverResult, 'Should have a hover result for @onclick');
        if (!hoverResult) {
            // Not possible, but strict TypeScript doesn't know about assert.ok above.
            return;
        }

        assert.equal(hoverResult.length, 1, 'Something else may be providing hover results');

        const onClickResult = hoverResult[0];
        const expectedRange = new vscode.Range(
            new vscode.Position(1, 31),
            new vscode.Position(1, 58));
        assert.deepEqual(hoverResult[0].range, expectedRange, 'Directive range should be @onclick');
        const mStr = onClickResult.contents[0] as vscode.MarkdownString;
        assert.ok(mStr.value.includes('EventHandlers.**onclick**'), `**onClick** not included in '${mStr.value}'`);
    });

    test('Can perform hovers on Components', async () => {
        const firstLine = new vscode.Position(0, 0);
        const mainLayoutPath = path.join(componentRoot, 'Components', 'Shared', 'MainLayout.razor');
        const {doc: cshtmlDoc, editor: editor} = await getEditorForFile(mainLayoutPath);
        await editor.edit(edit => edit.insert(firstLine, '<NavMenu />\n'));
        const hoverResult = await vscode.commands.executeCommand<vscode.Hover[]>(
            'vscode.executeHoverProvider',
            cshtmlDoc.uri,
            new vscode.Position(0, 3));
        const expectedRange = new vscode.Range(
            new vscode.Position(0, 1),
            new vscode.Position(0, 8));

        assert.ok(hoverResult, 'Should have a hover result for NavMenu');
        if (!hoverResult) {
            // Not possible, but strict TypeScript doesn't know about assert.ok above.
            return;
        }

        assert.equal(hoverResult.length, 1, 'Something else may be providing hover results');

        const navMenuResult = hoverResult[0];
        assert.deepEqual(navMenuResult.range, expectedRange, 'Component range should be <NavMenu>');
        const mStr = navMenuResult.contents[0] as vscode.MarkdownString;
        assert.ok(mStr.value.includes('**NavMenu**'), `**NavMenu** not included in '${mStr.value}'`);
    });

    // --------- GoToImplementation ---------

//     test('Implementation inside file works', async () => {
//         const firstLine = new vscode.Position(0, 0);
//         const {doc: cshtmlDoc, editor: editor} = await getEditorForFile(mvcWithComponentsIndex);

//         await editor.edit(edit => edit.insert(firstLine,
// `@functions{
//     public abstract class Cheese {}
//     public class Cheddar : Cheese {}
// }`));

//         const implementations = await vscode.commands.executeCommand<vscode.Location[]>(
//             'vscode.executeImplementationProvider',
//             cshtmlDoc.uri,
//             new vscode.Position(1, 30));

//         assert.equal(implementations!.length, 1, 'Should have had exactly one result');
//         const implementation = implementations![0];
//         assert.ok(implementation.uri.path.endsWith('Index.cshtml'), `Expected to find 'Index.cshtml' but found '${implementation.uri.path}'`);
//         assert.equal(implementation.range.start.line, 2);
//     });

//     test('Implementation outside file works', async () => {
//         const {doc: cshtmlDoc, editor: editor} = await getEditorForFile(mvcWithComponentsIndex);

//         const firstLine = new vscode.Position(0, 0);
//         await editor.edit(edit => edit.insert(firstLine, `@{
//     var x = typeof(Cheese);
// }`));

//         const programPath = path.join(mvcWithComponentsRoot, 'Program.cs');
//         const programDoc = await vscode.workspace.openTextDocument(programPath);
//         const programEditor = await vscode.window.showTextDocument(programDoc);
//         await programEditor.edit(edit => edit.insert(new vscode.Position(3, 0), `    public abstract class Cheese {}
//     public class Cheddar : Cheese {}
// `));

//         const position = new vscode.Position(1, 23);
//         const implementations = await vscode.commands.executeCommand<vscode.Location[]>(
//             'vscode.executeImplementationProvider',
//             cshtmlDoc.uri,
//             position);

//         await vscode.commands.executeCommand('workbench.action.revertAndCloseActiveEditor');

//         assert.equal(implementations!.length, 1, 'Should have had exactly one result');
//         const implementation = implementations![0];
//         assert.ok(implementation.uri.path.endsWith('Program.cs'), `Expected def to point to "Program.cs", but it pointed to ${implementation.uri.path}`);
//         assert.equal(implementation.range.start.line, 4);
//     });

    // --------- Html Typing ----------------

    test('Can auto-close start and end Html tags', async () => {
        const {doc: doc, editor: editor} = await getEditorForFile(simpleMvc21Index);
        const lastLine = new vscode.Position(doc.lineCount - 1, 0);
        await editor.edit(edit => edit.insert(lastLine, '<strong'));
        const lastLineEnd = new vscode.Position(doc.lineCount - 1, 7);
        await editor.edit(edit => edit.insert(lastLineEnd, '>'));

        const newDoc = await waitForDocumentUpdate(doc.uri, document => document.getText().indexOf('</strong>') >= 0);

        const docLine = newDoc.lineAt(newDoc.lineCount - 1);
        assert.deepEqual(docLine.text, '<strong></strong>');
    });

    test('Does not auto-close self-closing Html tags', async () => {
        const {doc: doc, editor: editor} = await getEditorForFile(simpleMvc21Index);
        const lastLine = new vscode.Position(doc.lineCount - 1, 0);
        await editor.edit(edit => edit.insert(lastLine, '<input /'));
        const lastLineEnd = new vscode.Position(doc.lineCount - 1, 8);
        await editor.edit(edit => edit.insert(lastLineEnd, '>'));

        const newDoc = await waitForDocumentUpdate(doc.uri, document => document.getText().indexOf('<input />') >= 0);

        await ensureNoChangesFor(newDoc.uri, 300);

        const docLine = newDoc.lineAt(newDoc.lineCount - 1);
        assert.deepEqual(docLine.text, '<input />');
    });

    test('Does not auto-close C# generics', async () => {
        const {doc: doc, editor: editor} = await getEditorForFile(simpleMvc21Index);
        const lastLine = new vscode.Position(doc.lineCount - 1, 0);
        await editor.edit(edit => edit.insert(lastLine, '@{new List<string}'));
        const lastLineEnd = new vscode.Position(doc.lineCount - 1, 17);
        await editor.edit(edit => edit.insert(lastLineEnd, '>'));

        const newDoc = await waitForDocumentUpdate(doc.uri, document => document.getText().indexOf('<string>') >= 0);

        await ensureNoChangesFor(newDoc.uri, 300);

        const docLine = newDoc.lineAt(newDoc.lineCount - 1);
        assert.deepEqual(docLine.text, '@{new List<string>}');
    });

    // --------- Signature help -------------

    test('Can get signature help for JavaScript', async () => {
        const {doc: doc, editor: editor} = await getEditorForFile(simpleMvc21Index);
        const firstLine = new vscode.Position(0, 0);
        const codeToInsert = '<script>console.log(</script>';
        await editor.edit(edit => edit.insert(firstLine, codeToInsert));
        await waitForDocumentUpdate(doc.uri, document => document.getText().indexOf(codeToInsert) >= 0);

        const signatureHelp = await vscode.commands.executeCommand<vscode.SignatureHelp>(
            'vscode.executeSignatureHelpProvider',
            doc.uri,
            new vscode.Position(0, 20));
        const signatures = signatureHelp!.signatures;

        assert.equal(signatures.length, 1);
        assert.equal(signatures[0].label, 'log(message?: any, ...optionalParams: any[]): void');
    });

    // test('Can get signature help for C#', async () => {
    //     const {doc: doc, editor: editor} = await getEditorForFile(simpleMvc21Index);
    //     const firstLine = new vscode.Position(0, 0);
    //     const codeToInsert = '@{ System.Console.WriteLine( }';
    //     await editor.edit(edit => edit.insert(firstLine, codeToInsert));
    //     await waitForDocumentUpdate(doc.uri, document => document.getText().indexOf(codeToInsert) >= 0);

    //     const signatureHelp = await vscode.commands.executeCommand<vscode.SignatureHelp>(
    //         'vscode.executeSignatureHelpProvider',
    //         doc.uri,
    //         new vscode.Position(firstLine.line, 28),
    //         '(');
    //     const signatures = signatureHelp!.signatures;
    //     assert.ok(signatures.some(s => s.label === 'void Console.WriteLine(bool value)'));
    // });

    // --------- Rename --------------

    // test('Can rename symbol within .razor', async () => {
    //     const filePath = path.join(mvcWithComponentsRoot, 'Views', 'Shared', 'NavMenu.razor');
    //     const {doc: razorDoc, editor: razorEditor} = await getEditorForFile(filePath);
    //     const expectedNewText = 'World';
    //     const firstLine = new vscode.Position(0, 0);
    //     await razorEditor.edit(edit => edit.insert(firstLine, '@hello\n'));
    //     await razorEditor.edit(edit => edit.insert(firstLine, '@{ var hello = "Hello"; }\n'));

    //     await new Promise(r => setTimeout(r, 3000));
    //     const renames = await vscode.commands.executeCommand<vscode.WorkspaceEdit>(
    //         'vscode.executeDocumentRenameProvider',
    //         razorDoc.uri,
    //         new vscode.Position(1, 2),
    //         expectedNewText);

    //     const entries = renames!.entries();
    //     assert.equal(entries.length, 1, 'Should only rename within the document.');
    //     const uri = entries[0][0];
    //     assert.equal(uri.path, razorDoc.uri.path);
    //     const edits = entries[0][1];
    //     assert.equal(edits.length, 2);
    // });

    // test('Can rename symbol within .cshtml', async () => {
    //     const cshtmlPath = path.join(mvcWithComponentsRoot, 'Views', 'Home', 'Index.cshtml');
    //     const cshtmlDoc = await vscode.workspace.openTextDocument(cshtmlPath);
    //     const cshtmlEditor = await vscode.window.showTextDocument(cshtmlDoc);
    //     const expectedNewText = 'World';
    //     const firstLine = new vscode.Position(0, 0);
    //     await cshtmlEditor.edit(edit => edit.insert(firstLine, '@hello\n'));
    //     await cshtmlEditor.edit(edit => edit.insert(firstLine, '@{ var hello = "Hello"; }\n'));

    //     await new Promise(r => setTimeout(r, 3000));
    //     const renames = await vscode.commands.executeCommand<vscode.WorkspaceEdit>(
    //         'vscode.executeDocumentRenameProvider',
    //         cshtmlDoc.uri,
    //         new vscode.Position(1, 2),
    //         expectedNewText);

    //     const entries = renames!.entries();
    //     assert.equal(entries.length, 1, 'Should only rename within the document.');
    //     const uri = entries[0][0];
    //     assert.equal(uri.path, cshtmlDoc.uri.path);
    //     const edits = entries[0][1];
    //     assert.equal(edits.length, 2);
    // });

    // test('Rename symbol in .razor also changes .cs', async () => {
    //     const razorPath = path.join(mvcWithComponentsRoot, 'Views', 'Shared', 'NavMenu.razor');
    //     const csPath = path.join(mvcWithComponentsRoot, 'Test.cs');
    //     const razorDoc = await vscode.workspace.openTextDocument(razorPath);
    //     const razorEditor = await vscode.window.showTextDocument(razorDoc);
    //     const expectedNewText = 'Oof';
    //     const firstLine = new vscode.Position(0, 0);
    //     await razorEditor.edit(edit => edit.insert(firstLine, '@Test.Bar\n'));

    //     await new Promise(r => setTimeout(r, 3000));
    //     const renames = await vscode.commands.executeCommand<vscode.WorkspaceEdit>(
    //         'vscode.executeDocumentRenameProvider',
    //         razorDoc.uri,
    //         new vscode.Position(0, 7),
    //         expectedNewText);

    //     const entries = renames!.entries();
    //     assert.equal(entries.length, 2, 'Should have renames in two documents.');

    //     // Razor file
    //     const uri1 = entries[0][0];
    //     assert.equal(uri1.path, vscode.Uri.file(csPath).path);
    //     const edits1 = entries[0][1];
    //     assert.equal(edits1.length, 1);

    //     // cs file
    //     const uri2 = entries[1][0];
    //     assert.equal(uri2.path, razorDoc.uri.path);
    //     const edits2 = entries[1][1];
    //     assert.equal(edits2.length, 1);
    // });

    // test('Rename symbol in .cs also changes .razor', async () => {
    //     const razorPath = path.join(mvcWithComponentsRoot, 'Views', 'Shared', 'NavMenu.razor');
    //     const csPath = path.join(mvcWithComponentsRoot, 'Test.cs');
    //     const expectedNewText = 'Oof';
    //     const csDoc = await vscode.workspace.openTextDocument(csPath);

    //     await new Promise(r => setTimeout(r, 3000));
    //     const renames = await vscode.commands.executeCommand<vscode.WorkspaceEdit>(
    //         'vscode.executeDocumentRenameProvider',
    //         csDoc.uri,
    //         new vscode.Position(4, 30), // Position `public static string F|oo { get; set; }`
    //         expectedNewText);

    //     const entries = renames!.entries();
    //     assert.equal(entries.length, 2, 'Should have renames in two documents.');

    //     // Razor file
    //     const uri1 = entries[0][0];
    //     assert.equal(uri1.path, csDoc.uri.path);
    //     const edits1 = entries[0][1];
    //     assert.equal(edits1.length, 1);

    //     // cs file
    //     const uri2 = entries[1][0];
    //     assert.equal(uri2.path, vscode.Uri.file(razorPath).path);
    //     const edits2 = entries[1][1];
    //     assert.equal(edits2.length, 1);
    // });

    // ------- Code Actions -------------

    // test('Can provide FullQualified CodeAction .razor file', async () => {
    //     const razorPath = path.join(mvcWithComponentsRoot, 'Views', 'Shared', 'NavMenu.razor');
    //     const razorDoc = await vscode.workspace.openTextDocument(razorPath);
    //     const firstLine = new vscode.Position(0, 0);
    //     await MakeEditAndFindDiagnostic('@{ var x = new HtmlString("sdf"); }\n', firstLine);

    //     const position = new vscode.Position(0, 21);
    //     const codeActions = await GetCodeActions(razorDoc.uri, new vscode.Range(position, position));

    //     assert.equal(codeActions.length, 1);
    //     const codeAction = codeActions[0];
    //     assert.equal(codeAction.title, 'Microsoft.AspNetCore.Html.HtmlString');

    //     await DoCodeAction(razorDoc.uri, codeAction, /* expectedDiagnosticCount */ 1);
    //     const reloadedDoc = await vscode.workspace.openTextDocument(razorDoc.uri);
    //     const editedText = reloadedDoc.getText();
    //     assert.ok(editedText.includes('var x = new Microsoft.AspNetCore.Html.HtmlString("sdf");'));
    // });

    // async function DoCodeAction(fileUri: vscode.Uri, codeAction: vscode.Command, expectedDiagnosticCount: number) {
    //     let diagnosticsChanged = false;
    //     vscode.languages.onDidChangeDiagnostics(diagnosticsChangedEvent => {
    //         const diagnostics = vscode.languages.getDiagnostics(fileUri);

    //         if (diagnostics.length === expectedDiagnosticCount) {
    //             diagnosticsChanged = true;
    //         }
    //     });

    //     if (codeAction.command && codeAction.arguments) {
    //         const result = await vscode.commands.executeCommand<boolean | string>(codeAction.command, codeAction.arguments[0]);
    //         console.log(result);
    //     }

    //     await pollUntil(() => {
    //         return diagnosticsChanged;
    //     }, /* timeout */ 20000, /* pollInterval */ 1000, false /* suppress timeout */);
    // }

    // async function MakeEditAndFindDiagnostic(editText: string, position: vscode.Position) {
    //     const razorPath = path.join(mvcWithComponentsRoot, 'Views', 'Shared', 'NavMenu.razor');
    //     const razorDoc = await vscode.workspace.openTextDocument(razorPath);
    //     const razorEditor = await vscode.window.showTextDocument(razorDoc);
    //     let diagnosticsChanged = false;
    //     vscode.languages.onDidChangeDiagnostics(diagnosticsChangedEvent => {
    //         const diagnostics = vscode.languages.getDiagnostics(razorDoc.uri);
    //         if (diagnostics.length > 0) {
    //             diagnosticsChanged = true;
    //         }
    //     });

    //     for (let i = 0; i < 3; i++) {
    //         await razorEditor.edit(edit => edit.insert(position, editText));
    //         await pollUntil(() => {
    //             return diagnosticsChanged;
    //         }, /* timeout */ 5000, /* pollInterval */ 1000, true /* suppress timeout */);
    //         if (diagnosticsChanged) {
    //             break;
    //         }
    //     }
    // }

    // async function GetCodeActions(fileUri: vscode.Uri, position: vscode.Range): Promise<vscode.Command[]> {
    //     return await vscode.commands.executeCommand('vscode.executeCodeActionProvider', fileUri, position) as vscode.Command[];
    // }

    // ------- Code Actions 2.2 ---------

    // test('Can provide FullQualified CodeAction 2.2 .cshtml file', async () => {
    //     const firstLine = new vscode.Position(0, 0);
    //     const {doc: cshtmlDoc} = await getEditorForFile(simpleMvc22Index);
    //     await MakeEditAndFindDiagnostic('@{ var x = new HtmlString("sdf"); }\n', firstLine);

    //     const position = new vscode.Position(0, 21);
    //     const codeActions = await GetCodeActions(cshtmlDoc.uri, new vscode.Range(position, position));

    //     assert.equal(codeActions.length, 1);
    //     const codeAction = codeActions[0];
    //     assert.equal(codeAction.title, 'Microsoft.AspNetCore.Html.HtmlString');

    //     await DoCodeAction(cshtmlDoc.uri, codeAction, /* expectedDiagnosticCount */0);
    //     const reloadedDoc = await vscode.workspace.openTextDocument(cshtmlDoc.uri);
    //     const editedText = reloadedDoc.getText();
    //     assert.ok(editedText.includes('var x = new Microsoft.AspNetCore.Html.HtmlString("sdf");'));
    // });

    // ----------- Code Lens Tests

    // test('Can provide CodeLens in .razor file', async () => {
    //     const razorPath = path.join(mvcWithComponentsRoot, 'Views', 'Shared', 'NavMenu.razor');
    //     const razorDoc = await vscode.workspace.openTextDocument(razorPath);
    //     const razorEditor = await vscode.window.showTextDocument(razorDoc);

    //     const firstLine = new vscode.Position(0, 0);
    //     await razorEditor.edit(edit => edit.insert(firstLine, '@{ var x = typeof(MyClass); }\n'));
    //     await razorEditor.edit(edit => edit.insert(firstLine, '@code { public class MyClass { } }\n'));

    //     const codeLenses = await GetCodeLenses(razorDoc.uri);

    //     assert.equal(codeLenses.length, 1);
    //     assert.equal(codeLenses[0].isResolved, false);
    //     assert.equal(codeLenses[0].command, undefined);
    // });

    // test('Can resolve CodeLens in .razor file', async () => {
    //     const razorPath = path.join(mvcWithComponentsRoot, 'Views', 'Shared', 'NavMenu.razor');
    //     const razorDoc = await vscode.workspace.openTextDocument(razorPath);
    //     const razorEditor = await vscode.window.showTextDocument(razorDoc);

    //     const firstLine = new vscode.Position(0, 0);
    //     await razorEditor.edit(edit => edit.insert(firstLine, '@{ var x = typeof(MyClass); }\n'));
    //     await razorEditor.edit(edit => edit.insert(firstLine, '@code { public class MyClass { } }\n'));

    //     // Second argument makes sure the CodeLens we expect is resolved.
    //     const codeLenses = await GetCodeLenses(razorDoc.uri, 100);

    //     assert.equal(codeLenses.length, 1);
    //     assert.equal(codeLenses[0].isResolved, true);
    //     assert.notEqual(codeLenses[0].command, undefined);
    //     assert.equal(codeLenses[0].command!.title, '1 reference');
    // });

    // async function GetCodeLenses(fileUri: vscode.Uri, resolvedItemCount?: number) {
    //     return await vscode.commands.executeCommand('vscode.executeCodeLensProvider', fileUri, resolvedItemCount) as vscode.CodeLens[];
    // }

    // ------------- Completions Tests

    test('Can complete Razor directive in .razor', async () => {
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
        const cshtmlDoc = await vscode.workspace.openTextDocument(mvcWithComponentsIndex);
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

    // test('Can complete C# code blocks in .cshtml', async () => {
    //     const cshtmlDoc = await vscode.workspace.openTextDocument(mvcWithComponentsIndex);
    //     const editor = await vscode.window.showTextDocument(cshtmlDoc);
    //     const lastLine = new vscode.Position(cshtmlDoc.lineCount - 1, 0);
    //     await editor.edit(edit => edit.insert(lastLine, '@{}'));
    //     await waitForDocumentUpdate(cshtmlDoc.uri, document => document.getText().indexOf('@{}') >= 0);

    //     const completions = await vscode.commands.executeCommand<vscode.CompletionList>(
    //         'vscode.executeCompletionItemProvider',
    //         cshtmlDoc.uri,
    //         new vscode.Position(cshtmlDoc.lineCount - 1, 2));

    //     assertHasCompletion(completions, 'DateTime');
    //     assertHasCompletion(completions, 'DateTimeKind');
    //     assertHasCompletion(completions, 'DateTimeOffset');
    // });

    // test('Can complete C# implicit expressions in .cshtml', async () => {
    //     const cshtmlDoc = await vscode.workspace.openTextDocument(mvcWithComponentsIndex);
    //     const editor = await vscode.window.showTextDocument(cshtmlDoc);
    //     const lastLine = new vscode.Position(cshtmlDoc.lineCount - 1, 0);
    //     await editor.edit(edit => edit.insert(lastLine, '@'));
    //     await waitForDocumentUpdate(cshtmlDoc.uri, document => document.lineAt(document.lineCount - 1).text === '@');

    //     const completions = await vscode.commands.executeCommand<vscode.CompletionList>(
    //         'vscode.executeCompletionItemProvider',
    //         cshtmlDoc.uri,
    //         new vscode.Position(lastLine.line, 1));

    //     assertHasCompletion(completions, 'DateTime');
    //     assertHasCompletion(completions, 'DateTimeKind');
    //     assertHasCompletion(completions, 'DateTimeOffset');
    // });

    // test('Can complete imported C# in .cshtml', async () => {
    //     const cshtmlDoc = await vscode.workspace.openTextDocument(mvcWithComponentsIndex);
    //     const editor = await vscode.window.showTextDocument(cshtmlDoc);
    //     const lastLine = new vscode.Position(cshtmlDoc.lineCount - 1, 0);
    //     await editor.edit(edit => edit.insert(lastLine, '@'));
    //     await waitForDocumentUpdate(cshtmlDoc.uri, document => document.lineAt(document.lineCount - 1).text === '@');

    //     const completions = await vscode.commands.executeCommand<vscode.CompletionList>(
    //         'vscode.executeCompletionItemProvider',
    //         cshtmlDoc.uri,
    //         new vscode.Position(cshtmlDoc.lineCount - 1, 1));

    //     assertHasCompletion(completions, 'TheTime');
    // });

    test('Can complete HTML tag in .cshtml', async () => {
        const cshtmlDoc = await vscode.workspace.openTextDocument(mvcWithComponentsIndex);
        const editor = await vscode.window.showTextDocument(cshtmlDoc);
        const lastLine = new vscode.Position(0, 0);
        await editor.edit(edit => edit.insert(lastLine, '<str'));
        const completions = await vscode.commands.executeCommand<vscode.CompletionList>(
            'vscode.executeCompletionItemProvider',
            cshtmlDoc.uri,
            new vscode.Position(0, 4));

        assertHasCompletion(completions, 'strong');
    });

    // --------- Completions Components -----

    // test('Can perform Completions on directive attributes', async () => {
    //     const counterRazorPath = path.join(componentRoot, 'Components', 'Pages', 'Counter.razor');
    //     const {doc: counterDoc, editor: counterEditor} = await getEditorForFile(counterRazorPath);
    //     const firstLine = new vscode.Position(1, 0);
    //     await counterEditor.edit(edit => edit.insert(firstLine, '<Microsoft.AspNetCore.Components.Forms.EditForm OnV'));

    //     const completions = await vscode.commands.executeCommand<vscode.CompletionList>(
    //         'vscode.executeCompletionItemProvider',
    //         counterDoc.uri,
    //         new vscode.Position(1, 50));

    //     assertHasCompletion(completions, 'OnValidSubmit');
    // });

    // -------- Completions 2.1 ----------

    // test('Can get HTML completions on document open', async () => {
    //     // This test relies on the Index.cshtml document containing at least 1 HTML tag in it.
    //     // For the purposes of this test it locates that tag and tries to get the Html completion
    //     // list from it.
    //     const {doc: doc} = await getEditorForFile(simpleMvc21Index);

    //     const content = doc.getText();
    //     const tagNameIndex = content.indexOf('<') + 1;
    //     const docPosition = doc.positionAt(tagNameIndex);
    //     const completions = await vscode.commands.executeCommand<vscode.CompletionList>(
    //         'vscode.executeCompletionItemProvider',
    //         doc.uri,
    //         docPosition);

    //     assertHasCompletion(completions, 'iframe');
    // });

    // test('Can complete C# code blocks', async () => {
    //     const {doc: doc, editor: editor} = await getEditorForFile(simpleMvc21Index);

    //     const lastLine = new vscode.Position(doc.lineCount - 1, 0);
    //     await editor.edit(edit => edit.insert(lastLine, '@{}'));
    //     await waitForDocumentUpdate(doc.uri, document => document.getText().indexOf('@{}') >= 0);

    //     const completions = await vscode.commands.executeCommand<vscode.CompletionList>(
    //         'vscode.executeCompletionItemProvider',
    //         doc.uri,
    //         new vscode.Position(doc.lineCount - 1, 2));

    //     assertHasCompletion(completions, 'DateTime');
    //     assertHasCompletion(completions, 'DateTimeKind');
    //     assertHasCompletion(completions, 'DateTimeOffset');
    // });

    // test('Can complete C# implicit expressions', async () => {
    //     const {doc: doc, editor: editor} = await getEditorForFile(simpleMvc21Index);
    //     const lastLine = new vscode.Position(doc.lineCount - 1, 0);
    //     await editor.edit(edit => edit.insert(lastLine, '@'));
    //     await waitForDocumentUpdate(doc.uri, document => document.lineAt(document.lineCount - 1).text === '@');

    //     const completions = await vscode.commands.executeCommand<vscode.CompletionList>(
    //         'vscode.executeCompletionItemProvider',
    //         doc.uri,
    //         new vscode.Position(doc.lineCount - 1, 1));

    //     assertHasCompletion(completions, 'DateTime');
    //     assertHasCompletion(completions, 'DateTimeKind');
    //     assertHasCompletion(completions, 'DateTimeOffset');
    // });

    // test('Can complete imported C#', async () => {
    //     const {doc: doc, editor: editor} = await getEditorForFile(simpleMvc21Index);
    //     const lastLine = new vscode.Position(doc.lineCount - 1, 0);
    //     await editor.edit(edit => edit.insert(lastLine, '@'));
    //     await waitForDocumentUpdate(doc.uri, document => document.lineAt(document.lineCount - 1).text === '@');

    //     const completions = await vscode.commands.executeCommand<vscode.CompletionList>(
    //         'vscode.executeCompletionItemProvider',
    //         doc.uri,
    //         new vscode.Position(doc.lineCount - 1, 1));

    //     assertHasCompletion(completions, 'TheTime');
    // });

    // test('Can complete Razor directive', async () => {
    //     const firstLine = new vscode.Position(0, 0);
    //     const {doc: doc, editor: editor} = await getEditorForFile(simpleMvc21Index);

    //     await editor.edit(edit => edit.insert(firstLine, '@\n'));
    //     const completions = await vscode.commands.executeCommand<vscode.CompletionList>(
    //         'vscode.executeCompletionItemProvider',
    //         doc.uri,
    //         new vscode.Position(0, 1));

    //     assertHasCompletion(completions, 'page');
    //     assertHasCompletion(completions, 'inject');
    //     assertHasNoCompletion(completions, 'div');
    // });

    test('Can complete HTML tag', async () => {
        const firstLine = new vscode.Position(0, 0);
        const {doc: doc, editor: editor} = await getEditorForFile(simpleMvc21Index);
        await editor.edit(edit => edit.insert(firstLine, '<str'));
        const completions = await vscode.commands.executeCommand<vscode.CompletionList>(
            'vscode.executeCompletionItemProvider',
            doc.uri,
            new vscode.Position(0, 4));

        assertHasCompletion(completions, 'strong');
    });

    // ------- Completions 1.0 -----------

    test('Can complete Razor directive 1.0', async () => {
        const firstLine = new vscode.Position(0, 0);
        const indexPath = path.join(simpleMvc11Root, 'Views', 'Home', 'Index.cshtml');
        const {doc: doc, editor: editor} = await getEditorForFile(indexPath);
        await editor.edit(edit => edit.insert(firstLine, '@\n'));
        const completions = await vscode.commands.executeCommand<vscode.CompletionList>(
            'vscode.executeCompletionItemProvider',
            doc.uri,
            new vscode.Position(0, 1));

        const hasCompletion = (text: string) => completions!.items.some(item => item.insertText === text);

        assert.ok(!hasCompletion('page'), 'Should not have completion for "page"');
        assert.ok(hasCompletion('inject'), 'Should have completion for "inject"');
        assert.ok(!hasCompletion('div'), 'Should not have completion for "div"');
    });
});
