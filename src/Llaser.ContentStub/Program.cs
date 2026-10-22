using System.Text.Json.Serialization;
using Llaser.ContentStub.Cms;

// Llaser.ContentStub: een minimale stand-in voor de Content Service.
// Leest content via ICmsClient, past de LLASER-regels toe (strengste wint, Teaser, terugval op NL)
// en levert de gedeelde DTO uit docs/contentmodel.md.

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase)));

void CmsHttp(HttpClient http)
{
    var cms = builder.Configuration.GetSection("Cms");
    http.BaseAddress = new Uri(cms["DeliveryApiUrl"] ?? throw new InvalidOperationException("Cms:DeliveryApiUrl ontbreekt"));
    http.DefaultRequestHeaders.Add("Api-Key", cms["ApiKey"] ?? throw new InvalidOperationException("Cms:ApiKey ontbreekt (user-secrets)"));
}

builder.Services.AddHttpClient<ICmsClient, UmbracoCmsClient>(CmsHttp);
builder.Services.AddScoped<ContentService>();

// Preview: dezelfde adapter en regels, maar het CMS levert concepten (niet-gepubliceerde versies).
builder.Services.AddHttpClient("cms-preview", http => { CmsHttp(http); http.DefaultRequestHeaders.Add("Preview", "true"); });
builder.Services.AddKeyedScoped<ContentService>("preview", (sp, _) =>
    new ContentService(new UmbracoCmsClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("cms-preview"))));
builder.Services.AddSingleton<WebhookOntvanger>();

var app = builder.Build();

// Demopagina (wwwroot/index.html): praat enkel met de endpoints hieronder, dus werkt ook voor de Payload-stub.
app.UseDefaultFiles();
app.UseStaticFiles();

// ---------- endpoints ----------

app.MapGet("/guides", async (HttpContext ctx, string? taal, ContentService content, CancellationToken ct) =>
    Results.Ok(await content.GetGuidesAsync(ParseTaal(taal), ct)));

app.MapGet("/guides/{id}", async (string id, HttpContext ctx, string? taal, ContentService content, CancellationToken ct) =>
    await content.GetGuideAsync(id, Lezer.Uit(ctx), ParseTaal(taal), ct) is { } guide ? Results.Ok(guide) : Results.NotFound());

app.MapGet("/guide/stappen/{id}", async (string id, HttpContext ctx, string? taal, ContentService content, CancellationToken ct) =>
    await content.GetStapAsync(id, Lezer.Uit(ctx), ParseTaal(taal), ct) switch
    {
        null => Results.NotFound(),
        { Afgeschermd: true } teaser => Results.Json(teaser, statusCode: StatusCodes.Status403Forbidden),
        var stap => Results.Ok(stap),
    });

app.MapGet("/resources", async (HttpContext ctx, string? taal, ContentService content, CancellationToken ct) =>
    Results.Ok(await content.GetResourcesAsync(Lezer.Uit(ctx), ParseTaal(taal), ct)));

app.MapGet("/resources/{id}", async (string id, HttpContext ctx, string? taal, ContentService content, CancellationToken ct) =>
    await content.GetResourceAsync(id, Lezer.Uit(ctx), ParseTaal(taal), ct) switch
    {
        null => Results.NotFound(),
        { Afgeschermd: true } teaser => Results.Json(teaser, statusCode: StatusCodes.Status403Forbidden),
        var resource => Results.Ok(resource),
    });

app.MapGet("/media/{id}", async (string id, HttpContext ctx, ContentService content, CancellationToken ct) =>
    await content.GetMediaAsync(id, Lezer.Uit(ctx), ct) switch
    {
        MediaResultaat.NietGevonden => Results.NotFound(),
        MediaResultaat.Afgeschermd => Results.StatusCode(StatusCodes.Status403Forbidden),
        MediaResultaat.Bestand { Inhoud: var b } => Results.Stream(b.Inhoud, b.ContentType, b.Bestandsnaam),
        _ => Results.NotFound(),
    });

// Preview voor "Save and preview" in de backoffice. Toont concepten, dus ENKEL in Development:
// in productie moet de BFF eerst nagaan dat de gebruiker een Editor is (Entra).
if (app.Environment.IsDevelopment())
{
    var editor = new Lezer(IsLid: true);
    var preview = app.MapGroup("/preview");

    preview.MapGet("/guides", async (string? taal, [FromKeyedServices("preview")] ContentService content, CancellationToken ct) =>
        Results.Ok(await content.GetGuidesAsync(ParseTaal(taal), ct)));

    preview.MapGet("/guides/{id}", async (string id, string? taal, [FromKeyedServices("preview")] ContentService content, CancellationToken ct) =>
        await content.GetGuideAsync(id, editor, ParseTaal(taal), ct) is { } guide ? Results.Ok(guide) : Results.NotFound());

    preview.MapGet("/guide/stappen/{id}", async (string id, string? taal, [FromKeyedServices("preview")] ContentService content, CancellationToken ct) =>
        await content.GetStapAsync(id, editor, ParseTaal(taal), ct) is { } stap ? Results.Ok(stap) : Results.NotFound());

    // Een Editor mag alle media zien: geen opzoeking van de content die het beeld gebruikt (die is mogelijk nog een concept).
    preview.MapGet("/media/{id}", async (string id, [FromKeyedServices("preview")] ContentService content, CancellationToken ct) =>
        await content.GetMediaVoorEditorAsync(id, ct) is { } b
            ? Results.Stream(b.Inhoud, b.ContentType, b.Bestandsnaam)
            : Results.NotFound());
}

// Webhook van het CMS bij publiceren/depubliceren. In productie: event op de outbox → Azure Service Bus.
app.MapPost("/webhooks/cms", async (HttpRequest request, WebhookOntvanger ontvanger) =>
{
    if (!ontvanger.SecretKlopt(request.Headers["X-Webhook-Secret"])) return Results.Unauthorized();
    using var reader = new StreamReader(request.Body);
    var resultaat = ontvanger.Ontvang(request.Headers["Umb-Webhook-Event"].ToString(), await reader.ReadToEndAsync());
    return resultaat is null ? Results.BadRequest() : Results.Accepted(value: resultaat);
});

// Enkel voor de demo: de laatst ontvangen events
app.MapGet("/webhooks/cms/events", (WebhookOntvanger ontvanger) => ontvanger.Laatste());

app.Run();

static Taal ParseTaal(string? taal) => string.Equals(taal, "en", StringComparison.OrdinalIgnoreCase) ? Taal.En : Taal.Nl;

// ---------- lezer (mock-login) ----------

/// <summary>Wie de content leest. In productie komt dit uit de Entra-claims die de BFF doorgeeft.</summary>
sealed record Lezer(bool IsLid)
{
    public static Lezer Uit(HttpContext ctx) =>
        new(string.Equals(ctx.Request.Headers["X-Mock-Lid"], "true", StringComparison.OrdinalIgnoreCase));

    public bool MagZien(Toegangsniveau niveau) => niveau == Toegangsniveau.Publiek || IsLid;
}

// ---------- regels ----------

/// <summary>De LLASER-regels, CMS-onafhankelijk: werkt voor elke ICmsClient-implementatie.</summary>
sealed class ContentService(ICmsClient cms)
{
    private const Taal Standaardtaal = Taal.Nl;

    /// <summary>Regel 1: het strengste toegangsniveau van stap en Hoofdstuk wint.</summary>
    private static Toegangsniveau Strengste(Toegangsniveau a, Toegangsniveau b) =>
        a == Toegangsniveau.Leden || b == Toegangsniveau.Leden ? Toegangsniveau.Leden : Toegangsniveau.Publiek;

    public async Task<IReadOnlyList<GuideSamenvattingDto>> GetGuidesAsync(Taal taal, CancellationToken ct) =>
        (await LijstMetTerugval(t => cms.GetGuidesAsync(t, ct), x => x.Id, taal))
            .Select(g => new GuideSamenvattingDto(g.Id, g.Titel, g.Intro, g.Taal))
            .ToList();

    public async Task<GuideDto?> GetGuideAsync(string id, Lezer lezer, Taal taal, CancellationToken ct)
    {
        var guide = await MetTerugval(t => cms.GetGuideAsync(id, t, ct), taal);
        if (guide is null) return null;

        var hoofdstukken = new List<HoofdstukDto>();
        foreach (var h in await LijstMetTerugval(t => cms.GetHoofdstukkenAsync(guide.Id, t, ct), x => x.Id, taal))
        {
            var stappen = (await LijstMetTerugval(t => cms.GetStappenAsync(guide.Id, h.Id, t, ct), x => x.Id, taal))
                .Select(s =>
                {
                    var niveau = Strengste(h.Toegangsniveau, s.Toegangsniveau);
                    return new StapSamenvattingDto(s.Id, s.Titel, s.Samenvatting, niveau, !lezer.MagZien(niveau), s.Taal);
                })
                .ToList();
            hoofdstukken.Add(new HoofdstukDto(h.Id, h.Titel, h.Beschrijving, h.Toegangsniveau, !lezer.MagZien(h.Toegangsniveau), h.Taal, stappen));
        }

        return new GuideDto(guide.Id, guide.Titel, guide.Intro, guide.Taal, taal, hoofdstukken);
    }

    public async Task<StapDto?> GetStapAsync(string id, Lezer lezer, Taal taal, CancellationToken ct)
    {
        var stap = await MetTerugval(t => cms.GetStapAsync(id, t, ct), taal);
        if (stap is null) return null;
        var hoofdstuk = await MetTerugval(t => cms.GetHoofdstukAsync(stap.HoofdstukId, t, ct), taal);
        var guide = await MetTerugval(t => cms.GetGuideAsync(stap.GuideId, t, ct), taal);
        if (hoofdstuk is null || guide is null) return null;

        var niveau = Strengste(hoofdstuk.Toegangsniveau, stap.Toegangsniveau);
        var afgeschermd = !lezer.MagZien(niveau);

        // Regel 2: een Teaser bevat nooit blokken.
        var blokken = afgeschermd ? null : stap.Blokken?.Select(MapBlok).ToList();
        return new StapDto(stap.Id, stap.Titel, stap.Samenvatting, niveau, afgeschermd, stap.Taal, taal,
            new RefDto(guide.Id, guide.Titel), new RefDto(hoofdstuk.Id, hoofdstuk.Titel), blokken);
    }

    public async Task<IReadOnlyList<ResourceDto>> GetResourcesAsync(Lezer lezer, Taal taal, CancellationToken ct) =>
        (await LijstMetTerugval(t => cms.GetResourcesAsync(t, ct), x => x.Id, taal))
            .Select(r => MapResource(r, lezer, taal))
            .ToList();

    public async Task<ResourceDto?> GetResourceAsync(string id, Lezer lezer, Taal taal, CancellationToken ct) =>
        await MetTerugval(t => cms.GetResourceAsync(id, t, ct), taal) is { } r ? MapResource(r, lezer, taal) : null;

    /// <summary>
    /// Regel 4: media enkel via de stub. Een media-item is zo streng afgeschermd als de strengste content die het gebruikt;
    /// media die door geen enkele gepubliceerde content gebruikt wordt, geeft de stub niet vrij.
    /// </summary>
    public async Task<MediaResultaat> GetMediaAsync(string mediaId, Lezer lezer, CancellationToken ct)
    {
        var niveau = await MediaNiveauAsync(mediaId, ct);
        if (niveau is null) return new MediaResultaat.NietGevonden();
        if (!lezer.MagZien(niveau.Value)) return new MediaResultaat.Afgeschermd();
        return await cms.GetMediaAsync(mediaId, ct) is { } bestand ? new MediaResultaat.Bestand(bestand) : new MediaResultaat.NietGevonden();
    }

    /// <summary>Enkel voor preview: een Editor ziet alle media, zonder toegangscontrole.</summary>
    public Task<CmsBestand?> GetMediaVoorEditorAsync(string mediaId, CancellationToken ct) => cms.GetMediaAsync(mediaId, ct);

    private async Task<Toegangsniveau?> MediaNiveauAsync(string mediaId, CancellationToken ct)
    {
        // POC: elke keer de (kleine) contentboom doorlopen. In productie: een index bijhouden, bijgewerkt via de webhook.
        var niveaus = new List<Toegangsniveau>();
        foreach (var r in await cms.GetResourcesAsync(Standaardtaal, ct))
            if (r.BestandMediaId == mediaId) niveaus.Add(r.Toegangsniveau);

        foreach (var taal in new[] { Taal.Nl, Taal.En }) // ook content die enkel in EN bestaat
            foreach (var guide in await cms.GetGuidesAsync(taal, ct))
                foreach (var h in await cms.GetHoofdstukkenAsync(guide.Id, taal, ct))
                    foreach (var s in await cms.GetStappenAsync(guide.Id, h.Id, taal, ct))
                        if (await cms.GetStapAsync(s.Id, taal, ct) is { Blokken: { } blokken }
                            && blokken.OfType<CmsAfbeeldingBlok>().Any(b => b.MediaId == mediaId))
                            niveaus.Add(Strengste(h.Toegangsniveau, s.Toegangsniveau));

        // Publiek zodra één publieke plek het beeld al toont; anders leden.
        return niveaus.Count == 0 ? null : niveaus.Contains(Toegangsniveau.Publiek) ? Toegangsniveau.Publiek : Toegangsniveau.Leden;
    }

    private static ResourceDto MapResource(CmsResource r, Lezer lezer, Taal gevraagd)
    {
        var afgeschermd = !lezer.MagZien(r.Toegangsniveau);
        return new ResourceDto(r.Id, r.Titel, r.Beschrijving, r.Soort, r.Toegangsniveau, afgeschermd,
            BestandUrl: afgeschermd || r.BestandMediaId is null ? null : MediaUrl(r.BestandMediaId),
            Url: afgeschermd ? null : r.Url,
            r.Taal, gevraagd);
    }

    private static BlokDto MapBlok(CmsBlok blok) => blok switch
    {
        CmsTekstBlok t => new TekstBlokDto(t.Html),
        CmsAfbeeldingBlok a => new AfbeeldingBlokDto(MediaUrl(a.MediaId), a.Alt, a.Bijschrift),
        CmsCalloutBlok c => new CalloutBlokDto(c.Soort, c.Tekst),
        _ => throw new NotSupportedException(blok.GetType().Name),
    };

    private static string MediaUrl(string mediaId) => $"/media/{mediaId}";

    /// <summary>Terugval op NL: ontbreekt een item in de gevraagde taal, dan de NL-versie (DTO toont dan taal = nl).</summary>
    private static async Task<T?> MetTerugval<T>(Func<Taal, Task<T?>> ophalen, Taal taal) where T : class =>
        await ophalen(taal) ?? (taal == Standaardtaal ? null : await ophalen(Standaardtaal));

    /// <summary>
    /// Terugval voor lijsten: elk item in de gevraagde taal, anders in NL. Items die enkel in de gevraagde taal
    /// bestaan (bv. enkel EN) komen er ook in, op hun plaats in de boom (na het item dat er in die taal voor staat).
    /// </summary>
    private static async Task<IReadOnlyList<T>> LijstMetTerugval<T>(Func<Taal, Task<IReadOnlyList<T>>> ophalen, Func<T, string> id, Taal taal)
    {
        var standaard = await ophalen(Standaardtaal);
        if (taal == Standaardtaal) return standaard;

        var vertaald = await ophalen(taal);
        var vertaaldOpId = vertaald.ToDictionary(id);
        var resultaat = standaard.Select(item => vertaaldOpId.GetValueOrDefault(id(item)) ?? item).ToList();

        var inStandaard = standaard.Select(id).ToHashSet();
        string? vorige = null;
        foreach (var item in vertaald)
        {
            if (!inStandaard.Contains(id(item)))
            {
                var positie = vorige is null ? 0 : resultaat.FindIndex(x => id(x) == vorige) + 1;
                resultaat.Insert(positie, item);
            }
            vorige = id(item);
        }
        return resultaat;
    }
}

// ---------- webhook ----------

/// <summary>
/// Ontvangt CMS-webhooks en vertaalt ze naar CMS-onafhankelijke domeinevents (ContentPublished / ContentUnpublished).
/// Idempotent: het CMS kan dezelfde webhook opnieuw sturen (retry), een duplicaat wordt herkend en niet opnieuw verwerkt.
/// </summary>
sealed class WebhookOntvanger(IConfiguration configuration, ILogger<WebhookOntvanger> logger)
{
    private readonly byte[] _secret = System.Text.Encoding.UTF8.GetBytes(
        configuration["Webhook:Secret"] ?? throw new InvalidOperationException("Webhook:Secret ontbreekt (user-secrets)"));
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _gezien = new();
    private readonly System.Collections.Concurrent.ConcurrentQueue<ContentEvent> _laatste = new();

    public bool SecretKlopt(string? waarde) =>
        waarde is not null && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(waarde), _secret);

    public ContentEvent? Ontvang(string cmsEvent, string body)
    {
        var soort = cmsEvent switch
        {
            "Umbraco.ContentPublish" => "ContentPublished",
            "Umbraco.ContentUnpublish" => "ContentUnpublished",
            _ => null,
        };
        if (soort is null) return null;

        using var json = System.Text.Json.JsonDocument.Parse(body);
        var id = json.RootElement.GetProperty("id").GetString()!;
        // Publish (Minimal payload) bevat de gepubliceerde cultures; Unpublish enkel het id.
        var talen = json.RootElement.TryGetProperty("cultures", out var c) && c.ValueKind == System.Text.Json.JsonValueKind.Array
            ? c.EnumerateArray().Select(x => x.GetProperty("culture").GetString()!).Order().ToArray()
            : [];

        // Umbraco stuurt geen event-id mee; de payload zelf (incl. publicatiedatums) dient als idempotentiesleutel.
        var sleutel = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(cmsEvent + body)));
        var duplicaat = !_gezien.TryAdd(sleutel, 0);

        var e = new ContentEvent(soort, id, talen, DateTimeOffset.UtcNow, duplicaat);
        if (duplicaat)
        {
            logger.LogInformation("Duplicaat genegeerd: {Soort} {Id}", soort, id);
        }
        else
        {
            logger.LogInformation("Event {Soort} voor {Id} [{Talen}] — in productie naar de outbox", soort, id, string.Join(", ", talen));
        }

        _laatste.Enqueue(e);
        while (_laatste.Count > 50) _laatste.TryDequeue(out _);
        return e;
    }

    public IReadOnlyList<ContentEvent> Laatste() => _laatste.Reverse().ToList();
}

sealed record ContentEvent(string Soort, string ContentId, string[] Talen, DateTimeOffset Ontvangen, bool Duplicaat);

abstract record MediaResultaat
{
    public sealed record NietGevonden : MediaResultaat;
    public sealed record Afgeschermd : MediaResultaat;
    public sealed record Bestand(CmsBestand Inhoud) : MediaResultaat;
}

// ---------- DTO's (docs/contentmodel.md) ----------

sealed record GuideSamenvattingDto(string Id, string Titel, string? Intro, Taal Taal);

sealed record GuideDto(string Id, string Titel, string? Intro, Taal Taal, Taal GevraagdeTaal, IReadOnlyList<HoofdstukDto> Hoofdstukken);

sealed record HoofdstukDto(string Id, string Titel, string? Beschrijving, Toegangsniveau Toegangsniveau, bool Afgeschermd, Taal Taal,
    IReadOnlyList<StapSamenvattingDto> Stappen);

sealed record StapSamenvattingDto(string Id, string Titel, string Samenvatting, Toegangsniveau Toegangsniveau, bool Afgeschermd, Taal Taal);

sealed record RefDto(string Id, string Titel);

sealed record StapDto(string Id, string Titel, string Samenvatting, Toegangsniveau Toegangsniveau, bool Afgeschermd, Taal Taal,
    Taal GevraagdeTaal, RefDto Guide, RefDto Hoofdstuk,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<BlokDto>? Blokken);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TekstBlokDto), "tekst")]
[JsonDerivedType(typeof(AfbeeldingBlokDto), "afbeelding")]
[JsonDerivedType(typeof(CalloutBlokDto), "callout")]
abstract record BlokDto;
sealed record TekstBlokDto(string Html) : BlokDto;
sealed record AfbeeldingBlokDto(string Url, string Alt, string? Bijschrift) : BlokDto;
sealed record CalloutBlokDto(string Soort, string Tekst) : BlokDto;

sealed record ResourceDto(string Id, string Titel, string? Beschrijving, string Soort, Toegangsniveau Toegangsniveau, bool Afgeschermd,
    string? BestandUrl, string? Url, Taal Taal, Taal GevraagdeTaal);
