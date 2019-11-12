/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as vscode from 'vscode';
import { RazorLanguageFeatureBase } from './RazorLanguageFeatureBase';

export class RazorDefinitionProvider
    extends RazorLanguageFeatureBase
    implements vscode.DefinitionProvider {

    public async provideDefinition(
        document: vscode.TextDocument, position: vscode.Position,
        token: vscode.CancellationToken) {

        const projection = await this.getProjection(document, position, token);
        if (projection) {
            const definitions = await vscode.commands.executeCommand<vscode.Definition>(
                'vscode.executeDefinitionProvider',
                projection.uri,
                projection.position);
            // C# knows about line pragma, if we're getting a direction to a virtual c# document
            // that means the piece we're trying to navigate to does not have a representation in the
            // top level file.
            const result = (definitions as vscode.Location[]).filter(element => {
                return !(element.uri.path.endsWith('__virtual.cs'));
            });

            result.forEach(element => {
                const uri = element.uri.fsPath || element.uri.path;
                const path = uri.toString().replace('.cshtml__virtual.html', '.cshtml');
                element.uri = vscode.Uri.file(path);
            });

            return result;
        }
    }
}
