namespace BlazorRepl.Client.Components
{
    using System.Threading.Tasks;
    using BlazorRepl.Client.Models;
    using Microsoft.AspNetCore.Components;

    public partial class ActivityBar
    {
        [Parameter]
        public EventCallback<ActivityToggleEventArgs> OnActivityToggle { get; set; }

        [Parameter]
        public int PackagesCount { get; set; }

        private bool PackageManagerVisible { get; set; }

        private bool StaticAssetManagerVisible { get; set; }

        private string PackageManagerActivityActiveClass => this.PackageManagerVisible ? "active-activity-option" : string.Empty;

        private string StaticAssetManagerActivityActiveClass => this.StaticAssetManagerVisible ? "active-activity-option" : string.Empty;

        private async Task TogglePackageManagerActivityAsync(bool calledByOtherActivityToggle = false)
        {
            if (this.StaticAssetManagerVisible)
            {
                await this.ToggleStaticAssetManagerActivityAsync(calledByOtherActivityToggle: true);
            }

            this.PackageManagerVisible = !this.PackageManagerVisible;

            if (!calledByOtherActivityToggle)
            {
                await this.OnActivityToggle.InvokeAsync(
                    new ActivityToggleEventArgs { Activity = nameof(PackageManager), Visible = this.PackageManagerVisible });
            }
        }

        private async Task ToggleStaticAssetManagerActivityAsync(bool calledByOtherActivityToggle = false)
        {
            if (this.PackageManagerVisible)
            {
                await this.TogglePackageManagerActivityAsync(calledByOtherActivityToggle: true);
            }

            this.StaticAssetManagerVisible = !this.StaticAssetManagerVisible;

            if (!calledByOtherActivityToggle)
            {
                await this.OnActivityToggle.InvokeAsync(
                    new ActivityToggleEventArgs { Activity = nameof(StaticAssetManager), Visible = this.StaticAssetManagerVisible });
            }
        }
    }
}
