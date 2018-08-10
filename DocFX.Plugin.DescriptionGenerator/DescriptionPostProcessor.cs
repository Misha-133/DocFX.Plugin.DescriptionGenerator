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
        private const int FixedDescriptionLength = 150;
        private const string FullStopDelimiter = ". ";
        private int _savedFiles;

        public ImmutableDictionary<string, object> PrepareMetadata(ImmutableDictionary<string, object> metadata) =>
            metadata;

        public Manifest Process(Manifest manifest, string outputFolder)
        {
            string versionInfo = Assembly.GetExecutingAssembly()
                                     .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                     ?.InformationalVersion ??
                                 Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Logger.LogInfo($"Version: {versionInfo}");
            foreach (var manifestItem in manifest.Files.Where(x => !string.IsNullOrEmpty(x.SourceRelativePath)))
            {
                Logger.LogVerbose($"Document type for {manifestItem.SourceRelativePath} is {manifestItem.DocumentType}.");
                foreach (var manifestItemOutputFile in manifestItem.OutputFiles.Where(x =>
                    !string.IsNullOrEmpty(x.Value.RelativePath)))
                {
                    string sourcePath = Path.Combine(manifest.SourceBasePath, manifestItem.SourceRelativePath);
                    string outputPath = Path.Combine(outputFolder, manifestItemOutputFile.Value.RelativePath);
                    if (manifestItem.DocumentType == "Conceptual")
                        WriteMetadataTag(sourcePath, outputPath, ArticleType.Conceptual);
                    if (manifestItem.DocumentType == "ManagedReference")
                        WriteMetadataTag(sourcePath, outputPath, ArticleType.Reference);
                }
            }

            Logger.LogInfo($"Added description tags to {_savedFiles} items.");
            return manifest;
        }

        /// <summary>
        ///     Injects the description tag according to the type of article.
        /// </summary>
        /// <param name="sourcePath">The original article path.</param>
        /// <param name="outputPath">The output path.</param>
        /// <param name="type">The type of document.</param>
        private void WriteMetadataTag(string sourcePath, string outputPath, ArticleType type)
        {
            Logger.LogVerbose($"Processing metadata from {sourcePath} to {outputPath}...");
            var htmlDoc = new HtmlDocument();
            htmlDoc.Load(outputPath);
            
            // Write description
            string descriptionText = string.Empty;
            switch (type)
            {
                case ArticleType.Conceptual:
                    var articleInnerText = htmlDoc.DocumentNode.SelectSingleNode("//article[@id='_content']/p")?.InnerText;
                    if (string.IsNullOrEmpty(articleInnerText)) return;

                    int articlePunctuationPos = articleInnerText.IndexOf(FullStopDelimiter, StringComparison.Ordinal);
                    descriptionText = articlePunctuationPos <= FixedDescriptionLength && articlePunctuationPos > 0
                        ? articleInnerText.Remove(articlePunctuationPos + FullStopDelimiter.Length).Trim()
                        : articleInnerText.Truncate(FixedDescriptionLength, "...");
                    break;
                case ArticleType.Reference:
                    var memberDescription = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'level0 summary')]/p")?.InnerText;
                    if (!string.IsNullOrEmpty(memberDescription)) descriptionText = memberDescription;
                    break;
            }

            if (!string.IsNullOrEmpty(descriptionText))
            {
                AppendMetadata(htmlDoc, descriptionText).Save(outputPath);
                _savedFiles++;
            }
        }

        /// <summary>
        ///     Appends the description text into the meta tag of the document.
        /// </summary>
        /// <param name="htmlDoc">The document to be modified.</param>
        /// <param name="type">The type of metadata to inject.</param>
        /// <param name="value">The text to append.</param>
        /// <returns>
        ///     The modified <see cref="HtmlDocument"/>.
        /// </returns>
        private static HtmlDocument AppendMetadata(HtmlDocument htmlDoc, string value)
        {
            var headerNode = htmlDoc.DocumentNode.SelectSingleNode("//head");
            var metaDescriptionNode = htmlDoc.CreateElement("meta");
            metaDescriptionNode.SetAttributeValue("name", "description");
            metaDescriptionNode.SetAttributeValue("content", value);
            headerNode.AppendChild(metaDescriptionNode);
            return htmlDoc;
        }
    }
}