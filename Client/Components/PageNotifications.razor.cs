namespace BlazorRepl.Client.Components
{
    using System;
    using System.Collections.Generic;
    using System.Timers;
    using BlazorRepl.Client.Models;

    public partial class PageNotifications : IDisposable
    {
        private readonly IList<PageNotification> notifications = new List<PageNotification>();
        private readonly IList<Timer> autoCloseNotificationTimers = new List<Timer>();

        public void Dispose()
        {
            foreach (var timer in this.autoCloseNotificationTimers)
            {
                timer.Dispose();
            }
        }

        internal void AddNotification(NotificationType type, string content, string title = null, int autoCloseTimeoutSeconds = 7)
        {
            if (!string.IsNullOrWhiteSpace(content))
            {
                var notification = new PageNotification { Type = type, Title = title, Content = content };
                this.notifications.Add(notification);

                this.AddAutoCloseNotificationTimer(notification, autoCloseTimeoutSeconds);

                this.StateHasChanged();
            }
        }

        internal void Clear()
        {
            this.notifications.Clear();

            this.StateHasChanged();
        }

        private static string GetAlertClass(NotificationType type) =>
            type switch
            {
                NotificationType.Info => "alert-info",
                NotificationType.Warning => "alert-warning",
                NotificationType.Error => "alert-danger",
                _ => "alert-info",
            };

        private void CloseNotification(PageNotification notification, bool triggerStateHasChanged = false)
        {
            var removed = this.notifications.Remove(notification);

            if (removed && triggerStateHasChanged)
            {
                this.StateHasChanged();
            }
        }

        private void AddAutoCloseNotificationTimer(PageNotification notification, int autoCloseTimeoutSeconds)
        {
            var autoCloseNotificationTimer = new Timer
            {
                AutoReset = false,
                Enabled = true,
                Interval = autoCloseTimeoutSeconds * 1_000,
            };

            autoCloseNotificationTimer.Elapsed += (sender, _) =>
            {
                if (sender is not Timer timer)
                {
                    return;
                }

                this.CloseNotification(notification, triggerStateHasChanged: true);

                this.autoCloseNotificationTimers.Remove(timer);

                timer.Dispose();
            };

            this.autoCloseNotificationTimers.Add(autoCloseNotificationTimer);
        }
    }
}
