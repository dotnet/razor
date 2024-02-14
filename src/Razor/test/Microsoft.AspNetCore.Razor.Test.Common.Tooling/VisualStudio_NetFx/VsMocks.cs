﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Moq;

namespace Microsoft.AspNetCore.Razor.Test.Common.VisualStudio;

internal static class VsMocks
{
    public static ITextBuffer CreateTextBuffer(bool core)
        => CreateTextBuffer(core ? ContentTypes.RazorCore : ContentTypes.NonRazor);

    public static ITextBuffer CreateTextBuffer(IContentType contentType, PropertyCollection? propertyCollection = null)
    {
        propertyCollection ??= new PropertyCollection();

        return StrictMock.Of<ITextBuffer>(b =>
            b.ContentType == contentType &&
            b.Properties == propertyCollection);
    }

    internal static class ContentTypes
    {
        public static readonly IContentType LegacyRazorCore = Create(RazorConstants.LegacyCoreContentType);
        public static readonly IContentType RazorCore = Create(RazorLanguage.CoreContentType);
        public static readonly IContentType NonRazor = StrictMock.Of<IContentType>(c => c.IsOfType(It.IsAny<string>()) == false);

        public static IContentType Create(params string[] types)
        {
            var mock = new StrictMock<IContentType>();
            mock.Setup(x => x.IsOfType(It.IsAny<string>()))
                .Returns((string type) => Array.IndexOf(types, type) >= 0);

            return mock.Object;
        }
    }

    public static IServiceProvider CreateServiceProvider(Action<IServiceProviderBuilder>? configure = null)
    {
        var builder = new ServiceProviderBuilder();
        configure?.Invoke(builder);
        return builder.Mock.Object;
    }

    public interface IServiceProviderBuilder
    {
        void AddService<T>(T? serviceInstance)
            where T : class;
        void AddService<T>(Func<T?> getServiceCallback)
            where T : class;
        void AddService<T>(object? serviceInstance)
            where T : class;
        void AddService<T>(Func<object?> getServiceCallback)
            where T : class;
        void AddService(Type serviceType, object? serviceInstance);
        void AddService(Type serviceType, Func<object?> getServiceCallback);
    }

    private class ServiceProviderBuilder : IServiceProviderBuilder
    {
        public StrictMock<IServiceProvider> Mock { get; } = new();

        public void AddService<T>(T? serviceInstance)
            where T : class
        {
            AddService(typeof(T), serviceInstance);
        }

        public void AddService<T>(Func<T?> getServiceCallback)
            where T : class
        {
            AddService(typeof(T), getServiceCallback);
        }

        public void AddService<T>(object? serviceInstance)
            where T : class
        {
            AddService(typeof(T), serviceInstance);
        }

        public void AddService<T>(Func<object?> getServiceCallback)
            where T : class
        {
            AddService(typeof(T), getServiceCallback);
        }

        public void AddService(Type serviceType, object? serviceInstance)
        {
            Mock.Setup(x => x.GetService(serviceType))
                .Returns(serviceInstance!);
        }

        public void AddService(Type serviceType, Func<object?> getServiceCallback)
        {
            Mock.Setup<object?>(x => x.GetService(serviceType))
                .Returns(() => getServiceCallback());
        }
    }
}
