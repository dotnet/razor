# Troubleshooting formatting issues

There are times when Razor formatting might not do what you want, or might seem to not do
anything. There are a few explanations for this, and a few steps you can take to help us
investigate further.

## Formatting seems to do nothing

It is a feature of the formatting engine that if a change is detected that would result in any
non-whitespace character being changed, the entire formatting operation will be abandoned.
This is to avoid having a formatting operation change your actual code and its meaning,
rather than just how it looks. Similarly, if the number of diagnostics in a document
changes before and after formatting, we assume that something has been broken by
formatting and abandon the operation.

When either of these two fail-safes trigger, a message should be written to the Razor log in
the Output window.

## Formatting does something I don't like

The Razor formatting is reasonably unopinionated, but it defers to the Html formatter that is
built into the IDE you're using, and to Roslyn for C# formatting. Sometimes this means it
can format code in ways you don't agree with, or that perhaps don't match your formatting
settings for that language. For example, `.editorconfig` is not supported
(https://github.com/dotnet/razor/issues/4406), so settings defined there may not be applied.

If formatting does something to change your code in a way you think is incorrect, please file
an issue, but ensure you include the "before", the "after", and what you wish the result
to have been. Ideally this should be done by including the actual source files rather than a
screenshot. If it's possible to reduce the files to a minimal repro, it will make the issue
easier to track down.

It is usually helpful in these situations to also turn on formatting logging and attach the
detailed logs to the issue.

## Formatting crashes

If you see an error when formatting your document, it usually means that either C# or Html
has made a formatting change that Razor is failing to deal with. In those situations we can
usually fix the problem easily, but first we need to be able to understand exactly what it
is. To make that process easier (or possible), the best thing to do is to turn on
"Formatting Logging" and attach the detailed logs to any issue you create.

## Turning on Formatting Logging

To enable detailed logging of the formatting system, set an environment variable called
`RazorFormattingLogPath` to a folder on your machine, then start your IDE of choice. Perform
the formatting operation that fails or breaks, and Razor will create a sub-folder for the
operation and write a number of log files there. Including these files (feel free to ZIP
them up; they should compress well) in any issue you report — either via the in-built
feedback mechanism or when creating an issue on GitHub (https://github.com/dotnet/razor) —
will greatly help us track down the exact problem you are encountering.

> [!NOTE]
> The logs contain your Razor file contents and full file paths, so be aware of this
when uploading them to public sites like GitHub if that is a concern for you.
