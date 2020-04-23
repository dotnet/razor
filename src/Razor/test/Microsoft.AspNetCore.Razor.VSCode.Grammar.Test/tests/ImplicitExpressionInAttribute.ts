/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import { assertMatchesSnapshot } from './infrastructure/TestUtilities';

// See GrammarTests.test.ts for details on exporting this test suite instead of running in place.

export function RunImplicitExpressionInAttributeSuite() {
    describe('Implicit Expressions In Attributes', () => {
        it('Class implicit expression', async () => {
            await assertMatchesSnapshot('<div class="@NavMenuCssClass"></div>');
        });

        it('Onclick implicit expression', async () => {
            await assertMatchesSnapshot('<button @onclick="@ToggleNavMenu()"></button>');
        });

        it('Onclick function name', async () => {
            await assertMatchesSnapshot('<button @onclick="ToggleNavMenu"></button>');
        });

        it('Email address, not implicit expression', async () => {
            await assertMatchesSnapshot('<button @onclick="abc@DateTime.Now"></button>');
        });

        it('Parenthesis prefix', async () => {
            await assertMatchesSnapshot('<button @onclick=")@DateTime.Now"></button>');
        });

        it('Punctuation prefix', async () => {
            await assertMatchesSnapshot('<button @onclick=".@DateTime.Now"></button>');
        });

        it('Close curly prefix', async () => {
            await assertMatchesSnapshot('<button @onclick="}@DateTime.Now"></button>');
        });

        it('Empty', async () => {
            await assertMatchesSnapshot('<button @onclick="@"></button>');
        });

        it('Open curly suffix', async () => {
            await assertMatchesSnapshot('<button @onclick="@DateTime.Now{"></button>');
        });

        it('Close curly suffix', async () => {
            await assertMatchesSnapshot('<button @onclick="@DateTime.Now}"></button>');
        });

        it('Close parenthesis suffix', async () => {
            await assertMatchesSnapshot('<button @onclick="@DateTime.Now)"></button>');
        });

        it('Close parenthesis suffix', async () => {
            await assertMatchesSnapshot('<button @onclick="@DateTime.Now]"></button>');
        });

        it('Single line simple', async () => {
            await assertMatchesSnapshot('<button @onclick="@DateTime.Now"></button>');
        });

        it('Awaited property', async () => {
            await assertMatchesSnapshot('<button @onclick="@await AsyncProperty"></button>');
        });

        it('Awaited method invocation', async () => {
            await assertMatchesSnapshot('<button @onclick="@await AsyncMethod()"></button>');
        });

        it('Single line complex', async () => {
            await assertMatchesSnapshot('<button @onclick="@DateTime!.Now()[1234 + 5678](abc()!.Current, 1 + 2 + getValue())?.ToString[123](() => 456)"></button>');
        });

        it('Combined with HTML', async () => {
            await assertMatchesSnapshot('<button @onclick="<p>@DateTime.Now</p>"></button>');
        });

        it('Multi line', async () => {
            await assertMatchesSnapshot(
                `<button @onclick="@DateTime!.Now()[1234 +
5678](
abc()!.Current,
1 + 2 + getValue())?.ToString[123](
() =>
{
    var x = 123;
    var y = true;

    return y ? x : 457;
})"></button>`);
        });
    });
}
