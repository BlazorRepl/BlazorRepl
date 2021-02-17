namespace BlazorRepl.Client.Services
{
    using System;
    using System.Linq;
    using BlazorRepl.Client.Models;

    public static class StaticAssetsHelper
    {
        public static string ValidateStaticAssetsForSnippetCreation(StaticAssets staticAssets)
        {
            if (staticAssets == null)
            {
                return null;
            }

            foreach (var url in staticAssets.Scripts ?? Enumerable.Empty<string>())
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                {
                    return $"Invalid JS file URL: {url}";
                }
            }

            foreach (var url in staticAssets.Styles ?? Enumerable.Empty<string>())
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                {
                    return $"Invalid CSS file URL: {url}";
                }
            }

            return null;
        }
    }
}
