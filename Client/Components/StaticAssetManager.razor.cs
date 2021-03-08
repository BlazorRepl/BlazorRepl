﻿namespace BlazorRepl.Client.Components
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using BlazorRepl.Client.Models;
    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    public partial class StaticAssetManager
    {
        private const string JsFileExtension = ".js";

        private static readonly string[] SupportedStaticAssetFileExtensions = { JsFileExtension, ".css" };

        [Inject]
        public IJSRuntime JsRuntime { get; set; }

        [Parameter]
        public bool Visible { get; set; }

        [Parameter]
        public IList<StaticAsset> Scripts { get; set; } = new List<StaticAsset>();

        [Parameter]
        public IList<StaticAsset> Styles { get; set; } = new List<StaticAsset>();

        [Parameter]
        public EventCallback OnStaticAssetsUpdated { get; set; }

        [Parameter]
        public string SessionId { get; set; }

        [CascadingParameter]
        private Func<PageNotifications> GetPageNotificationsComponent { get; set; }

        private string StaticAssetUrl { get; set; }

        private string DisplayStyle => this.Visible ? string.Empty : "display:none;";

        protected override async Task OnParametersSetAsync()
        {
            var updateStaticAssets = (this.Scripts != null && this.Scripts.Any()) || (this.Styles != null && this.Styles.Any());
            if (updateStaticAssets)
            {
                // TODO: check why this function is called multiple times on render and when toggle
                await this.JsRuntime.InvokeVoidAsync(
                    "App.CodeExecution.updateStaticAssets",
                    this.SessionId,
                    this.Scripts,
                    this.Styles);
            }

            await base.OnParametersSetAsync();
        }

        private async Task ToggleAsync(StaticAsset asset, bool enabled)
        {
            asset.Enabled = enabled;

            await this.JsRuntime.InvokeVoidAsync(
                "App.CodeExecution.updateStaticAssets",
                this.SessionId,
                this.Scripts,
                this.Styles);
        }

        private async Task AddStaticAssetAsync()
        {
            if (string.IsNullOrWhiteSpace(this.StaticAssetUrl))
            {
                return;
            }

            if (!Uri.TryCreate(this.StaticAssetUrl, UriKind.Absolute, out var uri))
            {
                this.GetPageNotificationsComponent().AddNotification(NotificationType.Error, "Invalid static file URL.");
                return;
            }

            var fileExtension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
            if (!SupportedStaticAssetFileExtensions.Contains(fileExtension))
            {
                this.GetPageNotificationsComponent().AddNotification(
                    NotificationType.Error,
                    $"Static assets with extension '{fileExtension}' are not supported. Supported extensions: {string.Join(", ", SupportedStaticAssetFileExtensions)}");

                return;
            }

            if (fileExtension == JsFileExtension)
            {
                if (this.Scripts.Any(a => a.Url == uri.AbsoluteUri))
                {
                    this.GetPageNotificationsComponent().AddNotification(NotificationType.Error, "Static asset already added.");
                    return;
                }

                this.Scripts.Add(new StaticAsset
                {
                    Url = uri.AbsoluteUri,
                    Source = StaticAssetSource.Cdn,
                    Enabled = true,
                });
            }
            else
            {
                if (this.Styles.Any(a => a.Url == uri.AbsoluteUri))
                {
                    this.GetPageNotificationsComponent().AddNotification(NotificationType.Error, "Static asset already added.");
                    return;
                }

                this.Styles.Add(new StaticAsset
                {
                    Url = uri.AbsoluteUri,
                    Source = StaticAssetSource.Cdn,
                    Enabled = true,
                });
            }

            await this.JsRuntime.InvokeVoidAsync("App.CodeExecution.updateStaticAssets", this.SessionId, this.Scripts, this.Styles);

            await this.OnStaticAssetsUpdated.InvokeAsync();

            this.StaticAssetUrl = null;
        }
    }
}
