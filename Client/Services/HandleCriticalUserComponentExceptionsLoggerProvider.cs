namespace BlazorRepl.Client.Services
{
    using Microsoft.Extensions.Logging;
    using Microsoft.JSInterop;

    public class HandleCriticalUserComponentExceptionsLoggerProvider : ILoggerProvider
    {
        private readonly IJSUnmarshalledRuntime unmarshalledJsRuntime;

        public HandleCriticalUserComponentExceptionsLoggerProvider(IJSUnmarshalledRuntime unmarshalledJsRuntime)
        {
            this.unmarshalledJsRuntime = unmarshalledJsRuntime;
        }

        public ILogger CreateLogger(string categoryName) => new HandleCriticalUserComponentExceptionsLogger(this.unmarshalledJsRuntime);

        public void Dispose()
        {
        }
    }
}
