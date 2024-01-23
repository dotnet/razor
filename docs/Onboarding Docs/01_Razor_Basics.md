# Razor Basics

Razor is a templating language used in ASP.NET for creating dynamic web pages. It's not a programming language itself, but a server-side markup syntax for embedding server-side code (C# or VB.NET) into HTML.

In a Razor file, you can use a combination of several languages:

| Language | Usage | Supported in .NET Core and .NET 5+ |
| --- | --- | --- |
| **Razor syntax** | Used to embed and execute server-side code within HTML. | Yes |
| **C#** | The server-side language used within Razor templates. Most commonly used with Razor. | Yes |
| **HTML** | Used to structure the content on web pages. | Yes |
| **JavaScript** | Used for client-side scripting in Razor templates. | Yes |
| **CSS** | Used for styling web pages. | Yes |
| **VB.NET** | Can be used in Razor syntax in the older .NET Framework. | No |

Please note that while Razor syntax does support VB.NET in the older .NET Framework, VB.NET is not supported in .NET Core or .NET 5 and onwards for Razor views. In these newer frameworks, only C# is supported for Razor views.

## Razor File Types

Razor files typically have the `.cshtml`, `.vbhtml` or `.razor` extension. The distinction is not in the file extension, but in how and where the file is used within the application.

| File Extension | Type | Description | Usage |
| --- | --- | --- | --- |
| `.cshtml` | Razor View | Part of the MVC (Model-View-Controller) pattern, where the View is responsible for the presentation logic. Located within the Views folder of an MVC application and associated with a Controller. | Used in MVC applications for complex scenarios where separation of concerns is important. |
| `.cshtml` | Razor Page | A page-based programming model that makes building web UI easier and more productive. Located within the Pages folder of a Razor Pages application and includes a `@page` directive at the top. | Used in Razor Pages applications for simpler scenarios where a full MVC model might be overkill. |
| `.razor` | Razor Component (Blazor) | Used in Blazor, a framework for building interactive client-side web UI with .NET. Each `.razor` file is a self-contained component that can include both the markup and the processing logic. | Used in Blazor applications for building interactive client-side web UIs. |
| `.vbhtml` | Razor View (VB.NET) | Part of the MVC (Model-View-Controller) pattern, where the View is responsible for the presentation logic. Located within the Views folder of an MVC application and associated with a Controller. | Used in older MVC applications written in VB.NET. |

## Razor Editors: Legacy vs New

| Aspect | Razor Legacy | Legacy .NET Core Razor Editor | New .NET Core Razor Editor |
| --- | --- | --- | --- |
| **Introduction** | Introduced with ASP.NET MVC 3. | Older Razor editor for ASP.NET Core projects. | Updated Razor editor introduced in Visual Studio 2019 version 16.8. |
| **Usage** | Used in ASP.NET MVC and ASP.NET Web Pages applications. | Used for editing Razor views and pages in ASP.NET Core projects. | Used for editing Razor views and pages in ASP.NET Core projects. |
| **Source code** | Closed source. | Closed source. | [Open source on GitHub](https://github.com/dotnet/razor/) |
| **File Extensions** | `.cshtml` for C#, `.vbhtml` for VB.NET. | N/A | N/A |
| **Functionality** | Creates dynamic web pages that combine HTML and server-side code. | Provides basic features like syntax highlighting and IntelliSense for Razor syntax. | Provides improved functionality and performance, including better IntelliSense, improved syntax highlighting, support for Razor formatting, better diagnostics, and features like "Go to Definition" and "Find All References" for Razor components and their parameters. |
| **Support** | Still supported for maintaining existing applications. New development typically done using newer versions of Razor in ASP.NET Core. | N/A | N/A |
| **Configuration** | N/A | To switch to the legacy editor, go to "Tools" > "Options" > "Environment" > "Preview Features" and uncheck the box that says "Use the new Razor editor for .NET Core apps". | To switch to the new editor, go to "Tools" > "Options" > "Environment" > "Preview Features" and check the box that says "Use the new Razor editor for .NET Core apps". |
| **Implementation** | N/A | Had a more monolithic design, with all language services being provided directly by the editor. Did not use LSP or TextMate grammars, and its integration with the rest of Visual Studio was more limited. | Implemented using the Language Server Protocol (LSP), which allows it to provide language services like IntelliSense, diagnostics, and code actions. Uses TextMate grammars for syntax highlighting, providing consistent and accurate highlighting for Razor, C#, and HTML. Integrated with the rest of Visual Studio through the editor API, allowing it to provide features like "Go to Definition", "Find All References", and code actions that work seamlessly with the rest of the IDE. Includes special support for Blazor. |

## Razor Support Across ASP.NET Versions

Different versions of ASP.NET support different features of Razor. Here's a summary:

| TFM | Razor Support |
| --- | --- |
| **.NET Framework (<= 4.8)** | Supports Razor syntax with C# and VB.NET. Used in ASP.NET MVC and ASP.NET Web Pages applications. |
| **.NET Core 1.x - 3.1** | Supports Razor syntax with C# only. Used in ASP.NET Core MVC and Razor Pages applications. |
| **.NET 5+** | Supports Razor syntax with C# only. Used in ASP.NET Core MVC, Razor Pages, and Blazor applications. |

This table provides a clear overview of the Razor support in different versions of ASP.NET.