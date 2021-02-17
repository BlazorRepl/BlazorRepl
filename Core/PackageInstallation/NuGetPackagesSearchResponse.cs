namespace BlazorRepl.Core.PackageInstallation
{
    using System.Collections.Generic;

    internal class NuGetPackagesSearchResponse
    {
        public IEnumerable<string> Data { get; set; }
    }
}
