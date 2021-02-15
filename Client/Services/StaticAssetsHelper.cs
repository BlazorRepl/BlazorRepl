namespace BlazorRepl.Client.Services
{
    using System;
    using BlazorRepl.Client.Models;

    public static class StaticAssetsHelper
    {
        public static string ValidateStaticAssetsForSnippetCreation(StaticAssets staticAssets)
        {
            if (staticAssets == null)
            {
                return null;
            }

            if (staticAssets.Scripts != null)
            {
                foreach (var url in staticAssets.Scripts)
                {
                    if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                    {
                        return $"Invalid JS file URL: {url}";
                    }
                }
            }

            if (staticAssets.Styles != null)
            {
                foreach (var url in staticAssets.Styles)
                {
                    if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                    {
                        return $"Invalid CSS file URL: {url}";
                    }
                }
            }

            return null;
        }
    }
}
