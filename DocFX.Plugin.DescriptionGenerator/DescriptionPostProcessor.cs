using System;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using HtmlAgilityPack;
using Microsoft.DocAsCode.Common;
using Microsoft.DocAsCode.Plugins;

namespace DocFX.Plugin.DescriptionGenerator
{
    [Export(nameof(DescriptionPostProcessor), typeof(IPostProcessor))]
    public class DescriptionPostProcessor : IPostProcessor
    {
        private int _savedFiles = 0;
        private const int FixedDescriptionLength = 135;
        private const string FullStopDelimiter = ". ";

        public ImmutableDictionary<string, object> PrepareMetadata(ImmutableDictionary<string, object> metadata) => metadata;

        public Manifest Process(Manifest manifest, string outputFolder)
        {
            string versionInfo = Assembly.GetExecutingAssembly()
                                     .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                     ?.InformationalVersion ??
                                 Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Logger.LogInfo($"Version: {versionInfo}");
            foreach (var manifestItem in manifest.Files.Where(x=>x.DocumentType == "Conceptual"))
            {
                foreach (var manifestItemOutputFile in manifestItem.OutputFiles)
                {
                    string sourcePath = Path.Combine(manifest.SourceBasePath, manifestItem.SourceRelativePath);
                    string outputPath = Path.Combine(outputFolder, manifestItemOutputFile.Value.RelativePath);
                    WriteDescriptionTag(sourcePath, outputPath);
                }
            }
            Logger.LogInfo($"Added description tags to {_savedFiles} conceptual articles.");
            return manifest;
        }

        private void WriteDescriptionTag(string sourcePath, string outputPath)
        {
            Logger.LogVerbose($"Processing metadata from {sourcePath} to {outputPath}...");
            var htmlDoc = new HtmlDocument();
            htmlDoc.Load(outputPath);
            var articleNode = htmlDoc.DocumentNode.SelectSingleNode("//article[contains(@class, 'content wrap')]/p");
            if (articleNode == null)
            {
                Logger.LogVerbose("ArticleNode containing a valid paragraph not found, returning...");
                return;
            }

            if (string.IsNullOrWhiteSpace(articleNode.InnerText))
            {
                Logger.LogVerbose("Article paragraph not found or empty, returning...");
                return;
            }

            var articlePunctuationPos = articleNode.InnerText.IndexOf(FullStopDelimiter, StringComparison.Ordinal);
            var articleDescription = articlePunctuationPos <= FixedDescriptionLength && articlePunctuationPos > 0
                ? articleNode.InnerText.Remove(articlePunctuationPos + FullStopDelimiter.Length).Trim()
                : Truncate(articleNode.InnerText, FixedDescriptionLength, "...");

            var headerNode = htmlDoc.DocumentNode.SelectSingleNode("//head");
            var metaDescriptionNode = htmlDoc.CreateElement("meta");
            metaDescriptionNode.SetAttributeValue("name", "description");
            metaDescriptionNode.SetAttributeValue("content", articleDescription);
            headerNode.AppendChild(metaDescriptionNode);
            htmlDoc.Save(outputPath);
            _savedFiles++;
        }

        /// <summary>
        ///     Truncates the specified string.
        /// </summary>
        /// <remarks>
        ///     This method is fetched from the Humanizer project, which is licensed under the MIT license. Copyright(c)
        ///     .NET Foundation and Contributors
        /// </remarks>
        /// <seealso href="https://github.com/Humanizr/Humanizer/" />
        /// <seealso href="https://github.com/Humanizr/Humanizer/blob/master/LICENSE" />
        public string Truncate(string value, int length, string truncationString)
        {
            if (value == null)
                return null;

            if (value.Length == 0)
                return value;

            if (truncationString == null || truncationString.Length > length)
                return value.Substring(0, length);

            return value.Length > length
                ? value.Substring(0, length - truncationString.Length) + truncationString
                : value;
        }
    }
}
