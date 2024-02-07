// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Test.Common;

namespace Microsoft.VisualStudio.LanguageServices.Razor;

internal static class VsMocks
{
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
