namespace BlazorRepl.Client.Shared
{
    using System;
    using BlazorRepl.Client.Components;

    public partial class MainLayout : IDisposable
    {
        private PageNotifications PageNotificationsComponent { get; set; }

        private Func<PageNotifications> GetPageNotificationsComponent { get; set; }

        public void Dispose() => this.PageNotificationsComponent?.Dispose();

        protected override void OnInitialized()
        {
            this.GetPageNotificationsComponent = () => this.PageNotificationsComponent;

            base.OnInitialized();
        }
    }
}
