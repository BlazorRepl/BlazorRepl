namespace BlazorRepl.Client.Shared
{
    using System;
    using BlazorRepl.Client.Components;

    public partial class MainLayout : IDisposable
    {
        private PageNotifications PageNotificationsComponent { get; set; }

        public void Dispose() => this.PageNotificationsComponent?.Dispose();
    }
}
