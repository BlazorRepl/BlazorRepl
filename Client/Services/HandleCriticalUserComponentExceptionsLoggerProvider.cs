namespace BlazorRepl.Client.Services
{
    using Microsoft.Extensions.Logging;
    using Microsoft.JSInterop;

    public class HandleCriticalUserComponentExceptionsLoggerProvider : ILoggerProvider
    {
        private readonly IJSInProcessRuntime jsRuntime;

        public HandleCriticalUserComponentExceptionsLoggerProvider(IJSInProcessRuntime jsRuntime)
        {
            this.jsRuntime = jsRuntime;
        }

        public ILogger CreateLogger(string categoryName) => new HandleCriticalUserComponentExceptionsLogger(this.jsRuntime);

        public void Dispose()
        {
        }
    }
}
