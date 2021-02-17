﻿namespace BlazorRepl.Client.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading.Tasks;
    using BlazorRepl.Client.Models;
    using BlazorRepl.Core;
    using BlazorRepl.Core.PackageInstallation;
    using Microsoft.Extensions.Options;

    public class SnippetsService
    {
        private const int SnippetIdLength = 18;

        private static readonly IDictionary<char, char> LetterToDigitIdMappings = new Dictionary<char, char>
        {
            ['a'] = '0',
            ['k'] = '0',
            ['u'] = '0',
            ['E'] = '0',
            ['O'] = '0',
            ['Y'] = '0',
            ['b'] = '1',
            ['l'] = '1',
            ['v'] = '1',
            ['F'] = '1',
            ['P'] = '1',
            ['c'] = '2',
            ['m'] = '2',
            ['w'] = '2',
            ['G'] = '2',
            ['Q'] = '2',
            ['d'] = '3',
            ['n'] = '3',
            ['x'] = '3',
            ['H'] = '3',
            ['R'] = '3',
            ['e'] = '4',
            ['o'] = '4',
            ['y'] = '4',
            ['I'] = '4',
            ['S'] = '4',
            ['f'] = '5',
            ['p'] = '5',
            ['z'] = '5',
            ['J'] = '5',
            ['T'] = '5',
            ['g'] = '6',
            ['q'] = '6',
            ['A'] = '6',
            ['K'] = '6',
            ['U'] = '6',
            ['h'] = '7',
            ['r'] = '7',
            ['B'] = '7',
            ['L'] = '7',
            ['V'] = '7',
            ['i'] = '8',
            ['s'] = '8',
            ['C'] = '8',
            ['M'] = '8',
            ['W'] = '8',
            ['j'] = '9',
            ['t'] = '9',
            ['D'] = '9',
            ['N'] = '9',
            ['X'] = '9',
            ['Z'] = '9',
        };

        private readonly HttpClient httpClient;
        private readonly SnippetsOptions snippetsOptions;

        public SnippetsService(HttpClient httpClient, IOptions<SnippetsOptions> snippetsOptions)
        {
            this.httpClient = httpClient;
            this.snippetsOptions = snippetsOptions.Value;
        }

        public async Task<string> SaveSnippetAsync(
            IEnumerable<CodeFile> codeFiles,
            IEnumerable<Package> installedPackages,
            StaticAssets staticAssets)
        {
            if (codeFiles == null)
            {
                throw new ArgumentNullException(nameof(codeFiles));
            }

            var codeFilesValidationError = CodeFilesHelper.ValidateCodeFilesForSnippetCreation(codeFiles);
            if (!string.IsNullOrWhiteSpace(codeFilesValidationError))
            {
                throw new InvalidOperationException(codeFilesValidationError);
            }

            var packagesValidationError = PackagesHelper.ValidatePackagesForSnippetCreation(installedPackages);
            if (!string.IsNullOrWhiteSpace(packagesValidationError))
            {
                throw new InvalidOperationException(packagesValidationError);
            }

            var staticAssetsValidationError = StaticAssetsHelper.ValidateStaticAssetsForSnippetCreation(staticAssets);
            if (!string.IsNullOrWhiteSpace(staticAssetsValidationError))
            {
                throw new InvalidOperationException(staticAssetsValidationError);
            }

            var requestData = new CreateSnippetRequest
            {
                Files = codeFiles,
                InstalledPackages = installedPackages,
                StaticAssets = staticAssets,
            };

            var response = await this.httpClient.PostAsJsonAsync(this.snippetsOptions.CreateUrl, requestData);
            response.EnsureSuccessStatusCode();

            var id = await response.Content.ReadAsStringAsync();
            return id;
        }

        public async Task<SnippetResponse> GetSnippetContentAsync(string snippetId)
        {
            if (string.IsNullOrWhiteSpace(snippetId) || snippetId.Length != SnippetIdLength)
            {
                throw new ArgumentException("Invalid snippet ID.", nameof(snippetId));
            }

            var yearFolder = DecodeDateIdPart(snippetId.Substring(0, 2));
            var monthFolder = DecodeDateIdPart(snippetId.Substring(2, 2));
            var dayAndHourFolder = DecodeDateIdPart(snippetId.Substring(4, 4));
            var id = snippetId[8..];

            var url = string.Format(this.snippetsOptions.ReadUrlFormat, yearFolder, monthFolder, dayAndHourFolder, id);
            var snippetResponse = await this.httpClient.GetAsync(url);
            snippetResponse.EnsureSuccessStatusCode();

            var snippetFiles = await ExtractSnippetFilesFromResponseAsync(snippetResponse);
            var installedPackages = ExtractPackagesFromResponse(snippetResponse);
            var staticAssets = ExtractStaticAssetsFromResponse(snippetResponse);

            var result = new SnippetResponse
            {
                Files = snippetFiles,
                InstalledPackages = installedPackages,
                StaticAssets = staticAssets,
            };
            return result;
        }

        private static async Task<IEnumerable<CodeFile>> ExtractSnippetFilesFromResponseAsync(HttpResponseMessage snippetResponse)
        {
            var result = new List<CodeFile>();

            if (snippetResponse.Headers.TryGetValues("x-ms-meta-zip", out _))
            {
                await using var zipFileStream = await snippetResponse.Content.ReadAsStreamAsync();
                using var zipArchive = new ZipArchive(zipFileStream, ZipArchiveMode.Read);
                foreach (var entry in zipArchive.Entries)
                {
                    using var streamReader = new StreamReader(entry.Open());

                    result.Add(new CodeFile { Path = entry.FullName, Content = await streamReader.ReadToEndAsync() });
                }
            }
            else
            {
                var fileContent = await snippetResponse.Content.ReadAsStringAsync();

                result.Add(new CodeFile { Path = CoreConstants.MainComponentFilePath, Content = fileContent });
            }

            return result;
        }

        private static IEnumerable<Package> ExtractPackagesFromResponse(HttpResponseMessage snippetResponse)
        {
            if (snippetResponse.Headers.TryGetValues("x-ms-meta-packages", out var packagesHeaderValue) &&
                packagesHeaderValue.Any())
            {
                var packages = JsonSerializer
                    .Deserialize<IDictionary<string, string>>(packagesHeaderValue.First())
                    .Select(x => new Package { Name = x.Key, Version = x.Value })
                    .ToList();

                return packages;
            }

            return Enumerable.Empty<Package>();
        }

        private static StaticAssets ExtractStaticAssetsFromResponse(HttpResponseMessage snippetResponse)
        {
            if (snippetResponse.Headers.TryGetValues("x-ms-meta-staticassets", out var staticAssetsHeaderValue) &&
                staticAssetsHeaderValue.Any())
            {
                var staticAssets = JsonSerializer.Deserialize<StaticAssets>(staticAssetsHeaderValue.First());
                return staticAssets;
            }

            return null;
        }

        private static string DecodeDateIdPart(string encodedPart)
        {
            var decodedPart = string.Empty;

            foreach (var letter in encodedPart)
            {
                if (!LetterToDigitIdMappings.TryGetValue(letter, out var digit))
                {
                    throw new InvalidDataException("Invalid snippet ID");
                }

                decodedPart += digit;
            }

            return decodedPart;
        }
    }
}
