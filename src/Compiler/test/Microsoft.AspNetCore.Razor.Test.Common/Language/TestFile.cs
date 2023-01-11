﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language;

public class TestFile
{
    private TestFile(string resourceName, Assembly assembly)
    {
        Assembly = assembly;
        ResourceName = Assembly.GetName().Name + "." + resourceName.Replace('/', '.').Replace('\\', '.');
    }

    public Assembly Assembly { get; }

    public string ResourceName { get; }

    public static TestFile Create(string resourceName, Type type)
    {
        return new TestFile(resourceName, type.GetTypeInfo().Assembly);
    }

    public static TestFile Create(string resourceName, Assembly assembly)
    {
        return new TestFile(resourceName, assembly);
    }

    public Stream OpenRead()
    {
        var stream = Assembly.GetManifestResourceStream(ResourceName);
        if (stream == null)
        {
            Assert.True(false, string.Format(CultureInfo.InvariantCulture, "Manifest resource: {0} not found", ResourceName));
        }

        return stream;
    }

    public bool Exists()
    {
        var resourceNames = Assembly.GetManifestResourceNames();
        foreach (var resourceName in resourceNames)
        {
            // Resource names are case-sensitive.
            if (string.Equals(ResourceName, resourceName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public string ReadAllText()
    {
        using (var reader = new StreamReader(OpenRead()))
        {
            // The .Replace() calls normalize line endings, in case you get \n instead of \r\n
            // since all the unit tests rely on the assumption that the files will have \r\n endings.
            return reader.ReadToEnd().Replace("\r", "").Replace("\n", "\r\n");
        }
    }

    /// <summary>
    /// Saves the file to the specified path.
    /// </summary>
    public void Save(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using (var outStream = File.Create(filePath))
        {
            using (var inStream = OpenRead())
            {
                inStream.CopyTo(outStream);
            }
        }
    }
}
