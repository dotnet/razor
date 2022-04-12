// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;

namespace Microsoft.VisualStudio.Razor.IntegrationTests
{
    public class EditingTests: AbstractRazorEditorTest
    {
        private const string LengthyFetchDataContent = @"@page ""/fetchdata""

<PageTitle>Weather forecast</PageTitle>

@using BlazorApp70.Data
@inject WeatherForecastService ForecastService

<strong>
    @DateTime.Now.ToString()
</strong>


<div>
    <strong>
        @DateTime.Now.ToString()
    </strong>
</div>


@{
    var abc = true;

    void Bar()
    {

    }
}


<content>
    <div>
    </div>
</content>
<strong>
    @DateTime.Now.ToString()
</strong>

<h1>Weather forecast</h1>

<p>This component demonstrates fetching data from a service.</p>

@if (forecasts == null)
        {
    <p><em>Loading...</em></p>
}
else
{
    <table class=""table"">
        <thead>
            <tr>
                <th>Date</th>
                <th>Temp. (C)</th>
                <th>Temp. (F)</th>
                <th>Summary</th>
            </tr>
        </thead>
        <tbody>
            @foreach(var forecast2 in forecasts)
            {
                <tr>
                    <td>@forecast2.Date.ToShortDateString()</td>
                    <td>@forecast2.TemperatureC</td>
                    <td>@forecast2.TemperatureF</td>
                    <td>@forecast2.Summary</td>
                </tr>
            }
        </tbody>
    </table>
}


@code {
    private WeatherForecast[]? forecasts;

    protected override async Task OnInitializedAsync()
    {
        forecasts = await ForecastService.GetForecastAsync(DateTime.Now);
    }
}
";

        [IdeFact]
        public async Task PasteLargeDocument_PasteSuccessfully()
        {
            // Arrange
            await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, FetchDataRazorFile, HangMitigatingCancellationToken);

            // Act
            await TestServices.Editor.SetTextAsync(LengthyFetchDataContent, HangMitigatingCancellationToken);
            await TestServices.Editor.PlaceCaretAsync("<c", charsOffset: 1, HangMitigatingCancellationToken);

            await TestServices.Editor.WaitForTextChangeAsync(() => TestServices.Input.Send("{ENTER}"), HangMitigatingCancellationToken);

            for(var i = 0; i < 5; i++)
            {
                await TestServices.Editor.UndoTextAsync(HangMitigatingCancellationToken);
            }

            // Assert
            var hasNoErrors = await TestServices.Output.HasNoErrorsAsync(HangMitigatingCancellationToken);
            Assert.True(hasNoErrors);
        }
    }
}
