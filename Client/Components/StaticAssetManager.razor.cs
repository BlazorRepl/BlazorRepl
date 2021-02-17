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
        private const string JsFileExtension = ".js";

        private static readonly string[] SupportedStaticAssetFileExtensions = { JsFileExtension, ".css" };

        [Inject]
        public IJSRuntime JsRuntime { get; set; }

        [Parameter]
        public bool Visible { get; set; }

        [Parameter]
        public EventCallback<bool> VisibleChanged { get; set; }

        [Parameter]
        public ISet<string> Scripts { get; set; } = new HashSet<string>();

        [Parameter]
        public ISet<string> Styles { get; set; } = new HashSet<string>();

        [Parameter]
        public string SessionId { get; set; }

        [CascadingParameter]
        private PageNotifications PageNotificationsComponent { get; set; }

        private IDictionary<string, string> ScriptUrlToFileNameMappings { get; set; } = new Dictionary<string, string>();

        private IDictionary<string, string> StyleUrlToFileNameMappings { get; set; } = new Dictionary<string, string>();

        private string StaticAssetUrl { get; set; }

        private string DisplayStyle => this.Visible ? string.Empty : "display:none;";

        public override async Task SetParametersAsync(ParameterView parameters)
        {
            await base.SetParametersAsync(parameters);

            var updateStaticAssets = false;
            if (this.Scripts != null && this.Scripts.Any())
            {
                this.ScriptUrlToFileNameMappings = this.Scripts.ToDictionary(a => a, Path.GetFileName);
                updateStaticAssets = true;
            }

            if (this.Styles != null && this.Styles.Any())
            {
                this.StyleUrlToFileNameMappings = this.Styles.ToDictionary(a => a, Path.GetFileName);
                updateStaticAssets = true;
            }

            if (updateStaticAssets)
            {
                await this.JsRuntime.InvokeVoidAsync(
                    "App.CodeExecution.updateStaticAssets",
                    this.SessionId,
                    this.Scripts,
                    this.Styles);
            }
        }

        private async Task AddStaticAssetAsync()
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

            var fileExtension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
            if (!SupportedStaticAssetFileExtensions.Contains(fileExtension))
            {
                this.PageNotificationsComponent.AddNotification(
                    NotificationType.Error,
                    $"Static assets with extension '{fileExtension}' are not supported. Supported extensions: {string.Join(", ", SupportedStaticAssetFileExtensions)}");

                return;
            }

            if (fileExtension == JsFileExtension)
            {
                if (this.Scripts.Contains(uri.AbsoluteUri))
                {
                    this.PageNotificationsComponent.AddNotification(NotificationType.Error, "Static asset already added.");
                    return;
                }

                this.Scripts.Add(uri.AbsoluteUri);
                this.ScriptUrlToFileNameMappings.Add(uri.AbsoluteUri, Path.GetFileName(uri.AbsolutePath));
            }
            else
            {
                if (this.Styles.Contains(uri.AbsoluteUri))
                {
                    this.PageNotificationsComponent.AddNotification(NotificationType.Error, "Static asset already added.");
                    return;
                }

                this.Styles.Add(uri.AbsoluteUri);
                this.StyleUrlToFileNameMappings.Add(uri.AbsoluteUri, Path.GetFileName(uri.AbsolutePath));
            }

            await this.JsRuntime.InvokeVoidAsync("App.CodeExecution.updateStaticAssets", this.SessionId, this.Scripts, this.Styles);

            this.StaticAssetUrl = null;
        }
    }
}
