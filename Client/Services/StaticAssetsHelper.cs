namespace BlazorRepl.Client.Services
{
    using System;
    using System.Collections.Generic;
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

            if (staticAssets.Scripts != null)
            {
                // TODO: Handle duplicates
                foreach (var script in staticAssets.Scripts ?? Enumerable.Empty<StaticAsset>())
                {
                    if (!Uri.TryCreate(script.Url, UriKind.Absolute, out _))
                    {
                        return $"Invalid JS file URL: {script.Url}";
                    }
                }
            }

            if (staticAssets.Styles != null)
            {
                // TODO: Handle duplicates
                foreach (var style in staticAssets.Styles ?? Enumerable.Empty<StaticAsset>())
                {
                    if (!Uri.TryCreate(style.Url, UriKind.Absolute, out _))
                    {
                        return $"Invalid CSS file URL: {style.Url}";
                    }
                }
            }

            return null;
        }
    }
}
