/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import { assertMatchesSnapshot } from './infrastructure/TestUtilities';

// See GrammarTests.test.ts for details on exporting this test suite instead of running in place.

export function RunExplicitExpressionInAttributeSuite() {
    describe('Explicit Expressions In Attributes', () => {
        it('Class explicit expression', async () => {
            await assertMatchesSnapshot('<div class="@(NavMenuCssClass)"></div>');
        });

        it('Onclick explicit expression', async () => {
            await assertMatchesSnapshot('<button @onclick="@(ToggleNavMenu())"></button>');
        });

        it('Empty', async () => {
            await assertMatchesSnapshot('<button @onclick="@()"></button>');
        });

        it('Single line simple', async () => {
            await assertMatchesSnapshot('<button @onclick="@(DateTime.Now)"></button>');
        });

        it('Single line complex', async () => {
            await assertMatchesSnapshot('<button @onclick="@(456 + new Array<int>(){1,2,3}[0] + await GetValueAsync<string>() ?? someArray[await DoMoreAsync(() => {})])"></button>');
        });

        it('Multi line', async () => {
            await assertMatchesSnapshot(
                `<button @onclick="@(
    Html.BeginForm(
        "Login",
        "Home",
        new
        {
            @class = "someClass",
            notValid = Html.DisplayFor<object>(
                (_) => Model,
                "name",
                "someName",
                new { })
        })
)"></button>`);
        });
    });
}
