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
                foreach (var script in staticAssets.Scripts ?? new Dictionary<string, bool>())
                {
                    if (!Uri.TryCreate(script.Key, UriKind.Absolute, out _))
                    {
                        return $"Invalid JS file URL: {script.Key}";
                    }
                }
            }

            if (staticAssets.Styles != null)
            {
                foreach (var style in staticAssets.Styles)
                {
                    if (!Uri.TryCreate(style.Key, UriKind.Absolute, out _))
                    {
                        return $"Invalid CSS file URL: {style.Key}";
                    }
                }
            }

            return null;
        }
    }
}
