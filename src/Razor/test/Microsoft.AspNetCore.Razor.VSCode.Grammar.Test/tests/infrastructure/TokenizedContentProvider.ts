/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * -------------------------------------------------------------------------------------------- */

import * as fs from 'fs';
import { IGrammar, INITIAL, IRawGrammar, ITokenizeLineResult, parseRawGrammar, Registry } from 'vscode-textmate';
import { ITokenizedContent } from './ITokenizedContent';

let razorGrammarCache: IGrammar | undefined;

export async function tokenize(source: string): Promise<ITokenizedContent> {
    const lines = source.split('\n');
    const grammar = await loadRazorGrammar();
    const tokenizedLines: ITokenizeLineResult[] = [];

    let ruleStack = INITIAL;
    for (const line of lines) {
        const tokenizedLine = grammar.tokenizeLine(line, ruleStack);
        tokenizedLines.push(tokenizedLine);
        ruleStack = tokenizedLine.ruleStack;
    }

    const tokenizedContent: ITokenizedContent = {
        source,
        lines,
        tokenizedLines,
    };
    return tokenizedContent;
}

async function loadRazorGrammar(): Promise<IGrammar> {
    if (!razorGrammarCache) {
        const registry = new Registry({
            loadGrammar: loadRawGrammarFromScope,
        });

        const razorGrammar = await registry.loadGrammar('text.aspnetcorerazor');
        if (!razorGrammar) {
            throw new Error('Could not load Razor grammar');
        }

        razorGrammarCache = razorGrammar;
    }

    return razorGrammarCache;
}

async function loadRawGrammarFromScope(scopeName: string): Promise<IRawGrammar> {
    const scopeToRawGrammarFilePath = await getScopeToFilePathRegistry();
    const grammar = scopeToRawGrammarFilePath[scopeName];
    if (!grammar) {
        // Unknown scope
        throw new Error(`Unknown scope name when loading raw grammar: ${scopeName}`);
    }

    return grammar;
}

async function loadRawGrammar(filePath: string): Promise<IRawGrammar> {
    const fileBuffer = await readFile(filePath);
    const fileContent = fileBuffer.toString();
    const rawGrammar = parseRawGrammar(fileContent, filePath);
    return rawGrammar;
}

type GrammarDict = { [key: string]: IRawGrammar };

async function getScopeToFilePathRegistry(): Promise<GrammarDict> {
    const razorRawGrammar = await loadRawGrammar('../../src/Microsoft.AspNetCore.Razor.VSCode.Extension/syntaxes/aspnetcorerazor.tmLanguage.json');
    const htmlRawGrammar = await loadRawGrammar('embeddedGrammars/html.tmLanguage.json');
    const cssRawGrammar = await loadRawGrammar('embeddedGrammars/css.tmLanguage.json');
    const javaScriptRawGrammar = await loadRawGrammar('embeddedGrammars/JavaScript.tmLanguage.json');
    const csharpRawGrammar = await loadRawGrammar('embeddedGrammars/csharp.tmLanguage.json');
    const scopeToRawGrammarFilePath: GrammarDict = {
        'text.aspnetcorerazor': razorRawGrammar,
        'text.html.basic': htmlRawGrammar,
        'source.css': cssRawGrammar,
        'source.js': javaScriptRawGrammar,
        'source.cs': csharpRawGrammar,
    };

    return scopeToRawGrammarFilePath;
}

function readFile(filePath: string): Promise<Buffer> {
    return new Promise((resolve, reject) => {
        fs.readFile(filePath, (error, data) => error ? reject(error) : resolve(data));
    });
}
