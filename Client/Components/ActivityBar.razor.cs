namespace BlazorRepl.Client.Components
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using BlazorRepl.Core.PackageInstallation;
    using Microsoft.AspNetCore.Components;

    // TODO: Rename to ActivityManager
    public partial class ActivityBar
    {
        [Parameter]
        public bool PackageManagerVisible { get; set; }

        [Parameter]
        public EventCallback<bool> PackageManagerVisibleChanged { get; set; }

        [Parameter]
        public string SessionId { get; set; }

        [Parameter]
        public ICollection<Package> PackagesToRestore { get; set; }

        [Parameter]
        public Func<string, Task> UpdateLoaderTextAsync { get; set; }

        [Parameter]
        public bool Loading { get; set; }

        [Parameter]
        public EventCallback<bool> LoadingChanged { get; set; }

        private bool ActivityVisible => this.PackageManagerVisible; // || StaticAssetManagerVisible...

        private string ActivityVisibleClass => this.ActivityVisible ? "activity-manager-expanded" : "activity-manager-collapsed";

        private PackageManager PackageManagerComponent { get; set; }

        private Task TogglePackageManagerAsync()
        {
            this.PackageManagerVisible = !this.PackageManagerVisible;
            return this.PackageManagerVisibleChanged.InvokeAsync(this.PackageManagerVisible);
        }
    }
}
