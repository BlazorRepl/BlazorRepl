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

    public partial class StaticAssetManager
    {
        [Parameter]
        public bool Visible { get; set; }

        [Parameter]
        public EventCallback<bool> VisibleChanged { get; set; }

        [Parameter]
        public ICollection<string> StaticAssets { get; set; } = new List<string>();

        [Parameter]
        public string SessionId { get; set; }

        [CascadingParameter]
        private PageNotifications PageNotificationsComponent { get; set; }

        // TODO: get type from url
        private string StaticAssetUrl { get; set; }

        private string DisplayStyle => this.Visible ? string.Empty : "display:none;";

        [JSInvokable]
        public Task CloseAsync() => this.CloseInternalAsync();

        private void AddStaticAsset()
        {
            if (string.IsNullOrWhiteSpace(this.StaticAssetUrl))
            {
                return;
            }

            this.StaticAssets.Add(this.StaticAssetUrl);
        }

        private Task CloseInternalAsync()
        {
            this.Visible = false;
            return this.VisibleChanged.InvokeAsync(this.Visible);
        }
    }
}
