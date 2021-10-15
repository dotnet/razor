namespace Microsoft.VisualStudio.Razor.ServiceHub
{
    public static class LanguageServerFactory
    {
        public static async Task<IRazorService> CreateServiceAsync()
        {
            var serviceContainer = await RazorToolWindowPackage.Instance.GetServiceAsync(typeof(SVsBrokeredServiceContainer)) as IBrokeredServiceContainer;
            var sb = serviceContainer.GetFullAccessServiceBroker();

#pragma warning disable ISB001 // Dispose of proxies
            return await sb.GetProxyAsync<IInteractiveService>(RpcDescriptor.InteractiveServiceDescriptor, NotebookToolWindowPackage.Instance.DisposalToken);
#pragma warning restore ISB001 // Dispose of proxies
        }
    }
}
