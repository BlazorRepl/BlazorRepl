namespace BlazorRepl.Client.Components
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using BlazorRepl.Client.Models;
    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    public partial class StaticAssetManager
    {
        private static readonly string[] SupportedStaticAssetFileExtensions = { ".js", ".css" };

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

            if (!Uri.TryCreate(this.StaticAssetUrl, UriKind.Absolute, out var uri))
            {
                this.PageNotificationsComponent.AddNotification(NotificationType.Error, "Invalid static file URL.");
                return;
            }

            if (this.StaticAssets.Any(a => string.Equals(a, uri.AbsoluteUri, StringComparison.OrdinalIgnoreCase)))
            {
                this.PageNotificationsComponent.AddNotification(NotificationType.Error, "Static asset already added.");
                return;
            }

            var fileExtension = Path.GetExtension(uri.AbsoluteUri);
            if (!SupportedStaticAssetFileExtensions.Contains(fileExtension))
            {
                this.PageNotificationsComponent.AddNotification(
                    NotificationType.Error,
                    $"Static assets with extension '{fileExtension}' are not supported. Supported extensions: {string.Join(", ", SupportedStaticAssetFileExtensions)}");

                return;
            }

            this.StaticAssets.Add(uri.AbsoluteUri);
            this.StaticAssetUrl = null;
        }

        private Task CloseInternalAsync()
        {
            this.Visible = false;
            return this.VisibleChanged.InvokeAsync(this.Visible);
        }
    }
}
