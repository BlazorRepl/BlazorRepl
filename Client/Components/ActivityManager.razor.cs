namespace BlazorRepl.Client.Components
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using BlazorRepl.Client.Models;
    using BlazorRepl.Core.PackageInstallation;
    using Microsoft.AspNetCore.Components;

    // TODO: extract components to caller component and have only the logic for activity show/hide here (rename to "bar")
    public partial class ActivityManager
    {
        [Parameter]
        public string SessionId { get; set; }

        [Parameter]
        public ICollection<Package> PackagesToRestore { get; set; }

        [Parameter]
        public StaticAssets StaticAssets { get; set; }

        [Parameter]
        public Func<string, Task> UpdateLoaderTextAsync { get; set; }

        [Parameter]
        public bool ShowLoader { get; set; }

        [Parameter]
        public EventCallback<bool> ShowLoaderChanged { get; set; }

        [Parameter]
        public EventCallback<bool> VisibleChanged { get; set; }

        private PackageManager PackageManagerComponent { get; set; }

        private bool PackageManagerVisible { get; set; }

        private bool StaticAssetManagerVisible { get; set; }

        private int PackagesCount => this.GetInstalledPackages()?.Count ?? 0;

        private bool ActivityVisible => this.PackageManagerVisible || this.StaticAssetManagerVisible;

        private string ActivityVisibleClass => this.ActivityVisible ? "activity-manager-expanded" : "activity-manager-collapsed";

        private string PackageManagerActivityActiveClass => this.PackageManagerVisible ? "active-activity-option" : string.Empty;

        private string StaticAssetManagerActivityActiveClass => this.StaticAssetManagerVisible ? "active-activity-option" : string.Empty;

        internal IReadOnlyCollection<Package> GetInstalledPackages() => this.PackageManagerComponent?.GetInstalledPackages();

        internal Task RestorePackagesAsync() => this.PackageManagerComponent?.RestorePackagesAsync() ?? Task.CompletedTask;

        private async Task TogglePackageManagerAsync(bool calledByOtherManagerToggle = false)
        {
            if (this.StaticAssetManagerVisible)
            {
                await this.ToggleStaticAssetManagerAsync(calledByOtherManagerToggle: true);
            }

            this.PackageManagerVisible = !this.PackageManagerVisible;

            if (!calledByOtherManagerToggle)
            {
                await this.VisibleChanged.InvokeAsync(this.ActivityVisible);
            }
        }

        private async Task ToggleStaticAssetManagerAsync(bool calledByOtherManagerToggle = false)
        {
            if (this.PackageManagerVisible)
            {
                await this.TogglePackageManagerAsync(calledByOtherManagerToggle: true);
            }

            this.StaticAssetManagerVisible = !this.StaticAssetManagerVisible;

            if (!calledByOtherManagerToggle)
            {
                await this.VisibleChanged.InvokeAsync(this.ActivityVisible);
            }
        }
    }
}
