namespace BlazorRepl.Core.PackageInstallation
{
    using System.Collections.Generic;

    public class PackagesContentsResult
    {
        public IDictionary<string, byte[]> DllFiles { get; set; } = new Dictionary<string, byte[]>();

        public IDictionary<string, byte[]> JavaScriptFiles { get; set; } = new Dictionary<string, byte[]>();

        public IDictionary<string, byte[]> CssFiles { get; set; } = new Dictionary<string, byte[]>();
    }
}
