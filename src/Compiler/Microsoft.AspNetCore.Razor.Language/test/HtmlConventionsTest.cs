﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Xunit;

namespace Microsoft.AspNetCore.Razor.Language;

public class HtmlConventionsTest
{
    public static TheoryData HtmlConversionData
    {
        get
        {
            return new TheoryData<string, string>
                {
                    { "SomeThing", "some-thing" },
                    { "someOtherThing", "some-other-thing" },
                    { "capsONInside", "caps-on-inside" },
                    { "CAPSOnOUTSIDE", "caps-on-outside" },
                    { "ALLCAPS", "allcaps" },
                    { "One1Two2Three3", "one1-two2-three3" },
                    { "ONE1TWO2THREE3", "one1two2three3" },
                    { "First_Second_ThirdHi", "first_second_third-hi" },
                    { "TestÄa", "test-äa" },
                    { "KůňŽluťoučký", "kůň-žluťoučký" },
                };
        }
    }

    [Theory]
    [MemberData(nameof(HtmlConversionData))]
    public void ToHtmlCase_ReturnsExpectedConversions(string input, string expectedOutput)
    {
        // Arrange, Act
        var output = HtmlConventions.ToHtmlCase(input);

        // Assert
        Assert.Equal(output, expectedOutput);
    }
}
