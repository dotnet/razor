/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * -------------------------------------------------------------------------------------------- */

import { RazorLogger } from 'microsoft.aspnetcore.razor.vscode/dist/RazorLogger';
import * as vscode from 'microsoft.aspnetcore.razor.vscode/dist/vscodeAdapter';
import * as os from 'os';
import { TestUri } from './TestUri';

type Log = { [logIdentifier: string]: string[] }

export interface TestVSCodeApi extends vscode.api {
    getOutputChannelSink(): Log;
    getRazorOutputChannel(): string[];
    setWorkspaceDocuments(...workspaceDocuments: vscode.TextDocument[]): void;
    setExtensions(...extensions: Array<vscode.Extension<any>>): void;
}

export function createTestVSCodeApi(): TestVSCodeApi {
    const workspaceDocuments: vscode.TextDocument[] = [];
    const extensions: Array<vscode.Extension<any>> = [];
    const outputChannelSink: { [logIdentifier: string]: string[] } = {};
    return {
        // Non-VSCode APIs, for tests only

        getOutputChannelSink: (): Log => outputChannelSink,
        getRazorOutputChannel: (): string[] => {
            let razorOutputChannel = outputChannelSink[RazorLogger.logName];
            if (!razorOutputChannel) {
                razorOutputChannel = [];
                outputChannelSink[RazorLogger.logName] = razorOutputChannel;
            }

            return razorOutputChannel;
        },
        setWorkspaceDocuments: (...documents): void => {
            workspaceDocuments.length = 0;
            workspaceDocuments.push(...documents);
        },
        setExtensions: (...exts: Array<vscode.Extension<any>>): void => {
            extensions.length = 0;
            extensions.push(...exts);
        },

        // VSCode APIs

        commands: {
            executeCommand: <T>(_command: string, ..._rest: any[]): Promise<T | undefined> => {
                throw new Error('Not Implemented');
            },
            registerCommand: (_command: string, _callback: (...args: any[]) => any, _thisArg?: any): vscode.Disposable => {
                throw new Error('Not Implemented');
            },
        },
        languages: {
            match: (_selector: vscode.DocumentSelector, _document: vscode.TextDocument): number => {
                throw new Error('Not Implemented');
            },
            registerDocumentSemanticTokensProvider: (
                _selector: vscode.DocumentSelector,
                _provider: vscode.DocumentSemanticTokensProvider,
                _legend: vscode.SemanticTokensLegend): vscode.Disposable => {
                throw new Error('Not Implemented');
            },
            registerDocumentRangeSemanticTokensProvider: (
                _selector: vscode.DocumentSelector,
                _provider: vscode.DocumentRangeSemanticTokensProvider,
                _legend: vscode.SemanticTokensLegend): vscode.Disposable => {
                throw new Error('Not Implemented');
            },
        },
        window: {
            activeTextEditor: undefined,
            showInformationMessage: <T extends vscode.MessageItem>(_message: string, ..._items: T[]): Thenable<T|undefined> => {
                throw new Error('Not Implemented');
            },
            showWarningMessage: <T extends vscode.MessageItem>(_message: string, ..._items: T[]): Thenable<T|undefined> => {
                throw new Error('Not Implemented');
            },
            showErrorMessage: (_message: string, ..._items: string[]): Thenable<undefined> => {
                throw new Error('Not Implemented');
            },
            createOutputChannel: (name: string): vscode.OutputChannel => {
                if (!outputChannelSink[name]) {
                    outputChannelSink[name] = [];
                }
                const outputChannel: vscode.OutputChannel = {
                    name,
                    append: (message) => outputChannelSink[name].push(message),
                    appendLine: (message) => outputChannelSink[name].push(`${message}${os.EOL}`),
                    clear: () => outputChannelSink[name].length = 0,
                    dispose: Function,
                    hide: Function,
                    show: () => {},
                };

                return outputChannel;
            },
            registerWebviewPanelSerializer: (_viewType: string, _serializer: vscode.WebviewPanelSerializer): vscode.Disposable => {
                throw new Error('Not implemented');
            },
        },
        workspace: {
            openTextDocument: (uri: vscode.Uri): Thenable<vscode.TextDocument> => {
                return new Promise((resolve) => {
                    for (const document of workspaceDocuments) {
                        if (document.uri === uri) {
                            resolve(document);
                        }
                    }
                    resolve(undefined);
                });
            },
            getConfiguration: (_section?: string, _resource?: vscode.Uri): vscode.WorkspaceConfiguration => {
                throw new Error('Not Implemented');
            },
            asRelativePath: (_pathOrUri: string | vscode.Uri, _includeWorkspaceFolder?: boolean): string => {
                throw new Error('Not Implemented');
            },
            createFileSystemWatcher: (_globPattern: vscode.GlobPattern, _ignoreCreateEvents?: boolean, _ignoreChangeEvents?: boolean, _ignoreDeleteEvents?: boolean): vscode.FileSystemWatcher => {
                throw new Error('Not Implemented');
            },
            onDidChangeConfiguration: (_listener: (e: vscode.ConfigurationChangeEvent) => any, _thisArgs?: any, _disposables?: vscode.Disposable[]): vscode.Disposable => {
                throw new Error('Not Implemented');
            },
        },
        extensions: {
            getExtension: (id): vscode.Extension<any> | any => {
                for (const extension of extensions) {
                    if (extension.id === id) {
                        return extension;
                    }
                }
            },
            all: extensions,
        },
        Uri: {
            parse: (path): vscode.Uri => new TestUri(path),
        },
        Disposable: {
            from: (..._disposableLikes: Array<{ dispose: () => any }>): any => {
                throw new Error('Not Implemented');
            },
        },
        version: '',
    };
}
