namespace BlazorRepl.Core.PackageInstallation
{
    using System.Collections.Generic;

    internal class NuGetPackageVersionsResponse
    {
        public IEnumerable<string> Data { get; set; }
    }
}
