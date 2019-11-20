/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as vscode from 'vscode';
import { backgroundVirtualCSharpSuffix, virtualCSharpSuffix, virtualHtmlSuffix } from './RazorDocumentFactory';
import { RazorLanguageFeatureBase } from './RazorLanguageFeatureBase';
import { LanguageKind } from './RPC/LanguageKind';
import { getUriPath } from './UriPaths';

export class RazorReferenceProvider
    extends RazorLanguageFeatureBase
    implements vscode.ReferenceProvider {

    public async provideReferences(
        document: vscode.TextDocument,
        position: vscode.Position,
        context: vscode.ReferenceContext,
        token: vscode.CancellationToken) {

        const projection = await this.getProjection(document, position, token);
        if (!projection) {
            return;
        }

        const references = await vscode.commands.executeCommand<vscode.Location[]>(
            'vscode.executeReferenceProvider',
            projection.uri,
            projection.position) as vscode.Location[];

        if (projection.languageKind === LanguageKind.CSharp) {
            for (const reference of references) {
                const uriPath = getUriPath(reference.uri);
                if (uriPath.endsWith(virtualCSharpSuffix)) {
                    let razorFilePath = uriPath.replace(backgroundVirtualCSharpSuffix, '');
                    razorFilePath = razorFilePath.replace(virtualCSharpSuffix, '');
                    const razorFile = vscode.Uri.file(razorFilePath);
                    const res = await this.serviceClient.mapToDocumentRange(
                        projection.languageKind,
                        reference.range,
                        razorFile);
                    if (res) {
                        reference.range = res.range;
                        reference.uri = razorFile;
                    }
                }
            }
        }

        if (projection.languageKind === LanguageKind.Html) {
            references.forEach(reference => {
                // Because the line pragmas for html are generated referencing the projected document
                // we need to remap their file locations to reference the top level Razor document.
                const uriPath = getUriPath(reference.uri);
                const path = uriPath.replace(virtualHtmlSuffix, '');
                reference.uri = vscode.Uri.file(path);
            });
        }

        return references;
    }
}
