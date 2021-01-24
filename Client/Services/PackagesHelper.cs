namespace BlazorRepl.Client.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using BlazorRepl.Core.PackageInstallation;

    public static class PackagesHelper
    {
        public static string ValidatePackagesForSnippetCreation(IEnumerable<Package> packages)
        {
            if (packages == null || !packages.Any())
            {
                return null;
            }

            var uniquePackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var index = 0;
            foreach (var package in packages)
            {
                if (package == null)
                {
                    return $"Package #{index} - no package.";
                }

                if (string.IsNullOrWhiteSpace(package.Name) || string.IsNullOrWhiteSpace(package.Version))
                {
                    return $"Package #{index} - no name or version.";
                }

                if (package.Name.Contains(' ') || package.Version.Contains(' '))
                {
                    return $"Package '{package.Name}' v{package.Version} - name or version contains white space.";
                }

                const int MaxPackageNameLength = 128;
                if (package.Name.Length > MaxPackageNameLength)
                {
                    return $"Package '{package.Name}' - name length > {MaxPackageNameLength}.";
                }

                const int MaxPackageVersionLength = 64;
                if (package.Version.Length > MaxPackageVersionLength)
                {
                    return $"Package '{package.Name}' v{package.Version} - version length > {MaxPackageVersionLength}.";
                }

                if (uniquePackages.Contains(package.Name))
                {
                    return $"Package '{package.Name}' is duplicated.";
                }

                uniquePackages.Add(package.Name);
                index++;
            }

            return null;
        }
    }
}
