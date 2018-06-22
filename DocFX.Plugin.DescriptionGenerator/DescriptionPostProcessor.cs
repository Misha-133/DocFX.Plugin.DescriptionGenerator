using System;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
                        WriteDescriptionTag(sourcePath, outputPath, ArticleType.Conceptual);
                    if (manifestItem.DocumentType == "ManagedReference")
                        WriteDescriptionTag(sourcePath, outputPath, ArticleType.Reference);
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
        private void WriteDescriptionTag(string sourcePath, string outputPath, ArticleType type)
        {
            Logger.LogVerbose($"Processing metadata from {sourcePath} to {outputPath}...");
            var htmlDoc = new HtmlDocument();
            htmlDoc.Load(outputPath);
            
            string descriptionText = string.Empty;
            switch (type)
            {
                case ArticleType.Conceptual:
                    var articleInnerText = htmlDoc.DocumentNode.SelectSingleNode("//article[@id='_content']/p")?.InnerText;
                    if (string.IsNullOrEmpty(articleInnerText)) return;

                    int articlePunctuationPos = articleInnerText.IndexOf(FullStopDelimiter, StringComparison.Ordinal);
                    descriptionText = articlePunctuationPos <= FixedDescriptionLength && articlePunctuationPos > 0
                        ? articleInnerText.Remove(articlePunctuationPos + FullStopDelimiter.Length).Trim()
                        : Truncate(articleInnerText, FixedDescriptionLength, "...");
                    break;
                case ArticleType.Reference:
                    var sb = new StringBuilder();

                    var memberName = htmlDoc.DocumentNode.SelectSingleNode("//article[@id='_content']/h1")?.InnerText;
                    if (!string.IsNullOrEmpty(memberName)) sb.Append(memberName.Trim());

                    var memberDescription = htmlDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'level0 summary')]/p")?.InnerText;
                    if (!string.IsNullOrEmpty(memberDescription)) sb.Append($" {char.ToLowerInvariant(memberDescription[0]) + memberDescription.Substring(1)}");

                    descriptionText = sb.ToString();
                    break;
            }

            if (string.IsNullOrEmpty(descriptionText)) return;
            AppendDescription(htmlDoc, descriptionText).Save(outputPath);
            _savedFiles++;
        }

        /// <summary>
        ///     Appends the description text into the meta tag of the document.
        /// </summary>
        /// <param name="htmlDoc">The document to be modified.</param>
        /// <param name="descriptionText">The text to append.</param>
        /// <returns>
        ///     The modified <see cref="HtmlDocument" />.
        /// </returns>
        private HtmlDocument AppendDescription(HtmlDocument htmlDoc, string descriptionText)
        {
            var headerNode = htmlDoc.DocumentNode.SelectSingleNode("//head");
            var metaDescriptionNode = htmlDoc.CreateElement("meta");
            metaDescriptionNode.SetAttributeValue("name", "description");
            metaDescriptionNode.SetAttributeValue("content", descriptionText);
            headerNode.AppendChild(metaDescriptionNode);
            return htmlDoc;
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
        private string Truncate(string value, int length, string truncationString)
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

    /// <summary>
    ///     Defines the type of article.
    /// </summary>
    internal enum ArticleType
    {
        Conceptual,
        Reference
    }
}