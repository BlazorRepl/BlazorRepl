﻿namespace BlazorRepl.Core
{
    public static class CoreConstants
    {
        public const string MainComponentFilePath = "__Main.razor";
        public const string MainComponentDefaultFileContent = @"<h1>Hello, Blazor REPL!</h1>

@code {

}
";

        public const string DefaultUserComponentsAssemblyBytes =
            @"TVqQAAMAAAAEAAAA//8AALgAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAAAA4fug4AtAnNIbgBTM0hVGhpcyBwcm9ncmFtIGNhbm5vdCBiZSBydW4gaW4gRE9TIG1vZGUuDQ0KJAAAAAAAAABQRQAATAEDACXVRfwAAAAAAAAAAOAAIiALATAAAAgAAAAGAAAAAAAA+icAAAAgAAAAQAAAAAAAEAAgAAAAAgAABAAAAAAAAAAEAAAAAAAAAACAAAAAAgAAAAAAAAMAYIUAABAAABAAAAAAEAAAEAAAAAAAABAAAAAAAAAAAAAAAKUnAABPAAAAAEAAALwDAAAAAAAAAAAAAAAAAAAAAAAAAGAAAAwAAAC4JgAAVAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIAAACAAAAAAAAAAAAAAACCAAAEgAAAAAAAAAAAAAAC50ZXh0AAAAAAgAAAAgAAAACAAAAAIAAAAAAAAAAAAAAAAAACAAAGAucnNyYwAAALwDAAAAQAAAAAQAAAAKAAAAAAAAAAAAAAAAAABAAABALnJlbG9jAAAMAAAAAGAAAAACAAAADgAAAAAAAAAAAAAAAAAAQAAAQgAAAAAAAAAAAAAAAAAAAADZJwAAAAAAAEgAAAACAAUAXCAAAFwGAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAYqBioeAigMAAAKKkJTSkIBAAEAAAAAAAwAAAB2NC4wLjMwMzE5AAAAAAUAbAAAAOQBAAAjfgAAUAIAAAwDAAAjU3RyaW5ncwAAAABcBQAABAAAACNVUwBgBQAAEAAAACNHVUlEAAAAcAUAAOwAAAAjQmxvYgAAAAAAAAACAAABRxUAAAkAAAAA+gEzABYAAAEAAAAQAAAAAwAAAAMAAAACAAAADAAAAAsAAAABAAAAAwAAAAAA3gEBAAAAAAAGAPYAcAIGAEgBcAIGAEEAXQIPAJACAAAGAHsAZgEGAC8BCgIGANcACgIGAJQACgIGALEACgIGABYBCgIGAFUACgIGANkC/AEKADYCqgEOAGwAnwIOADMAnwIOACQCgAEAAAAAAQAAAAAAAQABAIEBEAAcAr8CMQABAAEAAQAQAAMCvwI9AAEAAgBQIAAAAACWACkAJwABAFIgAAAAAMQACgAtAAIAVCAAAAAAhhhXAgYAAwAAAAEATwIAAAEATQIJAFcCAQARAFcCBgAZAFcCCgApAFcCEAAxAFcCEAA5AFcCEABBAFcCEABJAFcCEABRAFcCEABZAFcCEABxAFcCEAB5AFcCBgAuAAsAMwAuABMAPAAuABsAWwAuACMAZAAuACsAmgAuADMAuQAuADsAxgAuAEMA0wAuAEsAmgAuAFMAmgBjAFsA3gAEgAAAAQAAAAAAAAAAAAAAAAC/AgAABQAAAAAAAAAAAAAAFQAaAAAAAAAFAAAAAgAAAAAAAAAeAOACAAAAAAUAAAAAAAAAAAAAAB4AnwIAAAAAAAAAPE1vZHVsZT4AQnVpbGRSZW5kZXJUcmVlAFN5c3RlbS5SdW50aW1lAENvbmZpZ3VyZQBDb21wb25lbnRCYXNlAERlYnVnZ2FibGVBdHRyaWJ1dGUAQXNzZW1ibHlUaXRsZUF0dHJpYnV0ZQBSb3V0ZUF0dHJpYnV0ZQBUYXJnZXRGcmFtZXdvcmtBdHRyaWJ1dGUAQXNzZW1ibHlGaWxlVmVyc2lvbkF0dHJpYnV0ZQBBc3NlbWJseUluZm9ybWF0aW9uYWxWZXJzaW9uQXR0cmlidXRlAEFzc2VtYmx5Q29uZmlndXJhdGlvbkF0dHJpYnV0ZQBDb21waWxhdGlvblJlbGF4YXRpb25zQXR0cmlidXRlAEFzc2VtYmx5UHJvZHVjdEF0dHJpYnV0ZQBBc3NlbWJseUNvbXBhbnlBdHRyaWJ1dGUAUnVudGltZUNvbXBhdGliaWxpdHlBdHRyaWJ1dGUAU3lzdGVtLlJ1bnRpbWUuVmVyc2lvbmluZwBNaWNyb3NvZnQuQXNwTmV0Q29yZS5Db21wb25lbnRzLlJlbmRlcmluZwBNaWNyb3NvZnQuQXNwTmV0Q29yZS5Db21wb25lbnRzLldlYkFzc2VtYmx5Lkhvc3RpbmcAQmxhem9yUmVwbC5Vc2VyQ29tcG9uZW50cy5kbGwAU3lzdGVtAF9fTWFpbgBTeXN0ZW0uUmVmbGVjdGlvbgBTdGFydHVwAFJlbmRlclRyZWVCdWlsZGVyAFdlYkFzc2VtYmx5SG9zdEJ1aWxkZXIAX19idWlsZGVyAC5jdG9y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AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==";

        public const string DefaultRazorFileContentFormat = "<h1>{0}</h1>";

        public static readonly string DefaultCSharpFileContentFormat =
            @$"namespace {CompilationService.DefaultRootNamespace}
{{{{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class {{0}}
    {{{{
    }}}}
}}}}
";

        internal const string StartupClassFilePath = "Startup.cs";
        internal const string StartupClassDefaultContent = @"namespace BlazorRepl.UserComponents
{
    using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
    using Microsoft.Extensions.DependencyInjection;

    public static class Startup
    {
        public static void Configure(WebAssemblyHostBuilder builder)
        {
        }
    }
}
";
    }
}
