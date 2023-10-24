using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Docfx.Common;
using Docfx.Plugins;
using System.Composition;

namespace Docfx.Plugin.DescriptionGenerator;

[Export(nameof(DescriptionPostProcessor), typeof(IPostProcessor))]
public class DescriptionPostProcessor : IPostProcessor
{
    private const int FixedDescriptionLength = 150;
    private const string FullStopDelimiter = ". ";
    private int _savedFiles;

    public ImmutableDictionary<string, object> PrepareMetadata(ImmutableDictionary<string, object> metadata)
        => metadata;

    public Manifest Process(Manifest manifest, string outputFolder)
    {
        var versionInfo = Assembly.GetExecutingAssembly()
                              .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                              ?.InformationalVersion ??
                          Assembly.GetExecutingAssembly().GetName().Version.ToString();
        Logger.LogInfo($"Version: {versionInfo}");
        var taskQueue = manifest.Files.Where(x => !string.IsNullOrEmpty(x.SourceRelativePath)).Select(manifestItem =>
        {
            return Task.Run(() =>
            {
                Logger.LogVerbose(
                    $"Document type for {manifestItem.SourceRelativePath} is {manifestItem.Type}.");
                foreach (var manifestItemOutputFile in manifestItem.Output.Where(x =>
                    !string.IsNullOrEmpty(x.Value.RelativePath)))
                {
                    var sourcePath = Path.Combine(manifest.SourceBasePath, manifestItem.SourceRelativePath);
                    var outputPath = Path.Combine(outputFolder, manifestItemOutputFile.Value.RelativePath);
                    if (manifestItem.Type == "Conceptual")
                        WriteMetadataTag(sourcePath, outputPath, ArticleType.Conceptual);
                    if (manifestItem.Type == "ManagedReference")
                        WriteMetadataTag(sourcePath, outputPath, ArticleType.Reference);
                }
            });
        }).ToArray();

        Task.WaitAll(taskQueue);

        Logger.LogInfo($"Added description tags to {_savedFiles} items.");
        return manifest;
    }

    /// <summary>
    ///     Injects the description tag according to the <paramref name="type" /> of article.
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
        var descriptionText = string.Empty;
        switch (type)
        {
            case ArticleType.Conceptual:
                var articleInnerText =
                    htmlDoc.DocumentNode.SelectSingleNode("//article /p")?.InnerText;
                if (string.IsNullOrEmpty(articleInnerText))
                    break;

                var articlePunctuationPos = articleInnerText.IndexOf(FullStopDelimiter, StringComparison.Ordinal);
                descriptionText = articlePunctuationPos <= FixedDescriptionLength && articlePunctuationPos > 0
                    ? articleInnerText.Remove(articlePunctuationPos + FullStopDelimiter.Length).Trim()
                    : articleInnerText.Truncate(FixedDescriptionLength, "...");
                break;
            case ArticleType.Reference:
                var memberDescription = htmlDoc.DocumentNode
                    .SelectSingleNode("//div[contains(@class, 'markdown summary')]/p")?.InnerText;
                if (!string.IsNullOrEmpty(memberDescription))
                    descriptionText = memberDescription;
                break;
        }

        var titleText = htmlDoc.DocumentNode.SelectSingleNode("//head/title")?.InnerText;

        if (!string.IsNullOrEmpty(descriptionText))
        {
            htmlDoc = AppendDescriptionMetadata(htmlDoc, descriptionText);
        }

        if (!string.IsNullOrEmpty(titleText))
            htmlDoc = AppendTitleMetadata(htmlDoc, titleText);
        AppendSiteNameMetadata(htmlDoc, "Discord.Net Docs");
        AppendThemeColorMetadata(htmlDoc, "#995EA7");
        AppendImageMetadata(htmlDoc, "https://raw.githubusercontent.com/Discord-Net/Discord.Net/dev/docs/marketing/logo/PackageLogo.png");
        htmlDoc.Save(outputPath);
        _savedFiles++;
    }

    /// <summary>
    ///     Appends the description text into the meta tag of the document.
    /// </summary>
    /// <param name="htmlDoc">The document to be modified.</param>
    /// <param name="value">The text to append.</param>
    /// <returns>
    ///     The modified <see cref="HtmlDocument" /> .
    /// </returns>
    private static HtmlDocument AppendDescriptionMetadata(HtmlDocument htmlDoc, string value)
    {
        var headerNode = htmlDoc.DocumentNode.SelectSingleNode("//head");
        var metaDescriptionNode = htmlDoc.CreateElement("meta");
        metaDescriptionNode.SetAttributeValue("property", "og:description");
        metaDescriptionNode.SetAttributeValue("content", value);
        headerNode.AppendChild(metaDescriptionNode);
        return htmlDoc;
    }

    private static HtmlDocument AppendTitleMetadata(HtmlDocument htmlDoc, string value)
    {
        var headerNode = htmlDoc.DocumentNode.SelectSingleNode("//head");
        var metaDescriptionNode = htmlDoc.CreateElement("meta");
        metaDescriptionNode.SetAttributeValue("property", "og:title");
        metaDescriptionNode.SetAttributeValue("content", value);
        headerNode.AppendChild(metaDescriptionNode);
        return htmlDoc;
    }

    private static HtmlDocument AppendSiteNameMetadata(HtmlDocument htmlDoc, string value)
    {
        var headerNode = htmlDoc.DocumentNode.SelectSingleNode("//head");
        var metaDescriptionNode = htmlDoc.CreateElement("meta");
        metaDescriptionNode.SetAttributeValue("property", "og:site_name");
        metaDescriptionNode.SetAttributeValue("content", value);
        headerNode.AppendChild(metaDescriptionNode);
        return htmlDoc;
    }

    private static HtmlDocument AppendImageMetadata(HtmlDocument htmlDoc, string value)
    {
        var headerNode = htmlDoc.DocumentNode.SelectSingleNode("//head");
        var metaDescriptionNode = htmlDoc.CreateElement("meta");
        metaDescriptionNode.SetAttributeValue("property", "og:image");
        metaDescriptionNode.SetAttributeValue("content", value);
        headerNode.AppendChild(metaDescriptionNode);
        return htmlDoc;
    }

    private static HtmlDocument AppendThemeColorMetadata(HtmlDocument htmlDoc, string value)
    {
        var headerNode = htmlDoc.DocumentNode.SelectSingleNode("//head");
        var metaDescriptionNode = htmlDoc.CreateElement("meta");
        metaDescriptionNode.SetAttributeValue("name", "theme-color");
        metaDescriptionNode.SetAttributeValue("content", value);
        headerNode.AppendChild(metaDescriptionNode);
        return htmlDoc;
    }
}