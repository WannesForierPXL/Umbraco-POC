namespace Llaser.ContentStub.Cms;

/// <summary>
/// De poort naar het CMS. De stub (en later de Content Service) praat enkel via deze interface met het CMS,
/// zodat de implementatie (Umbraco of Payload) uitwisselbaar blijft.
/// Elke methode levert content in precies de gevraagde taal, of null/leeg als die taal niet gepubliceerd is.
/// Terugvallen op een andere taal en afschermen zijn regels van de stub, niet van de adapter.
/// </summary>
public interface ICmsClient
{
    Task<IReadOnlyList<CmsGuide>> GetGuidesAsync(Taal taal, CancellationToken ct);
    Task<CmsGuide?> GetGuideAsync(string id, Taal taal, CancellationToken ct);
    Task<IReadOnlyList<CmsHoofdstuk>> GetHoofdstukkenAsync(string guideId, Taal taal, CancellationToken ct);
    Task<CmsHoofdstuk?> GetHoofdstukAsync(string id, Taal taal, CancellationToken ct);

    /// <summary>Guide-stappen van een Hoofdstuk, zonder blokken.</summary>
    Task<IReadOnlyList<CmsStap>> GetStappenAsync(string guideId, string hoofdstukId, Taal taal, CancellationToken ct);

    /// <summary>Eén Guide-stap met blokken.</summary>
    Task<CmsStap?> GetStapAsync(string id, Taal taal, CancellationToken ct);

    Task<IReadOnlyList<CmsResource>> GetResourcesAsync(Taal taal, CancellationToken ct);
    Task<CmsResource?> GetResourceAsync(string id, Taal taal, CancellationToken ct);

    Task<CmsBestand?> GetMediaAsync(string mediaId, CancellationToken ct);
}

public enum Taal { Nl, En }

public enum Toegangsniveau { Publiek, Leden }

public sealed record CmsGuide(string Id, string Titel, string? Intro, Taal Taal);

public sealed record CmsHoofdstuk(string Id, string Titel, string? Beschrijving, Toegangsniveau Toegangsniveau, Taal Taal);

public sealed record CmsStap(
    string Id,
    string GuideId,
    string HoofdstukId,
    string Titel,
    string Samenvatting,
    Toegangsniveau Toegangsniveau,
    IReadOnlyList<CmsBlok>? Blokken,
    Taal Taal);

public abstract record CmsBlok;
public sealed record CmsTekstBlok(string Html) : CmsBlok;
public sealed record CmsAfbeeldingBlok(string MediaId, string Alt, string? Bijschrift) : CmsBlok;
public sealed record CmsCalloutBlok(string Soort, string Tekst) : CmsBlok;

public sealed record CmsResource(
    string Id,
    string Titel,
    string? Beschrijving,
    string Soort,
    string? BestandMediaId,
    string? Url,
    Toegangsniveau Toegangsniveau,
    Taal Taal);

public sealed record CmsBestand(Stream Inhoud, string ContentType, string Bestandsnaam);
