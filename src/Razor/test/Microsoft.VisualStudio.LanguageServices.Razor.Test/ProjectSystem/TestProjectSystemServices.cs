﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Xml;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.XamlTypes;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Build;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.ProjectSystem.References;
using Microsoft.VisualStudio.Threading;
using Moq;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal class TestProjectSystemServices : IUnconfiguredProjectCommonServices
{
    public TestProjectSystemServices(string fullPath, params TestPropertyData[] data)
    {
        ProjectService = new TestProjectService();
        ThreadingService = (TestThreadingService)ProjectService.Services.ThreadingPolicy;

        UnconfiguredProject = new TestUnconfiguredProject(ProjectService, fullPath);
        ProjectService.LoadedUnconfiguredProjects.Add(UnconfiguredProject);

        ActiveConfiguredProject = new TestConfiguredProject(UnconfiguredProject, data);
        UnconfiguredProject.LoadedConfiguredProjects.Add(ActiveConfiguredProject);

        ActiveConfigurationGroupSubscriptionService = new TestActiveConfigurationGroupSubscriptionService();

        TasksService = new TestProjectAsynchronousTasksService(ProjectService, UnconfiguredProject, ActiveConfiguredProject);

        FaultHandlerService = new TestProjectFaultHandlerService();
    }

    public ProjectServices Services { get; }

    public TestProjectService ProjectService { get; }

    public TestUnconfiguredProject UnconfiguredProject { get; }

    public TestConfiguredProject ActiveConfiguredProject { get; }

    public TestActiveConfigurationGroupSubscriptionService ActiveConfigurationGroupSubscriptionService { get; }

    public TestProjectAsynchronousTasksService TasksService { get; }

    public TestThreadingService ThreadingService { get; }

    public TestProjectFaultHandlerService FaultHandlerService { get; }

    IActiveConfigurationGroupSubscriptionService IUnconfiguredProjectCommonServices.ActiveConfigurationGroupSubscriptionService => ActiveConfigurationGroupSubscriptionService;

    IProjectAsynchronousTasksService IUnconfiguredProjectCommonServices.TasksService => TasksService;

    IProjectThreadingService IUnconfiguredProjectCommonServices.ThreadingService => ThreadingService;

    UnconfiguredProject IUnconfiguredProjectCommonServices.UnconfiguredProject => UnconfiguredProject;

    IProjectFaultHandlerService IUnconfiguredProjectCommonServices.FaultHandlerService => FaultHandlerService;

    public IProjectVersionedValue<IProjectSubscriptionUpdate> CreateUpdate(params TestProjectChangeDescription[] descriptions)
    {
        return new ProjectVersionedValue<IProjectSubscriptionUpdate>(
            value: new ProjectSubscriptionUpdate(
                projectChanges: descriptions.ToImmutableDictionary(d => d.After.RuleName, d => (IProjectChangeDescription)d),
                projectConfiguration: ActiveConfiguredProject.ProjectConfiguration),
            dataSourceVersions: ImmutableDictionary<NamedIdentity, IComparable>.Empty);
    }

    public class TestProjectService : IProjectService
    {
        public TestProjectService()
        {
            LoadedUnconfiguredProjects = new List<TestUnconfiguredProject>();

            var services = new Mock<ProjectServices>(MockBehavior.Strict);
            services.Setup(s => s.ProjectService).Returns(this);
            services.Setup(s => s.ThreadingPolicy).Returns(new TestThreadingService());
            Services = services.Object;
        }

        public List<TestUnconfiguredProject> LoadedUnconfiguredProjects { get; }

        public ProjectServices Services { get; }

        IEnumerable<UnconfiguredProject> IProjectService.LoadedUnconfiguredProjects => throw new NotImplementedException();

        ProjectServices IProjectService.Services => Services;

        IProjectCapabilitiesScope IProjectService.Capabilities => throw new NotImplementedException();

        Task<UnconfiguredProject> IProjectService.LoadProjectAsync(string projectLocation, IImmutableSet<string> projectCapabilities)
        {
            throw new NotImplementedException();
        }

        Task<UnconfiguredProject> IProjectService.LoadProjectAsync(XmlReader reader, IImmutableSet<string> projectCapabilities)
        {
            throw new NotImplementedException();
        }

        Task<UnconfiguredProject> IProjectService.LoadProjectAsync(string projectLocation, bool delayAutoLoad, IImmutableSet<string> projectCapabilities)
        {
            throw new NotImplementedException();
        }

        Task IProjectService.UnloadProjectAsync(UnconfiguredProject project)
        {
            throw new NotImplementedException();
        }
    }

    public class TestUnconfiguredProject : UnconfiguredProject
    {
        public TestUnconfiguredProject(TestProjectService projectService, string fullPath)
        {
            ProjectService = projectService;
            FullPath = fullPath;

            LoadedConfiguredProjects = new List<TestConfiguredProject>();
        }

        public TestProjectService ProjectService { get; }

        public string FullPath { get; set; }

        public List<TestConfiguredProject> LoadedConfiguredProjects { get; }

        public UnconfiguredProjectServices Services => throw new NotImplementedException();

        string UnconfiguredProject.FullPath => FullPath;

        bool UnconfiguredProject.RequiresReloadForExternalFileChange => throw new NotImplementedException();

        IProjectCapabilitiesScope UnconfiguredProject.Capabilities => throw new NotImplementedException();

        IProjectService UnconfiguredProject.ProjectService => ProjectService;

        IEnumerable<ConfiguredProject> UnconfiguredProject.LoadedConfiguredProjects => LoadedConfiguredProjects;

        bool UnconfiguredProject.IsLoading => throw new NotImplementedException();

        event AsyncEventHandler UnconfiguredProject.ProjectUnloading
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        event AsyncEventHandler<ProjectRenamedEventArgs> UnconfiguredProject.ProjectRenaming
        {
            add
            {
            }

            remove
            {
            }
        }

        event AsyncEventHandler<ProjectRenamedEventArgs> UnconfiguredProject.ProjectRenamedOnWriter
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        event AsyncEventHandler<ProjectRenamedEventArgs> UnconfiguredProject.ProjectRenamed
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        Task<bool> UnconfiguredProject.CanRenameAsync(string newFilePath)
        {
            throw new NotImplementedException();
        }

        Task<Encoding> UnconfiguredProject.GetFileEncodingAsync()
        {
            throw new NotImplementedException();
        }

        Task<bool> UnconfiguredProject.GetIsDirtyAsync()
        {
            throw new NotImplementedException();
        }

        Task<ConfiguredProject> UnconfiguredProject.GetSuggestedConfiguredProjectAsync()
        {
            throw new NotImplementedException();
        }

        Task<ConfiguredProject> UnconfiguredProject.LoadConfiguredProjectAsync(string name, IImmutableDictionary<string, string> configurationProperties)
        {
            throw new NotImplementedException();
        }

        Task<ConfiguredProject> UnconfiguredProject.LoadConfiguredProjectAsync(ProjectConfiguration projectConfiguration)
        {
            throw new NotImplementedException();
        }

        Task UnconfiguredProject.ReloadAsync(bool immediately)
        {
            throw new NotImplementedException();
        }

        Task UnconfiguredProject.RenameAsync(string newFilePath)
        {
            throw new NotImplementedException();
        }

        Task UnconfiguredProject.SaveAsync(string filePath)
        {
            throw new NotImplementedException();
        }

        Task UnconfiguredProject.SaveCopyAsync(string filePath, Encoding fileEncoding)
        {
            throw new NotImplementedException();
        }

        Task UnconfiguredProject.SaveUserFileAsync()
        {
            throw new NotImplementedException();
        }

        Task UnconfiguredProject.SetFileEncodingAsync(Encoding value)
        {
            throw new NotImplementedException();
        }
    }

    public class TestConfiguredProject : ConfiguredProject
    {
        public TestConfiguredProject(TestUnconfiguredProject unconfiguredProject, TestPropertyData[] data)
        {
            UnconfiguredProject = unconfiguredProject;

            var services = new Mock<ConfiguredProjectServices>(MockBehavior.Strict);
            services.Setup(s => s.AdditionalRuleDefinitions).Returns(new TestAdditionalRuleDefinitionsService());
            services.Setup(s => s.PropertyPagesCatalog).Returns(new TestPropertyPagesCatalogProvider(new TestPropertyPagesCatalog(data)));
            services.Setup(s => s.ProjectService).Returns(UnconfiguredProject.ProjectService);
            Services = services.Object;

            ProjectConfiguration = new StandardProjectConfiguration(
                "Debug|AnyCPU",
                ImmutableDictionary<string, string>.Empty.Add("Configuration", "Debug").Add("Platform", "AnyCPU"));
        }

        public TestUnconfiguredProject UnconfiguredProject { get; }

        public ProjectConfiguration ProjectConfiguration { get; }

        public ConfiguredProjectServices Services { get; }

        IComparable ConfiguredProject.ProjectVersion => throw new NotImplementedException();

        IReceivableSourceBlock<IComparable> ConfiguredProject.ProjectVersionBlock => throw new NotImplementedException();

        ProjectConfiguration ConfiguredProject.ProjectConfiguration => ProjectConfiguration;

        IProjectCapabilitiesScope ConfiguredProject.Capabilities => throw new NotImplementedException();

        UnconfiguredProject ConfiguredProject.UnconfiguredProject => UnconfiguredProject;

        ConfiguredProjectServices ConfiguredProject.Services => Services;

        event AsyncEventHandler ConfiguredProject.ProjectUnloading
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        event EventHandler ConfiguredProject.ProjectChanged
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        event EventHandler ConfiguredProject.ProjectChangedSynchronous
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        void ConfiguredProject.NotifyProjectChange()
        {
            throw new NotImplementedException();
        }
    }

    public class TestAdditionalRuleDefinitionsService : IAdditionalRuleDefinitionsService
    {
        IProjectVersionedValue<IAdditionalRuleDefinitions> IAdditionalRuleDefinitionsService.AdditionalRuleDefinitions => throw new NotImplementedException();

        IReceivableSourceBlock<IProjectVersionedValue<IAdditionalRuleDefinitions>> IProjectValueDataSource<IAdditionalRuleDefinitions>.SourceBlock => throw new NotImplementedException();

        ISourceBlock<IProjectVersionedValue<object>> IProjectValueDataSource.SourceBlock => throw new NotImplementedException();

        NamedIdentity IProjectValueDataSource.DataSourceKey => throw new NotImplementedException();

        IComparable IProjectValueDataSource.DataSourceVersion => throw new NotImplementedException();

        bool IAdditionalRuleDefinitionsService.AddRuleDefinition(string path, string context)
        {
            return false;
        }

        bool IAdditionalRuleDefinitionsService.AddRuleDefinition(Rule rule, string context)
        {
            return false;
        }

        IDisposable IJoinableProjectValueDataSource.Join()
        {
            throw new NotImplementedException();
        }

        bool IAdditionalRuleDefinitionsService.RemoveRuleDefinition(string path)
        {
            return false;
        }

        bool IAdditionalRuleDefinitionsService.RemoveRuleDefinition(Rule rule)
        {
            return false;
        }
    }

    public class TestPropertyPagesCatalogProvider : IPropertyPagesCatalogProvider
    {
        public TestPropertyPagesCatalogProvider(TestPropertyPagesCatalog catalog)
        {
            Catalog = catalog;
            CatalogsByContext = new Dictionary<string, IPropertyPagesCatalog>()
            {
                { "Project", catalog },
            };
        }

        public TestPropertyPagesCatalog Catalog { get; }

        public Dictionary<string, IPropertyPagesCatalog> CatalogsByContext { get; }

        public IReceivableSourceBlock<IProjectVersionedValue<IProjectCatalogSnapshot>> SourceBlock => throw new NotImplementedException();

        public NamedIdentity DataSourceKey => throw new NotImplementedException();

        public IComparable DataSourceVersion => throw new NotImplementedException();

        ISourceBlock<IProjectVersionedValue<object>> IProjectValueDataSource.SourceBlock => throw new NotImplementedException();

        public Task<IPropertyPagesCatalog> GetCatalogAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CatalogsByContext[name]);
        }

        public Task<IImmutableDictionary<string, IPropertyPagesCatalog>> GetCatalogsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IImmutableDictionary<string, IPropertyPagesCatalog>>(CatalogsByContext.ToImmutableDictionary());
        }

        public IPropertyPagesCatalog GetMemoryOnlyCatalog(string context)
        {
            return Catalog;
        }

        public IDisposable Join()
        {
            throw new NotImplementedException();
        }
    }

    public class TestActiveConfigurationGroupSubscriptionService : IActiveConfigurationGroupSubscriptionService
    {
        public TestActiveConfigurationGroupSubscriptionService()
        {
            SourceBlock = new BufferBlock<IProjectVersionedValue<ConfigurationSubscriptionSources>>();
        }

        public BufferBlock<IProjectVersionedValue<ConfigurationSubscriptionSources>> SourceBlock { get; }

        ConfigurationSubscriptionSources IActiveConfigurationGroupSubscriptionService.Current { get; }

        IReceivableSourceBlock<IProjectVersionedValue<ConfigurationSubscriptionSources>> IProjectValueDataSource<ConfigurationSubscriptionSources>.SourceBlock => SourceBlock;

        ISourceBlock<IProjectVersionedValue<object>> IProjectValueDataSource.SourceBlock => SourceBlock;

        NamedIdentity IProjectValueDataSource.DataSourceKey { get; }

        IComparable IProjectValueDataSource.DataSourceVersion { get; }

        IDisposable IJoinableProjectValueDataSource.Join() => null;
    }

    public class TestProjectFaultHandlerService : IProjectFaultHandlerService
    {
        Task IProjectFaultHandlerService.HandleFaultAsync(Exception ex, ErrorReportSettings watsonReportSettings, ProjectFaultSeverity severity, UnconfiguredProject project) => throw new NotImplementedException();

        void IProjectFaultHandlerService.RegisterFaultHandler(Task task, ErrorReportSettings watsonReportSettings, ProjectFaultSeverity severity, UnconfiguredProject project) => throw new NotImplementedException();

        void IProjectFaultHandlerService.RegisterFaultHandler<TResult>(Task<TResult> task, ErrorReportSettings watsonReportSettings, ProjectFaultSeverity severity, UnconfiguredProject project) => throw new NotImplementedException();

        Task IProjectFaultHandlerService.ReportUserFaultAsync(Exception ex, ProjectFaultSeverity severity, UnconfiguredProject project) => throw new NotImplementedException();
    }

    public class TestProjectValueDataSource<T> : IProjectValueDataSource<T>
    {
        public TestProjectValueDataSource(BufferBlock<IProjectVersionedValue<T>> sourceBlock)
        {
            SourceBlock = sourceBlock;
        }

        public BufferBlock<IProjectVersionedValue<T>> SourceBlock { get; }

        IReceivableSourceBlock<IProjectVersionedValue<T>> IProjectValueDataSource<T>.SourceBlock => SourceBlock;

        ISourceBlock<IProjectVersionedValue<object>> IProjectValueDataSource.SourceBlock => throw new NotImplementedException();

        NamedIdentity IProjectValueDataSource.DataSourceKey => throw new NotImplementedException();

        IComparable IProjectValueDataSource.DataSourceVersion => throw new NotImplementedException();

        IDisposable IJoinableProjectValueDataSource.Join()
        {
            throw new NotImplementedException();
        }
    }

    public class TestPropertyPagesCatalog : IPropertyPagesCatalog
    {
        private readonly Dictionary<string, IRule> _data;

        public TestPropertyPagesCatalog(TestPropertyData[] data)
        {
            _data = new Dictionary<string, IRule>();
            foreach (var category in data.GroupBy(p => p.Category))
            {
                _data.Add(
                    category.Key,
                    CreateRule(category.Select(property => CreateProperty(property.PropertyName, property.Value, property.SetValues))));
            }
        }

        private static IRule CreateRule(IEnumerable<IProperty> properties)
        {
            var rule = new Mock<IRule>(MockBehavior.Strict);
            rule
                .Setup(o => o.GetProperty(It.IsAny<string>()))
                .Returns((string propertyName) => properties.FirstOrDefault(p => p.Name == propertyName));

            return rule.Object;
        }

        private static IProperty CreateProperty(string name, object value, List<object> setValues = null)
        {
            var property = new Mock<IProperty>(MockBehavior.Strict);
            property.SetupGet(o => o.Name)
                    .Returns(name);

            property.Setup(o => o.GetValueAsync())
                    .ReturnsAsync(value);

            property.As<IEvaluatedProperty>().Setup(p => p.GetEvaluatedValueAtEndAsync()).ReturnsAsync(value.ToString());
            property.As<IEvaluatedProperty>().Setup(p => p.GetEvaluatedValueAsync()).ReturnsAsync(value.ToString());

            if (setValues != null)
            {
                property
                    .Setup(p => p.SetValueAsync(It.IsAny<object>()))
                    .Callback<object>(obj => setValues.Add(obj))
                    .Returns(() => Task.CompletedTask);
            }

            return property.Object;
        }

        IRule IPropertyPagesCatalog.BindToContext(string schemaName, string file, string itemType, string itemName)
        {
            _data.TryGetValue(schemaName, out var value);
            return value;
        }

        IRule IPropertyPagesCatalog.BindToContext(string schemaName, IProjectPropertiesContext context)
        {
            throw new NotImplementedException();
        }

        IRule IPropertyPagesCatalog.BindToContext(string schemaName, ProjectInstance projectInstance, string itemType, string itemName)
        {
            throw new NotImplementedException();
        }

        IRule IPropertyPagesCatalog.BindToContext(string schemaName, ProjectInstance projectInstance, ITaskItem taskItem)
        {
            throw new NotImplementedException();
        }

        IReadOnlyCollection<string> IPropertyPagesCatalog.GetProjectLevelPropertyPagesSchemas()
        {
            throw new NotImplementedException();
        }

        IReadOnlyCollection<string> IPropertyPagesCatalog.GetPropertyPagesSchemas()
        {
            throw new NotImplementedException();
        }

        IReadOnlyCollection<string> IPropertyPagesCatalog.GetPropertyPagesSchemas(string itemType)
        {
            throw new NotImplementedException();
        }

        IReadOnlyCollection<string> IPropertyPagesCatalog.GetPropertyPagesSchemas(IEnumerable<string> paths)
        {
            throw new NotImplementedException();
        }

        Rule IPropertyPagesCatalog.GetSchema(string schemaName)
        {
            throw new NotImplementedException();
        }
    }

    public class TestAssemblyReferencesService : IAssemblyReferencesService
    {
        public TestAssemblyReferencesService()
        {
            ResolvedReferences = new List<IAssemblyReference>();
        }

        public List<IAssemblyReference> ResolvedReferences { get; }

        Task<AddReferenceResult<IUnresolvedAssemblyReference>> IAssemblyReferencesService.AddAsync(AssemblyName assemblyName, string assemblyPath)
        {
            throw new NotImplementedException();
        }

        Task<bool> IAssemblyReferencesService.CanResolveAsync(AssemblyName assemblyName, string assemblyPath)
        {
            throw new NotImplementedException();
        }

        Task<bool> IAssemblyReferencesService.ContainsAsync(AssemblyName assemblyName, string assemblyPath)
        {
            throw new NotImplementedException();
        }

        Task<IAssemblyReference> IAssemblyReferencesService.GetResolvedReferenceAsync(AssemblyName assemblyName, string assemblyPath)
        {
            throw new NotImplementedException();
        }

        Task<IAssemblyReference> IResolvableReferencesService<IUnresolvedAssemblyReference, IAssemblyReference>.GetResolvedReferenceAsync(IUnresolvedAssemblyReference unresolvedReference)
        {
            throw new NotImplementedException();
        }

        Task<IImmutableSet<IAssemblyReference>> IResolvableReferencesService<IUnresolvedAssemblyReference, IAssemblyReference>.GetResolvedReferencesAsync()
        {
            return Task.FromResult<IImmutableSet<IAssemblyReference>>(ResolvedReferences.ToImmutableHashSet());
        }

        Task<IUnresolvedAssemblyReference> IAssemblyReferencesService.GetUnresolvedReferenceAsync(AssemblyName assemblyName, string assemblyPath)
        {
            throw new NotImplementedException();
        }

        Task<IUnresolvedAssemblyReference> IResolvableReferencesService<IUnresolvedAssemblyReference, IAssemblyReference>.GetUnresolvedReferenceAsync(IAssemblyReference resolvedReference)
        {
            throw new NotImplementedException();
        }

        Task<IImmutableSet<IUnresolvedAssemblyReference>> IResolvableReferencesService<IUnresolvedAssemblyReference, IAssemblyReference>.GetUnresolvedReferencesAsync()
        {
            throw new NotImplementedException();
        }

        Task IAssemblyReferencesService.RemoveAsync(AssemblyName assemblyName, string assemblyPath)
        {
            throw new NotImplementedException();
        }

        Task IResolvableReferencesService<IUnresolvedAssemblyReference, IAssemblyReference>.RemoveAsync(IUnresolvedAssemblyReference reference)
        {
            throw new NotImplementedException();
        }

        Task IResolvableReferencesService<IUnresolvedAssemblyReference, IAssemblyReference>.RemoveAsync(IEnumerable<IUnresolvedAssemblyReference> references)
        {
            throw new NotImplementedException();
        }
    }

    public class TestProjectAsynchronousTasksService : IProjectAsynchronousTasksService, IProjectContext
    {
        public CancellationToken UnloadCancellationToken => CancellationToken.None;

        public TestProjectAsynchronousTasksService(
            IProjectService projectService,
            UnconfiguredProject unconfiguredProject,
            ConfiguredProject configuredProject)
        {
            ProjectService = projectService;
            UnconfiguredProject = unconfiguredProject;
            ConfiguredProject = configuredProject;
        }

        public IProjectService ProjectService { get; }

        public UnconfiguredProject UnconfiguredProject { get; }

        public ConfiguredProject ConfiguredProject { get; }

        public Task DrainCriticalTaskQueueAsync(bool drainCurrentQueueOnly = false, bool throwExceptions = false, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task DrainTaskQueueAsync(bool drainCurrentQueueOnly = false, bool throwExceptions = false, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task DrainTaskQueueAsync(ProjectCriticalOperation operation, bool drainCurrentQueueOnly = false, bool throwExceptions = false, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public bool IsTaskQueueEmpty(ProjectCriticalOperation projectCriticalOperation)
        {
            throw new NotImplementedException();
        }

        public void RegisterAsyncTask(JoinableTask joinableTask, bool registerFaultHandler = false)
        {
        }

        public void RegisterAsyncTask(Task task, bool registerFaultHandler = false)
        {
        }

        public void RegisterAsyncTask(JoinableTask joinableTask, ProjectCriticalOperation operationFlags, bool registerFaultHandler = false)
        {
        }

        public void RegisterCriticalAsyncTask(JoinableTask joinableTask, bool registerFaultHandler = false)
        {
        }
    }

    public class TestThreadingService : IProjectThreadingService
    {
        public TestThreadingService()
        {
            JoinableTaskContext = new JoinableTaskContextNode(new JoinableTaskContext());
            JoinableTaskFactory = new JoinableTaskFactory(JoinableTaskContext.Context);
        }

        public JoinableTaskContextNode JoinableTaskContext { get; }

        public JoinableTaskFactory JoinableTaskFactory { get; }

        public bool IsOnMainThread => throw new NotImplementedException();

        public void ExecuteSynchronously(Func<Task> asyncAction)
        {
            asyncAction().GetAwaiter().GetResult();
        }

        public T ExecuteSynchronously<T>(Func<Task<T>> asyncAction)
        {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            return asyncAction().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
        }

        public void Fork(
            Func<Task> asyncAction,
            JoinableTaskFactory factory = null,
            UnconfiguredProject unconfiguredProject = null,
            ConfiguredProject configuredProject = null,
            ErrorReportSettings watsonReportSettings = null,
            ProjectFaultSeverity faultSeverity = ProjectFaultSeverity.Recoverable,
            ForkOptions options = ForkOptions.Default)
        {
            throw new NotImplementedException();
        }

        public IDisposable SuppressProjectExecutionContext()
        {
            throw new NotImplementedException();
        }

        public void VerifyOnUIThread()
        {
            if (!JoinableTaskContext.IsOnMainThread)
            {
                throw new InvalidOperationException("This isn't the main thread.");
            }
        }
    }
}
