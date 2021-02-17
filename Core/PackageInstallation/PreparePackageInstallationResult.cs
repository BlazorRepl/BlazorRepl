namespace BlazorRepl.Core.PackageInstallation
{
    using System.Collections.Generic;
    using System.Linq;

    public class PreparePackageInstallationResult
    {
        public IEnumerable<PackageLicenseInfo> PackagesToAcceptLicense { get; set; } = Enumerable.Empty<PackageLicenseInfo>();
    }
}
