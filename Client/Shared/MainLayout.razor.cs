namespace BlazorRepl.Client.Shared
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using BlazorRepl.Client.Components;
    using BlazorRepl.Core;
    using BlazorRepl.Core.PackageInstallation;
    using Microsoft.AspNetCore.Components;

    public partial class MainLayout : IDisposable
    {
        [Inject]
        public HttpClient HttpClient { get; set; }

        private PageNotifications PageNotificationsComponent { get; set; }

        public void Dispose() => this.PageNotificationsComponent?.Dispose();

        protected override async Task OnInitializedAsync()
        {
            // Order of init and add to cache is really important
            await CompilationService.InitAsync(this.HttpClient);

            NuGetRemoteDependencyProvider.AddBaseAssemblyPackageDependenciesToCache(CompilationService.BaseAssemblyPackageVersionMappings);

            await base.OnInitializedAsync();
        }
    }
}
