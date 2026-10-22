# Research: wat kan de Umbraco Delivery API voor ons contentmodel?

Ticket: [#3](https://github.com/WannesForierPXL/Umbraco-POC/issues/3) · Onderzocht op 2026-10-22

## Versie

- De huidige LTS is **Umbraco 17** (release 27 nov 2025, op .NET 10 LTS, security-fase tot 27 nov 2027, EOL 27 nov 2028). Umbraco 18 (25 jun 2026) is een STS-release; de volgende LTS is 21 (dec 2027). [Umbraco LTS & EOL](https://umbraco.com/products/knowledge-center/long-term-support-and-end-of-life/)
- De bronnen hieronder zijn de docs op `docs.umbraco.com/umbraco-cms/...` (de "latest"-versie). Voor de kernpagina is gecontroleerd dat de `17.latest`-versie hetzelfde zegt (zelfde config-keys, `v2`-paden, dezelfde fetch-selectors). [Delivery API (17.latest)](https://docs.umbraco.com/umbraco-cms/17.latest/reference/content-delivery-api.md)
- Alle endpoints staan onder `/umbraco/delivery/api/v2/...`.

## TL;DR

| Vraag | Antwoord |
|---|---|
| Intern houden | `Enabled: true`, `PublicAccess: false`, `ApiKey` instellen, en de header `Api-Key` meesturen. Daarnaast nog netwerkafscherming van `/umbraco/delivery/` voorzien (defense in depth). |
| Blocks en media expanden | `expand=properties[...]` en `fields=properties[...]`, genest via `alias[properties[...]]`. De properties van een block (content + settings) komen **altijd** mee; enkel pickers *in* een block moet je expanden. Mediapickers geven `url`/`width`/`height` ook zonder expand. |
| Ancestors | `fetch=ancestors:{id}` geeft de ancestors, maar **niet het item zelf**. Item + ancestors kan dus niet in één request: het zijn er 2 (of 1 met een custom selector). |
| Taal | `Accept-Language: en-US`. **Geen ingebouwde taal-fallback** in de Delivery API. Wel kan je een eigen `IPublishedValueFallback` registreren, die wordt aangeroepen voor lege properties. |
| Per type ophalen | `filter=contentType:guide` werkt zonder extra code. Filteren op `toegangsniveau` vraagt een **custom `IFilterHandler` + `IContentIndexHandler`** en daarna een index-rebuild. |
| Wat blijft publiek | `/media/...`-bestanden (altijd, los van de Delivery API), de backoffice `/umbraco` en de Management API `/umbraco/management/api/v1` (die vraagt wel backoffice-auth). |

## 1. Delivery API aanzetten, niet publiek, met API key

Opt-in. Je hebt een config-sectie nodig **en** `.AddDeliveryApi()` in `Program.cs`; daarna rebuild je de `DeliveryApiContentIndex` via Settings → Examine Management. [Delivery API](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/headless-and-apis/content-delivery-api)

```json
{
  "Umbraco": {
    "CMS": {
      "DeliveryApi": {
        "Enabled": true,
        "PublicAccess": false,
        "ApiKey": "<geheim, via secrets/env>",
        "AllowedContentTypeAliases": ["guide", "hoofdstuk", "guideStap", "resource"],
        "RichTextOutputAsJson": true
      }
    }
  }
}
```

```csharp
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers()
    .Build();
```

- `PublicAccess`: "Determines whether the enabled Delivery API should be publicly accessible or if access should require an API key." De standaardwaarde is publiek: "When the Delivery API is enabled in your project, all your published content will be made available to the public by default."
- `ApiKey`: de key die nodig is als public access uit staat. Dezelfde key geeft ook toegang tot preview (draft content, header `Preview: true`). Gevolg voor ons: wie de key heeft, kan ook drafts lezen.
- Header: **`Api-Key: <key>`**. Zonder geldige key krijg je `401 Unauthorized`.
- `AllowedContentTypeAliases` heeft voorrang op `DisallowedContentTypeAliases`. Rebuild de index na een wijziging.
- `RichTextOutputAsJson`: de docs raden JSON aan, omdat interne links dan beter routeren.
- Er is maar **één** `ApiKey`-waarde. Per-client keys of key-rotatie zonder downtime zijn niet gedocumenteerd.
- De Media Delivery API (`/umbraco/delivery/api/v2/media`) zet je apart aan met `DeliveryApi:Media:Enabled`. Die kan nooit publieker zijn dan de content-API: is `DeliveryApi:PublicAccess` false, dan heeft `Media:PublicAccess` geen effect. [Media Delivery API](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/headless-and-apis/content-delivery-api/media-delivery-api.md)

## 2. Blocks (Block List / Block Grid) en media expanden

Bron: [Property Expansion and Limiting](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/headless-and-apis/content-delivery-api/property-expansion-and-limiting.md)

- Expandable editors: Content Picker, Media Picker, Multinode Treepicker, **Block List, Block Grid** en RTE met blocks.
- Twee vuistregels: (1) expandable properties worden *niet* standaard ge-expand, (2) alle properties komen standaard mee en `fields` beperkt dat.
- **Blocks**: "the content and settings properties of blocks are always included in the output. However, these properties are not expanded." Onze text- en callout-blocks staan dus meteen volledig in de response. Voor een image-block met een mediapicker hoef je enkel te expanden als je properties van het mediatype nodig hebt (bv. `altText`).
- **Media zonder expand** bevat al `url` (bv. `/media/lyulnwzr/arrows.png`), `width`, `height`, `bytes`, `crops`, `focalPoint`. Enkel `properties` is leeg.
- Syntax in v17:
  - `expand=properties[$all]` of `expand=properties[alias1,alias2]`
  - genest: `expand=properties[alias[properties[nested1,nested2]]]`, ook als `$all[properties[...]]`
  - `fields=properties[...]` werkt met dezelfde syntax.
- Voorbeeld voor een Guide-stap met een Block List `inhoud` en een image-block met mediapicker `afbeelding`:

  ```
  GET /umbraco/delivery/api/v2/content/item/{stapId}
      ?expand=properties[inhoud[properties[afbeelding]]]
  Api-Key: ...
  Accept-Language: nl-BE
  ```

  Dit volgt het patroon uit de docs: `?expand=properties[blockList[properties[post[properties[coverImage]]]]]`. `expand` en `fields` werken op zowel het content- als het settings-deel van elk block.
- Er is geen API-limiet op de nestdiepte, maar de lengte van de URL is wel een harde grens. Diep geneste JSON kan de .NET `MaxDepth` overschrijden (`JsonException`); dat kan je verhogen via `AddJsonOptions(Constants.JsonOptionsNames.DeliveryApi, ...)`. [Delivery API](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/headless-and-apis/content-delivery-api)
- De docs zeggen zelf: "The query syntax quickly gets complicated, particularly when targeting block editors. You will likely need to experiment to get the query exactly right."

## 3. Ancestors/children ophalen voor het strengste toegangsniveau

Bron: [Delivery API, query parameters](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/headless-and-apis/content-delivery-api)

- Selectors op `GET /umbraco/delivery/api/v2/content`: `fetch=ancestors:{id|path}`, `fetch=children:{id|path}`, `fetch=descendants:{id|path}`.
- "Only one selector option can be applied to a query at a time." Je kan dus geen `fetch` combineren.
- `ancestors` geeft "all ancestors of a content item". Het item zelf zit daar volgens de docs niet bij, en er is geen parameter om item + ancestors samen op te halen. **Item + ancestors in één request kan dus niet** met de ingebouwde selectors. Opties:
  1. **Twee requests**: `GET /content/item/{stapId}` en `GET /content?fetch=ancestors:{stapId}&fields=properties[toegangsniveau]`. Met `fields` blijft het tweede request klein. De Content Service neemt dan het strengste van stap, hoofdstuk en guide.
  2. Een **custom selector** (`ISelectorHandler` + `IContentIndexHandler`), bv. `fetch=selfAndAncestors:{id}`, dat het pad van het item indexeert. Mogelijk, maar zelf te bouwen en te testen. [Extension API for Querying](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/headless-and-apis/content-delivery-api/extension-api-for-querying.md)
  3. Omgekeerd werken: haal een hele guide op met `fetch=descendants:{guideId}` en bereken het toegangsniveau in de Content Service voor alle stappen tegelijk. Dat past goed bij het renderen van een volledige guide. Let op paging (`skip`/`take`) en sorteer met `sort=level:asc&sort=sortOrder:asc`.
- Elk item heeft `route.path` en `route.startItem`, maar **geen parent-id of ancestor-ids** in de standaardoutput. Wie de boomstructuur nodig heeft, moet die met `fetch` opvragen.
- De query-endpoint werkt via de Examine-index (`DeliveryApiContentIndex`), niet live tegen de cache. Zonder `sort` volgt de volgorde de "relevance score" van de index.

## 4. Taalkeuze en fallback (EN → NL)

- Kies de taal met `Accept-Language: en-US` (of de ISO-code die in Umbraco geconfigureerd is) bij opvragen per id. Bij opvragen per path zit de cultuur al in het pad, maar een aanwezige `Accept-Language` "will take precedence over any other configuration settings". Culture- en hostname-config worden ook gebruikt. Varianten staan onder `cultures` in de output. [Delivery API, Localization](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/headless-and-apis/content-delivery-api)
- **Fallback: niet ingebouwd.** De docs beschrijven geen fallbackketen voor de Delivery API. `Fallback.ToLanguage` en verwante opties zijn enkel gedocumenteerd voor property-access in templates. [IPublishedContent properties](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/templating-and-rendering/querying/ipublishedcontent/properties.md)
- Primaire bron in de code: PR [#14421](https://github.com/umbraco/Umbraco-CMS/pull/14421) (merged in 12.0) zegt: "The Delivery API currently has no means of defining fallback for properties when requesting content ... if a solution replaces the default `IPublishedValueFallback` with a custom one, we definitively want this custom `IPublishedValueFallback` implementation invoked for empty properties." De taal-fallback die in de backoffice per taal is ingesteld, wordt dus **niet** automatisch toegepast. Met een eigen `IPublishedValueFallback` kan je ze wel afdwingen, voor lege properties.
- Een communitymelding (forum, v13) bevestigt het gedrag: vertaalde properties die leeg zijn, komen terug als `null` in plaats van de fallbackwaarde. [Forum](https://forum.umbraco.com/t/content-delivery-api-language-fallback-not-working-as-expected-in-umbraco-13/2276)
- **Niet gedocumenteerd**: wat je krijgt als de EN-variant van een item helemaal niet gepubliceerd is (404? lege properties?). Dat moeten we in de POC testen. Een property-fallback helpt niet als het hele item in EN niet bestaat. Opties: fallback in de Content Service (EN opvragen en bij 404 NL opvragen), of beleid: publiceer altijd een EN-variant.
- Caching: oudere bugs over het ontbreken van `Vary: Accept-Language` met output caching ([#19706](https://github.com/umbraco/Umbraco-CMS/issues/19706)) zijn gesloten (jul 2025). Let er toch op als we een cache tussen de Content Service en Umbraco zetten.

## 5. Alle items van één type ophalen en filteren op een eigen property

- Ingebouwd: `filter=contentType:guide` (ook negatie: `contentType:!guide`), `filter=name:...`, `createDate`/`updateDate` met `>`, `>:`, `<`, `<:`. Meerdere `filter`-parameters combineren als AND. Sorteren kan op `createDate`, `level`, `name`, `sortOrder` en `updateDate`. Pagineren met `skip`/`take`. [Delivery API](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/headless-and-apis/content-delivery-api)

  ```
  GET /umbraco/delivery/api/v2/content?filter=contentType:resource&sort=name:asc&skip=0&take=50
  ```

- **Filteren op `toegangsniveau` vraagt eigen code.** Je hebt een klasse nodig die `IContentIndexHandler` implementeert (velden toevoegen aan de `DeliveryApiContentIndex` via `GetFields()`/`GetFieldValues()`) en `IFilterHandler` (`CanHandle()` en `BuildFilterOption()`). Na een wijziging moet je de index rebuilden. Operators: `Is`, `IsNot`, `Contains`, `DoesNotContain`, en voor `Number`/`Date`-velden ook range-operators. Meerdere waarden in één filter gelden als OR, voor AND herhaal je de filter. [Extension API for Querying](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/headless-and-apis/content-delivery-api/extension-api-for-querying.md)

  ```
  GET /umbraco/delivery/api/v2/content?filter=contentType:guideStap&filter=toegangsniveau:publiek
  ```

- Let op: zo'n filter kijkt enkel naar de eigen waarde van het item. De regel "het strengste van Hoofdstuk en Guide-stap" kan je niet met een gewone property-filter uitdrukken. Wil je dat toch in een query, dan moet `GetFieldValues()` bij het indexeren het effectieve niveau berekenen (met de ancestors). Dan moet je de stappen wel herindexeren als een hoofdstuk wijzigt, en dat is niet gedocumenteerd. Ons advies: houd die logica in de Content Service, zoals gepland.
- De Media Delivery API heeft **geen** uitbreidingsmodel voor queries, en `fetch` ondersteunt daar enkel `children`. [Media Delivery API](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/headless-and-apis/content-delivery-api/media-delivery-api.md)

## 6. Wat blijft publiek als de Delivery API dicht is

- **Mediabestanden `/media/...`**: worden als statische bestanden geserveerd, los van de Delivery API. `PublicAccess: false` en `Media:PublicAccess: false` beschermen de bestands-URL's **niet**. Die settings gelden enkel voor de JSON-endpoints. Ook met `UmbracoMediaPhysicalRootPath` (media buiten de webroot) "composes that folder into the webroot file provider, so both static file serving and image processing continue to work". De bestanden blijven dus bereikbaar. [FileSystemProviders](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/configuration/filesystemproviders.md)
  - In de docs staat geen ingebouwde setting om `/media` alleen voor leden beschikbaar te maken. De enige media-gerelateerde bescherming is `EnableMediaRecycleBinProtection`: bestanden in de prullenbak worden onbereikbaar voor wie niet in de backoffice is ingelogd. Die staat standaard aan vanaf v18, in v17 zet je ze zelf aan. [Content Settings](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/configuration/contentsettings.md)
  - De "protected content"-functie (member auth via OIDC Authorization Code + PKCE) gaat over **content** in de Delivery API. Over media zegt ze niets, en standaard wordt beschermde content gewoon weggelaten. [Protected content](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/headless-and-apis/content-delivery-api/protected-content-in-the-delivery-api.md)
  - Opties om media te beschermen (eigen werk):
    1. `/media/*` op reverse-proxy- of netwerkniveau enkel intern toelaten, en laten serveren via de Content Service (proxy die eerst het toegangsniveau controleert).
    2. Eigen ASP.NET-middleware vóór de static-files-middleware die `/media` blokkeert zonder geldige key of token, plus een controller die bestanden streamt via `MediaFileManager` (dat is volgens de docs de aanbevolen manier om de stream van een bestand te krijgen).
    3. Blob storage (Azure) zonder publieke toegang, met signed URLs vanuit de Content Service. Dit vraagt een eigen file-system-provider of -configuratie, nog uit te zoeken.
  - Mediapaden zijn onvoorspelbaar (`/media/{random}/bestand.png`), maar dat is security through obscurity: de URL's lekken in elke response.
- **Backoffice** `/umbraco`: publiek, zoals gewenst. Inloggen gebeurt met backoffice-users.
- **Management API** `/umbraco/management/api/v1/...`: altijd aanwezig als de backoffice aanstaat, beschermd met backoffice-auth (OAuth Authorization Code + PKCE). In productie mag enkel de client `umbraco-back-office` verbinden. Swagger/OpenAPI op `/umbraco/openapi` staat in productie standaard uit. [Management API](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/headless-and-apis/management-api.md)
- **Netwerk**: de IIS-recept-docs doen het omgekeerde van wat wij willen. Ze sluiten `/umbraco/` af en laten `umbraco/api`, `umbraco/surface` en `umbraco/delivery` expliciet open. Voor ons moet de regel net `/umbraco/delivery/` (en eventueel `/media/`) enkel intern toelaten. [Lock down /umbraco (IIS)](https://docs.umbraco.com/umbraco-cms/run-in-production/security/lock-access-to-umbraco-folder.md)
- Website-rendering (`.AddWebsite()`): als er templates zijn, worden die publiek gerenderd. In een headless setup zet je geen templates op de documenttypes, of laat je `AddWebsite` weg. Dat moeten we nog verifiëren in de POC.

## 7. Verrassingen en beperkingen tegenover een headless-first CMS (Payload)

1. **Standaard publiek**: zodra de Delivery API aanstaat, is alle gepubliceerde content publiek tot je `PublicAccess: false` zet.
2. **Eén statische API key** voor lezen *en* preview. Er zijn geen scopes, geen keys per client en geen rotatiemechanisme. Payload heeft API keys per user/collectie met access control.
3. **Geen access control op veldniveau of per document via de API.** Je hebt enkel de Umbraco "public access" (member-gebaseerd, opt-in via OIDC) of je filtert extern, zoals wij doen. Payload heeft `access`-functies per collectie en veld.
4. **Query's lopen via de Examine-index.** Filteren of sorteren op eigen velden kan pas na C#-code plus een index-rebuild. Er is geen generieke `where`-query op properties zoals in Payload (`where[field][equals]=`). Indexwijzigingen en rebuilds horen bij deployment.
5. **Eén `fetch` per query**, en ancestors zonder het item zelf. Boomvragen kosten meerdere requests.
6. **Geen taal-fallback** in de API, ook al is fallback in de backoffice ingesteld. Payload heeft `fallback` voor lokalisatie ingebouwd.
7. **De expand/fields-syntax** wordt snel onleesbaar bij geneste blocks (de docs geven dat zelf toe). De URL-lengte is de enige grens.
8. **Media staat los van de API-beveiliging**: `/media` is altijd statisch en publiek. Bij Payload kan je upload-collecties ook achter access control zetten.
9. **Member Picker en Content Picker (voor members)** worden niet ondersteund in de Delivery API, om te vermijden dat memberdata lekt.
10. **Beschermde content** wordt stilzwijgend weggelaten. Na het opheffen van de bescherming moet je opnieuw publiceren of de index rebuilden.
11. Pluspunten: block-properties komen altijd mee (minder expand-gedoe dan verwacht), mediametadata (`url`, afmetingen, crops) zit standaard in de output, er zijn content-type-schema's in OpenAPI, en de selectors, filters en sorts zijn uitbreidbaar.

## Open punten voor de POC

- Testen wat `GET /content/item/{id}` met `Accept-Language: en-US` teruggeeft als enkel NL gepubliceerd is (404 of leeg).
- Een eigen `IPublishedValueFallback` (EN → NL) proberen en kijken of die ook in blocks werkt.
- Beslissen of het effectieve toegangsniveau in de Content Service (2 requests of `descendants`) of in de index wordt berekend.
- Een keuze maken voor de mediabescherming (proxy of middleware of blob) en die meten.
