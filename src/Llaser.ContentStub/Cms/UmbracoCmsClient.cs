using System.Net;
using System.Text.Json;

namespace Llaser.ContentStub.Cms;

/// <summary>
/// ICmsClient op de Umbraco Delivery API (v2). Enkel deze klasse kent Umbraco: aliassen, cultures,
/// het JSON-formaat van blokken en media, en de API-key.
/// </summary>
public sealed class UmbracoCmsClient(HttpClient http) : ICmsClient
{
    // Umbraco-cultures per taal van de stub
    private static string Culture(Taal taal) => taal == Taal.En ? "en-US" : "nl-BE";

    public async Task<CmsGuide?> GetGuideAsync(Taal taal, CancellationToken ct)
    {
        var items = await QueryAsync("filter=contentType:guide&take=1", taal, ct);
        return items.Select(i => new CmsGuide(Id(i), Naam(i), Tekst(i, "intro"), taal)).FirstOrDefault();
    }

    public async Task<IReadOnlyList<CmsHoofdstuk>> GetHoofdstukkenAsync(string guideId, Taal taal, CancellationToken ct) =>
        (await QueryAsync($"fetch=children:{guideId}&sort=sortOrder:asc&take=100", taal, ct))
            .Where(i => Type(i) == "hoofdstuk")
            .Select(i => MapHoofdstuk(i, taal))
            .ToList();

    public async Task<CmsHoofdstuk?> GetHoofdstukAsync(string id, Taal taal, CancellationToken ct)
    {
        var item = await ItemAsync(id, taal, ct);
        return item is { } i && Type(i) == "hoofdstuk" ? MapHoofdstuk(i, taal) : null;
    }

    public async Task<IReadOnlyList<CmsStap>> GetStappenAsync(string hoofdstukId, Taal taal, CancellationToken ct) =>
        (await QueryAsync($"fetch=children:{hoofdstukId}&sort=sortOrder:asc&take=100&fields=properties[samenvatting,toegangsniveau]", taal, ct))
            .Where(i => Type(i) == "guideStap")
            .Select(i => MapStap(i, hoofdstukId, taal, metBlokken: false))
            .ToList();

    public async Task<CmsStap?> GetStapAsync(string id, Taal taal, CancellationToken ct)
    {
        var item = await ItemAsync(id, taal, ct);
        if (item is not { } i || Type(i) != "guideStap") return null;

        // Het item zelf bevat geen parent-id; die halen we uit de voorouders (tweede request).
        var voorouders = await QueryAsync($"fetch=ancestors:{id}&fields=properties[toegangsniveau]", taal, ct);
        var hoofdstukId = voorouders.Where(a => Type(a) == "hoofdstuk").Select(Id).FirstOrDefault();
        return hoofdstukId is null ? null : MapStap(i, hoofdstukId, taal, metBlokken: true);
    }

    public async Task<IReadOnlyList<CmsResource>> GetResourcesAsync(Taal taal, CancellationToken ct) =>
        (await QueryAsync("filter=contentType:resource&sort=sortOrder:asc&take=100", taal, ct))
            .Select(i => MapResource(i, taal))
            .ToList();

    public async Task<CmsResource?> GetResourceAsync(string id, Taal taal, CancellationToken ct)
    {
        var item = await ItemAsync(id, taal, ct);
        return item is { } i && Type(i) == "resource" ? MapResource(i, taal) : null;
    }

    public async Task<CmsBestand?> GetMediaAsync(string mediaId, CancellationToken ct)
    {
        using var metaResponse = await http.GetAsync($"media/item/{mediaId}", ct);
        if (metaResponse.StatusCode == HttpStatusCode.NotFound) return null;
        metaResponse.EnsureSuccessStatusCode();
        using var meta = JsonDocument.Parse(await metaResponse.Content.ReadAsStringAsync(ct));
        var url = meta.RootElement.GetProperty("url").GetString()!;
        var naam = Path.GetFileName(url);

        // Het bestand zelf serveert Umbraco publiek onder /media/...; de stub haalt het op en geeft het enkel door na de toegangscontrole.
        var bestand = await http.GetAsync(new Uri(http.BaseAddress!, url), HttpCompletionOption.ResponseHeadersRead, ct);
        bestand.EnsureSuccessStatusCode();
        var contentType = bestand.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return new CmsBestand(await bestand.Content.ReadAsStreamAsync(ct), contentType, naam);
    }

    // ---------- Delivery API ----------

    private async Task<JsonElement?> ItemAsync(string id, Taal taal, CancellationToken ct)
    {
        using var request = Request($"content/item/{id}", taal);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null; // ook: niet gepubliceerd in deze taal
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.Clone();
    }

    private async Task<IReadOnlyList<JsonElement>> QueryAsync(string query, Taal taal, CancellationToken ct)
    {
        using var request = Request($"content?{query}", taal);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("items").EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private static HttpRequestMessage Request(string path, Taal taal)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Accept-Language", Culture(taal));
        return request;
    }

    // ---------- mapping ----------

    private static CmsHoofdstuk MapHoofdstuk(JsonElement i, Taal taal) =>
        new(Id(i), Naam(i), Tekst(i, "beschrijving"), Niveau(i), taal);

    private static CmsStap MapStap(JsonElement i, string hoofdstukId, Taal taal, bool metBlokken) =>
        new(Id(i), hoofdstukId, Naam(i), Tekst(i, "samenvatting") ?? "", Niveau(i), metBlokken ? Blokken(i) : null, taal);

    private static CmsResource MapResource(JsonElement i, Taal taal) =>
        new(Id(i), Naam(i), Tekst(i, "beschrijving"), Tekst(i, "soort") ?? "link", EersteMediaId(Prop(i, "bestand")), Tekst(i, "url"), Niveau(i), taal);

    private static IReadOnlyList<CmsBlok> Blokken(JsonElement i)
    {
        if (Prop(i, "blokken") is not { ValueKind: JsonValueKind.Object } blokken) return [];
        var result = new List<CmsBlok>();
        foreach (var item in blokken.GetProperty("items").EnumerateArray())
        {
            var content = item.GetProperty("content");
            var p = content.GetProperty("properties");
            CmsBlok? blok = Type(content) switch
            {
                "blokTekst" => new CmsTekstBlok(p.TryGetProperty("tekst", out var t) && t.ValueKind == JsonValueKind.Object
                    ? t.GetProperty("markup").GetString() ?? "" : ""),
                "blokAfbeelding" when EersteMediaId(p.TryGetProperty("afbeelding", out var a) ? a : null) is { } mediaId =>
                    new CmsAfbeeldingBlok(mediaId, Str(p, "alt") ?? "", Str(p, "bijschrift")),
                "blokCallout" => new CmsCalloutBlok(Str(p, "soort") ?? "info", Str(p, "tekst") ?? ""),
                _ => null,
            };
            if (blok is not null) result.Add(blok);
        }
        return result;
    }

    private static string Id(JsonElement i) => i.GetProperty("id").GetString()!;
    private static string Naam(JsonElement i) => i.GetProperty("name").GetString()!;
    private static string? Type(JsonElement i) => i.GetProperty("contentType").GetString();

    private static JsonElement? Prop(JsonElement i, string alias) =>
        i.TryGetProperty("properties", out var p) && p.TryGetProperty(alias, out var v) && v.ValueKind != JsonValueKind.Null ? v : null;

    private static string? Tekst(JsonElement i, string alias) => Prop(i, alias) is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;
    private static string? Str(JsonElement p, string alias) => p.TryGetProperty(alias, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static Toegangsniveau Niveau(JsonElement i) => Tekst(i, "toegangsniveau") == "leden" ? Toegangsniveau.Leden : Toegangsniveau.Publiek;

    private static string? EersteMediaId(JsonElement? picker) =>
        picker is { ValueKind: JsonValueKind.Array } a && a.GetArrayLength() > 0 ? a[0].GetProperty("id").GetString() : null;
}
