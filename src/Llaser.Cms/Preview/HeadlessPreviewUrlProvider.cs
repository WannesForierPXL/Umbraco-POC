using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;

namespace Llaser.Cms.Preview;

/// <summary>
/// Headless preview: "Save and preview" in de backoffice opent de frontend (hier de demopagina van de stub)
/// in plaats van een Umbraco-template, die we bewust niet hebben. De frontend haalt het concept op via de
/// Delivery API met de header "Preview: true".
/// Gewone (gepubliceerde) URL's laat deze provider over aan de standaardproviders.
/// </summary>
public sealed class HeadlessPreviewUrlProvider(IConfiguration configuration) : IUrlProvider
{
    public string Alias => "llaserHeadlessPreview";

    public UrlInfo? GetUrl(IPublishedContent content, UrlMode mode, string? culture, Uri current) => null;

    public IEnumerable<UrlInfo> GetOtherUrls(int id, Uri current) => [];

    public Task<UrlInfo?> GetPreviewUrlAsync(IContent content, string? culture, string? segment)
    {
        var frontend = configuration["Llaser:Preview:Url"];
        if (string.IsNullOrWhiteSpace(frontend)) return Task.FromResult<UrlInfo?>(null);

        var taal = culture?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true ? "en" : "nl";
        var query = $"preview=1&taal={taal}";
        if (content.ContentType.Alias == "guideStap") query += $"&stap={content.Key}";

        return Task.FromResult<UrlInfo?>(UrlInfo.AsUrl($"{frontend.TrimEnd('/')}/?{query}", Alias, culture, isExternal: true));
    }
}

public sealed class PreviewComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) => builder.UrlProviders().Insert<HeadlessPreviewUrlProvider>();
}
