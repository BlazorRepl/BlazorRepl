namespace BlazorRepl.Client.Components
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading.Tasks;
    using BlazorRepl.Client.Models;
    using BlazorRepl.Core;
    using BlazorRepl.Core.PackageInstallation;
    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    public partial class PackageManager : IAsyncDisposable
    {
        private DotNetObjectReference<PackageManager> dotNetInstance;

        [Inject]
        public IJSUnmarshalledRuntime UnmarshalledJsRuntime { get; set; }

        [Inject]
        public HttpClient Http { get; set; }

        [Inject]
        public CompilationService CompilationService { get; set; }

        [Inject]
        public NuGetPackageManagementService NuGetPackageManagementService { get; set; }

        [Parameter]
        public bool Visible { get; set; }

        [Parameter]
        public EventCallback<bool> VisibleChanged { get; set; }

        [Parameter]
        public string SessionId { get; set; }

        [Parameter]
        public ICollection<Package> PackagesToRestore { get; set; }

        [Parameter]
        public bool Loading { get; set; }

        [Parameter]
        public EventCallback<bool> LoadingChanged { get; set; }

        [Parameter]
        public Func<string, Task> UpdateLoaderTextFunc { get; set; }

        [CascadingParameter]
        private PageNotifications PageNotificationsComponent { get; set; }

        private string NuGetPackageName { get; set; }

        private IEnumerable<PackageLicenseInfo> PackagesToAcceptLicense { get; set; } = Enumerable.Empty<PackageLicenseInfo>();

        private bool LicensePopupVisible { get; set; }

        private string SelectedNuGetPackageName { get; set; }

        private string SelectedNuGetPackageVersion { get; set; }

        private IEnumerable<string> NuGetPackages { get; set; } = new List<string>();

        private IEnumerable<string> NuGetPackageVersions { get; set; } = new List<string>();

        private string VisibleClass => this.Visible ? "show" : string.Empty;

        private string DisplayStyle => this.Visible ? string.Empty : "display: none;";

        public ValueTask DisposeAsync()
        {
            this.dotNetInstance?.Dispose();
            this.PageNotificationsComponent?.Dispose();

            return ValueTask.CompletedTask;
        }

        public async Task RestorePackagesAsync(bool handleLoading = false)
        {
            if (handleLoading)
            {
                this.Loading = true;
                await this.LoadingChanged.InvokeAsync(this.Loading);
            }

            var index = 1;
            foreach (var package in this.PackagesToRestore)
            {
                if (this.UpdateLoaderTextFunc != null)
                {
                    await this.UpdateLoaderTextFunc($"[{index}/{this.PackagesToRestore.Count}] Restoring package: {package.Name}");
                    index++;
                }

                await this.NuGetPackageManagementService.PreparePackageForDownloadAsync(package.Name, package.Version);

                await this.InstallNuGetPackageAsync();
            }

            this.PackagesToRestore.Clear();

            if (handleLoading)
            {
                this.Loading = false;
                await this.LoadingChanged.InvokeAsync(this.Loading);
            }
        }

        public IReadOnlyCollection<Package> GetInstalledPackages() => this.NuGetPackageManagementService.InstalledPackages;

        [JSInvokable]
        public Task CloseAsync() => this.CloseInternalAsync();

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                this.dotNetInstance = DotNetObjectReference.Create(this);
            }

            await base.OnAfterRenderAsync(firstRender);
        }

        // TODO: rename to search and move to the NuGet package manager
        // TODO: handle no packages found (ex. "Newtonsoft.Json 12.0.3")
        private async Task GetNuGetPackages()
        {
            this.NuGetPackages = await this.NuGetPackageManagementService.SearchPackagesAsync(this.NuGetPackageName);

            this.SelectedNuGetPackageName = null;
        }

        private async Task SelectNuGetPackage(string selectedPackage)
        {
            this.SelectedNuGetPackageName = selectedPackage;
            this.NuGetPackageName = selectedPackage;

            this.NuGetPackageVersions = await this.NuGetPackageManagementService.GetPackageVersionsAsync(this.SelectedNuGetPackageName);

            this.SelectedNuGetPackageVersion = this.NuGetPackageVersions.FirstOrDefault();
        }

        private async Task PreparePackageToInstallAsync()
        {
            var prepareResult = await this.NuGetPackageManagementService.PreparePackageForDownloadAsync(
                this.SelectedNuGetPackageName,
                this.SelectedNuGetPackageVersion);

            this.PackagesToAcceptLicense = prepareResult.PackagesToAcceptLicense ?? Enumerable.Empty<PackageLicenseInfo>();

            if (this.PackagesToAcceptLicense.Any())
            {
                this.LicensePopupVisible = true;
            }
            else
            {
                await this.ProceedToPackageInstallationAsync();
            }
        }

        private void DeclineLicense()
        {
            this.NuGetPackageManagementService.CancelPackageInstallation();

            this.LicensePopupVisible = false;
        }

        private async Task ProceedToPackageInstallationAsync()
        {
            await this.InstallNuGetPackageAsync();

            this.PageNotificationsComponent.AddNotification(
                NotificationType.Info,
                $"{this.SelectedNuGetPackageName} package is successfully installed.");

            this.LicensePopupVisible = false;
        }

        private async Task InstallNuGetPackageAsync()
        {
            var sw = Stopwatch.StartNew();

            // TODO: extract custom object for the package contents to prevent filtering
            var packageContents = await this.NuGetPackageManagementService.DownloadPackagesContentsAsync();
            Console.WriteLine($"NuGetPackageManager.DownloadPackageContentsAsync - {sw.Elapsed}");

            sw.Restart();
            var dllsBytes = packageContents.Where(x => Path.GetExtension(x.Key) == ".dll").Select(x => x.Value);
            this.CompilationService.AddReferences(dllsBytes);
            Console.WriteLine($"CompilationService.AddReferences - {sw.Elapsed}");

            sw.Restart();

            foreach (var (fileName, fileBytes) in packageContents)
            {
                this.UnmarshalledJsRuntime.InvokeUnmarshalled<string, string, byte[], object>(
                    "App.CodeExecution.storeNuGetPackageFile",
                    this.SessionId,
                    fileName,
                    fileBytes);
            }

            Console.WriteLine($"App.CodeExecution.storeNuGetPackageFile - {sw.Elapsed}");
        }

        private Task CloseInternalAsync()
        {
            this.Visible = false;
            return this.VisibleChanged.InvokeAsync(this.Visible);
        }
    }
}
