namespace BlazorRepl.Client.Components
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using BlazorRepl.Client.Models;
    using BlazorRepl.Core;
    using BlazorRepl.Core.PackageInstallation;
    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    public partial class PackageManager : IDisposable
    {
        [Inject]
        public IJSUnmarshalledRuntime UnmarshalledJsRuntime { get; set; }

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
        public bool ShowLoader { get; set; }

        [Parameter]
        public EventCallback<bool> ShowLoaderChanged { get; set; }

        [Parameter]
        public Func<string, Task> UpdateLoaderTextFunc { get; set; }

        [CascadingParameter]
        private PageNotifications PageNotificationsComponent { get; set; }

        private string PackageSearchQuery { get; set; }

        private bool PackageSearchResultsFetched { get; set; }

        private string SelectedPackageName { get; set; }

        private string SelectedPackageVersion { get; set; }

        private IEnumerable<string> PackageSearchResults { get; set; } = Enumerable.Empty<string>();

        private IEnumerable<string> SelectedPackageVersions { get; set; } = Enumerable.Empty<string>();

        private IEnumerable<PackageLicenseInfo> PackagesToAcceptLicense { get; set; } = Enumerable.Empty<PackageLicenseInfo>();

        private bool LicensePopupVisible { get; set; }

        private string DisplayStyle => this.Visible ? string.Empty : "display:none;";

        public IReadOnlyCollection<Package> GetInstalledPackages() =>
            this.NuGetPackageManagementService.InstalledPackages
                .Concat(this.PackagesToRestore ?? Enumerable.Empty<Package>())
                .ToList();

        public async Task RestorePackagesAsync(bool handleLoading = false)
        {
            if (this.PackagesToRestore == null || !this.PackagesToRestore.Any())
            {
                return;
            }

            if (handleLoading)
            {
                await this.ToggleLoaderAsync(true);
            }

            try
            {
                var index = 1;
                foreach (var package in this.PackagesToRestore)
                {
                    if (this.UpdateLoaderTextFunc != null)
                    {
                        await this.UpdateLoaderTextFunc($"[{index}/{this.PackagesToRestore.Count}] Restoring package: {package.Name}");
                        index++;
                    }

                    await this.NuGetPackageManagementService.PreparePackageForDownloadAsync(package.Name, package.Version);

                    await this.InstallPackageAsync();
                }

                this.PackagesToRestore.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                this.NuGetPackageManagementService.CancelPackageInstallation();

                var errorMessage = ex is NotSupportedException
                    ? ex.Message
                    : "Error while restoring packages. Please try again later.";

                this.PageNotificationsComponent.AddNotification(NotificationType.Error, errorMessage);
            }

            if (handleLoading)
            {
                await this.ToggleLoaderAsync(false);
            }
        }

        public void Dispose() => this.NuGetPackageManagementService?.Dispose();

        [JSInvokable]
        public Task CloseAsync() => this.CloseInternalAsync();

        private async Task SearchPackagesAsync()
        {
            try
            {
                this.PackageSearchResults = await this.NuGetPackageManagementService.SearchPackagesAsync(this.PackageSearchQuery);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                this.PageNotificationsComponent.AddNotification(
                    NotificationType.Error,
                    content: "Error while searching packages. Please try again later.");

                return;
            }

            this.SelectedPackageName = null;
            this.PackageSearchResultsFetched = true;
        }

        private async Task SelectPackageAsync(string selectedPackage)
        {
            try
            {
                this.SelectedPackageVersions = await this.NuGetPackageManagementService.GetPackageVersionsAsync(selectedPackage);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                this.PageNotificationsComponent.AddNotification(
                    NotificationType.Error,
                    content: "Error while getting package versions. Please try again later.");

                return;
            }

            this.PackageSearchQuery = selectedPackage;
            this.SelectedPackageName = selectedPackage;
            this.SelectedPackageVersion = this.SelectedPackageVersions.FirstOrDefault();
        }

        private async Task PreparePackageToInstallAsync()
        {
            PreparePackageInstallationResult prepareResult;
            try
            {
                prepareResult = await this.NuGetPackageManagementService.PreparePackageForDownloadAsync(
                   this.SelectedPackageName,
                   this.SelectedPackageVersion);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                this.NuGetPackageManagementService.CancelPackageInstallation();

                var errorMessage = ex is NotSupportedException
                    ? ex.Message
                    : "Error while installing package. Please try again later.";

                this.PageNotificationsComponent.AddNotification(NotificationType.Error, errorMessage);

                return;
            }

            this.PackagesToAcceptLicense = prepareResult.PackagesToAcceptLicense ?? Enumerable.Empty<PackageLicenseInfo>();

            if (this.PackagesToAcceptLicense.Any())
            {
                this.LicensePopupVisible = true;
            }
            else
            {
                await this.AcceptPackageLicenseAsync();
            }
        }

        private void DeclinePackageLicense()
        {
            this.NuGetPackageManagementService.CancelPackageInstallation();

            this.LicensePopupVisible = false;
        }

        private async Task AcceptPackageLicenseAsync()
        {
            this.LicensePopupVisible = false;

            await this.ToggleLoaderAsync(true);

            await (this.UpdateLoaderTextFunc?.Invoke($"Installing package: {this.SelectedPackageName}") ?? Task.CompletedTask);

            try
            {
                await this.InstallPackageAsync();

                this.PageNotificationsComponent.AddNotification(
                    NotificationType.Info,
                    $"{this.SelectedPackageName} package is successfully installed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                this.NuGetPackageManagementService.CancelPackageInstallation();

                this.PageNotificationsComponent.AddNotification(
                    NotificationType.Error,
                    content: "Error while installing package. Please try again later.");

                return;
            }
            finally
            {
                await this.ToggleLoaderAsync(false);
            }

            this.PackageSearchQuery = null;
            this.SelectedPackageName = null;
            this.SelectedPackageVersion = null;
            this.PackageSearchResults = Enumerable.Empty<string>();
            this.SelectedPackageVersions = Enumerable.Empty<string>();
            this.PackageSearchResultsFetched = false;
        }

        private async Task InstallPackageAsync()
        {
            var sw = Stopwatch.StartNew();

            var packagesContents = await this.NuGetPackageManagementService.DownloadPackagesContentsAsync();
            Console.WriteLine($"NuGetPackageManager.DownloadPackagesContentsAsync - {sw.Elapsed}");

            sw.Restart();
            this.CompilationService.AddAssemblyReferences(packagesContents.DllFiles.Values);
            Console.WriteLine($"CompilationService.AddAssemblyReferences - {sw.Elapsed}");

            sw.Restart();
            var allPackageFiles = packagesContents.DllFiles.Concat(packagesContents.JavaScriptFiles).Concat(packagesContents.CssFiles);
            foreach (var (fileName, fileBytes) in allPackageFiles)
            {
                this.UnmarshalledJsRuntime.InvokeUnmarshalled<string, string, byte[], object>(
                    "App.CodeExecution.storePackageFile",
                    this.SessionId,
                    fileName,
                    fileBytes);
            }

            Console.WriteLine($"App.CodeExecution.storePackageFile - {sw.Elapsed}");
        }

        private Task CloseInternalAsync()
        {
            this.Visible = false;
            return this.VisibleChanged.InvokeAsync(this.Visible);
        }

        private Task ToggleLoaderAsync(bool value)
        {
            this.ShowLoader = value;
            return this.ShowLoaderChanged.InvokeAsync(this.ShowLoader);
        }
    }
}
