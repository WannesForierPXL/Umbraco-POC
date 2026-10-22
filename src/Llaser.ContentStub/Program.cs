using System.Text.Json.Serialization;
using Llaser.ContentStub.Cms;

// Llaser.ContentStub: een minimale stand-in voor de Content Service.
// Leest content via ICmsClient, past de LLASER-regels toe (strengste wint, Teaser, terugval op NL)
// en levert de gedeelde DTO uit docs/contentmodel.md.

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase)));

builder.Services.AddHttpClient<ICmsClient, UmbracoCmsClient>(http =>
{
    var cms = builder.Configuration.GetSection("Cms");
    http.BaseAddress = new Uri(cms["DeliveryApiUrl"] ?? throw new InvalidOperationException("Cms:DeliveryApiUrl ontbreekt"));
    http.DefaultRequestHeaders.Add("Api-Key", cms["ApiKey"] ?? throw new InvalidOperationException("Cms:ApiKey ontbreekt (user-secrets)"));
});
builder.Services.AddScoped<ContentService>();

var app = builder.Build();

// ---------- endpoints ----------

app.MapGet("/guide", async (HttpContext ctx, string? taal, ContentService content, CancellationToken ct) =>
    await content.GetGuideAsync(Lezer.Uit(ctx), ParseTaal(taal), ct) is { } guide ? Results.Ok(guide) : Results.NotFound());

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

    public async Task<GuideDto?> GetGuideAsync(Lezer lezer, Taal taal, CancellationToken ct)
    {
        var guide = await MetTerugval(t => cms.GetGuideAsync(t, ct), taal);
        if (guide is null) return null;

        var hoofdstukken = new List<HoofdstukDto>();
        foreach (var h in await LijstMetTerugval(t => cms.GetHoofdstukkenAsync(guide.Id, t, ct), x => x.Id, taal))
        {
            var stappen = (await LijstMetTerugval(t => cms.GetStappenAsync(h.Id, t, ct), x => x.Id, taal))
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
        if (hoofdstuk is null) return null;

        var niveau = Strengste(hoofdstuk.Toegangsniveau, stap.Toegangsniveau);
        var afgeschermd = !lezer.MagZien(niveau);

        // Regel 2: een Teaser bevat nooit blokken.
        var blokken = afgeschermd ? null : stap.Blokken?.Select(MapBlok).ToList();
        return new StapDto(stap.Id, stap.Titel, stap.Samenvatting, niveau, afgeschermd, stap.Taal, taal,
            new HoofdstukRefDto(hoofdstuk.Id, hoofdstuk.Titel), blokken);
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

    private async Task<Toegangsniveau?> MediaNiveauAsync(string mediaId, CancellationToken ct)
    {
        // POC: elke keer de (kleine) contentboom doorlopen. In productie: een index bijhouden, bijgewerkt via de webhook.
        var niveaus = new List<Toegangsniveau>();
        foreach (var r in await cms.GetResourcesAsync(Standaardtaal, ct))
            if (r.BestandMediaId == mediaId) niveaus.Add(r.Toegangsniveau);

        if (await cms.GetGuideAsync(Standaardtaal, ct) is { } guide)
            foreach (var h in await cms.GetHoofdstukkenAsync(guide.Id, Standaardtaal, ct))
                foreach (var s in await cms.GetStappenAsync(h.Id, Standaardtaal, ct))
                    if (await cms.GetStapAsync(s.Id, Standaardtaal, ct) is { Blokken: { } blokken }
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

    /// <summary>Terugval voor lijsten: volgorde en volledigheid uit NL, per item de gevraagde taal als die bestaat.</summary>
    private static async Task<IReadOnlyList<T>> LijstMetTerugval<T>(Func<Taal, Task<IReadOnlyList<T>>> ophalen, Func<T, string> id, Taal taal)
    {
        var standaard = await ophalen(Standaardtaal);
        if (taal == Standaardtaal) return standaard;
        var vertaald = (await ophalen(taal)).ToDictionary(id);
        return standaard.Select(item => vertaald.GetValueOrDefault(id(item)) ?? item).ToList();
    }
}

abstract record MediaResultaat
{
    public sealed record NietGevonden : MediaResultaat;
    public sealed record Afgeschermd : MediaResultaat;
    public sealed record Bestand(CmsBestand Inhoud) : MediaResultaat;
}

// ---------- DTO's (docs/contentmodel.md) ----------

sealed record GuideDto(string Id, string Titel, string? Intro, Taal Taal, Taal GevraagdeTaal, IReadOnlyList<HoofdstukDto> Hoofdstukken);

sealed record HoofdstukDto(string Id, string Titel, string? Beschrijving, Toegangsniveau Toegangsniveau, bool Afgeschermd, Taal Taal,
    IReadOnlyList<StapSamenvattingDto> Stappen);

sealed record StapSamenvattingDto(string Id, string Titel, string Samenvatting, Toegangsniveau Toegangsniveau, bool Afgeschermd, Taal Taal);

sealed record HoofdstukRefDto(string Id, string Titel);

sealed record StapDto(string Id, string Titel, string Samenvatting, Toegangsniveau Toegangsniveau, bool Afgeschermd, Taal Taal,
    Taal GevraagdeTaal, HoofdstukRefDto Hoofdstuk,
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
