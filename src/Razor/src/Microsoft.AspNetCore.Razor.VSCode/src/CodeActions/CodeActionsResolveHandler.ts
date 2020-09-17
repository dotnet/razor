/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

// import * as vscode from 'vscode';
// import { RequestType } from 'vscode-languageclient';
// import { RazorDocumentManager } from '../RazorDocumentManager';
// import { RazorLanguageServerClient } from '../RazorLanguageServerClient';
// import { RazorLogger } from '../RazorLogger';
// import { RazorCodeAction } from '../RPC/RazorCodeAction';
// import { convertRangeFromSerializable } from '../RPC/SerializableRange';

// export class CodeActionsResolveHandler {

//     private static readonly resolveCodeActionsEndpoint = 'razor/resolveCodeActions';
//     private codeActionResolveRequestType: RequestType<RazorCodeAction, RazorCodeAction, any, any> = new RequestType(CodeActionsResolveHandler.resolveCodeActionsEndpoint);

//     constructor(
//         private readonly documentManager: RazorDocumentManager,
//         private readonly serverClient: RazorLanguageServerClient,
//         private readonly logger: RazorLogger) {
//     }

//     public register() {
//         // tslint:disable-next-line: no-floating-promises
//         this.serverClient.onRequestWithParams<RazorCodeAction, RazorCodeAction, any, any>(
//             this.codeActionResolveRequestType,
//             async (request, token) => this.resolveCodeAction(request, token));
//     }

//     private async resolveCodeAction(
//         codeAction: RazorCodeAction,
//         cancellationToken: vscode.CancellationToken) {
//         try {
//             const razorDocumentUri = vscode.Uri.parse(codeActionParams.textDocument.uri);
//             const razorDocument = await this.documentManager.getDocument(razorDocumentUri);
//             if (razorDocument === undefined) {
//                 return null;
//             }

//             const virtualCSharpUri = razorDocument.csharpDocument.uri;

//             const range = convertRangeFromSerializable(codeActionParams.range);

//             if (commands[0].arguments !== undefined &&
//                 commands[0].arguments.length === 2) {
//                 const results = await vscode.commands.executeCommand<vscode.WorkspaceEdit>(
//                     'omnisharp.resolveCodeAction',
//                     commands[0].arguments[0],
//                     commands[0].arguments[1]) as vscode.WorkspaceEdit;
//                 if (results === undefined) { // so eslint doesn't complain REMOVE THIS
//                     return commands.map(c => this.commandAsCodeAction(c));
//                 }
//             }

//             if (commands.length === 0) {
//                 return null;
//             }

//             // return commands.map(c => this.commandAsCodeAction(c));
//         } catch (error) {
//             this.logger.logWarning(`${CodeActionsResolveHandler.resolveCodeActionsEndpoint} failed with ${error}`);
//         }

//         return  null;
//     }

//     private commandAsCodeAction(command: vscode.Command): RazorCodeAction {
//         return { title: command.title } as RazorCodeAction;
//     }
// }
