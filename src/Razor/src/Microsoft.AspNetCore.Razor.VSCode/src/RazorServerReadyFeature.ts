/* ---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 * --------------------------------------------------------------------------------------------- */

import {
    ClientCapabilities,
    DocumentSelector,
    InitializeParams,
    RequestType,
    ServerCapabilities,
    StaticFeature,
} from 'vscode-languageclient/lib/main';
import { RazorLanguageServerClient } from './RazorLanguageServerClient';

export class RazorServerReadyFeature implements StaticFeature {
    private static readonly razorServerReadyEndpoint = 'razor/serverReady';
    public fillInitializeParams?: ((params: InitializeParams) => void) | undefined;
    private razorServerReadyHandlerType: RequestType<void, void, any, any> = new RequestType(RazorServerReadyFeature.razorServerReadyEndpoint);

    constructor(private client: RazorLanguageServerClient) {}

    public fillClientCapabilities(capabilities: ClientCapabilities): void {
        return;
    }

    public async initialize(capabilities: ServerCapabilities, documentSelector: DocumentSelector | undefined): Promise<void> {
        this.client.onRequestWithParams<void, void,  any, any>(
            this.razorServerReadyHandlerType,
            async (request, token) => this.handleRazorServerReady());
    }

    private handleRazorServerReady(): void {
        return;
    }
}
