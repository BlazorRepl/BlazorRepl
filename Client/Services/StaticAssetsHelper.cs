namespace BlazorRepl.Client.Services
{
    using System;
    using System.Collections.Generic;
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
                var uniqueScripts = new HashSet<string>();
                foreach (var script in staticAssets.Scripts)
                {
                    var isValidUrl = script.Source == StaticAssetSource.Cdn
                        ? Uri.TryCreate(script.Url, UriKind.Absolute, out _)
                        : Uri.TryCreate(script.Url, UriKind.Relative, out _);

                    if (!isValidUrl)
                    {
                        return $"Invalid JS file URL: {script.Url}";
                    }

                    if (uniqueScripts.Contains(script.Url))
                    {
                        return $"Script '{script.Url}' is duplicated.";
                    }

                    uniqueScripts.Add(script.Url);
                }
            }

            if (staticAssets.Styles != null)
            {
                var uniqueStyles = new HashSet<string>();
                foreach (var style in staticAssets.Styles)
                {
                    var isValidUrl = style.Source == StaticAssetSource.Cdn
                        ? Uri.TryCreate(style.Url, UriKind.Absolute, out _)
                        : Uri.TryCreate(style.Url, UriKind.Relative, out _);

                    if (!isValidUrl)
                    {
                        return $"Invalid CSS file URL: {style.Url}";
                    }

                    if (uniqueStyles.Contains(style.Url))
                    {
                        return $"Style '{style.Url}' is duplicated.";
                    }

                    uniqueStyles.Add(style.Url);
                }
            }

            return null;
        }
    }
}
