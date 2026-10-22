# POC-verslag: Umbraco

Bevindingen per stap van de Umbraco-POC voor LLASER. Scoren gebeurt volgens [criteria.md](criteria.md).

- **Versies:** Umbraco 17.7.1 (LTS, .NET 10), uSync 17.4.3 (gratis), SQL Server 2022
- **Timebox:** 2 dagen
- **Totale tijd:** ±7 u binnen de timebox (zie [Totaaltijd](#totaaltijd))
- **Gewogen score:** 65 % (zie [Scores](#scores); K1 voorlopig, K8 op papier)

## Stappen

<!-- Per stap: wat gedaan, wat werkte, wat niet, tijd, criterium -->

### Stap 0 – Repo, `.gitignore`, docs-skelet

- **Criterium:** –
- **Tijd:** ±15 min
- **Wat gedaan:** `.gitignore` (build-output, `.env`, Umbraco-runtimebestanden; `uSync/` bewust níet genegeerd), `.env.example`, skelet van dit verslag, `http/poc.http`, `CLAUDE.md`, `README.md`.
- **Wat werkte:** niets Umbraco-specifieks in deze stap.
- **Wat niet:** –

### Stap 1 – Umbraco met SQL Server (Docker) en unattended install

- **Criterium:** K6
- **Tijd:** ±30 min (waarvan ±1 min eerste start)
- **Wat gedaan:**
  - Template `Umbraco.Templates` 17.7.1 → `dotnet new umbraco -r LTS -pm PerProject`; package-versies van `17.*` naar exact `17.7.1` gepind.
  - `Llaser.slnx` + `global.json` (SDK 10.0.401).
  - `docker-compose.yml` met enkel SQL Server 2022 (Developer), volume en healthcheck; `sa`-wachtwoord uit `.env`.
  - Connection string en admin-wachtwoord in user-secrets; overige unattended-instellingen in `appsettings.Development.json`.
- **Wat werkte:**
  - Unattended install liep in één keer: 77 tabellen in `LlaserCms`, admin `admin@llaser.test` aangemaakt, geen wizard.
  - `InstallMissingDatabase` maakt de database zelf aan; geen SQL-script nodig.
- **Wat niet / opvallend:**
  - De template zet `17.*` (zwevend) en de template-tool stelt meteen voor te updaten naar 18.2.1 (STS). Zonder expliciet pinnen beland je niet op de LTS.
  - Bij de eerste start logt Umbraco één keer `Failed to detected SqlServer version` (Error), omdat de database dan nog niet bestaat. Onschuldig, maar verwarrend.
  - De template voegt standaard ook `Umbraco.Cms.DevelopmentMode.Backoffice` en `Microsoft.ICU.ICU4C.Runtime` toe.

### Stap 2 – uSync

- **Criterium:** K5
- **Tijd:** ±20 min
- **Wat gedaan:**
  - Package `uSync` 17.4.3 (gratis) toegevoegd.
  - `ImportOnFirstBoot: true` zodat een verse database het model uit de bestanden haalt; `ExportOnSave: All` (standaard) zodat elke wijziging in de backoffice meteen een bestand wordt.
  - `WebhookHandler` uitgeschakeld: het webhook-secret mag niet in git (webhook wordt in stap 7 in code aangemaakt).
  - Eenmalige export bij opstart via omgevingsvariabele `uSync__Settings__ExportAtStartup=All` (niet in de config): 48 bestanden in `src/Llaser.Cms/uSync/v17/` (DataTypes, Languages, MediaTypes, MemberTypes, RelationTypes).
- **Wat werkte:** installatie en eerste export zonder problemen; 12 handlers actief.
- **Wat niet / opvallend:**
  - Standaard importeert uSync niets bij opstart; zonder `ImportOnFirstBoot` start een nieuwe omgeving met een leeg model.
  - Het JSON-schema van uSync 17.4.3 noemt nog `uSync/v14/` als standaardmap, terwijl de map in de praktijk `uSync/v17/` is. De map heeft een versienummer in de naam: bij een upgrade naar een volgende major moet je hierop letten.
  - De standaardtaal na installatie is `en-US`. Voor LLASER moet dat `nl` worden (stap 3).
  - Bijvangst: de template zette `Imaging:HMACSecretKey` (ondertekent beeld-URL's) in `appsettings.json`, en die zat zo in de commit van stap 1. Verplaatst naar user-secrets met een nieuwe sleutel, zodat de oude waardeloos is.
  - Nog niet getest: een volledige round-trip (lege database → import). Dat is pas zinvol met een echt model en gebeurt in stap 3.

### Stap 3 – Contentmodel (Guide-boom, bloktypes, Resource, NL/EN, toegangsniveau)

- **Criterium:** K1, K4 (en K5 voor de round-trip)
- **Tijd:** ±1,5 u modelleren in de backoffice (door een Umbraco-beginner, met checklist) + ±30 min voorbeeldcontent via script
- **Wat gedaan:**
  - Talen `nl-BE` (standaard) en `en-US`.
  - Data types `Toegangsniveau`, `Callout soort`, `Resource soort` (Radio Button List) en `Guide-blokken` (Block List, min. 1).
  - Element types `blokTekst`, `blokAfbeelding`, `blokCallout`; document types `guide`, `hoofdstuk`, `guideStap`, `resourceLibrary`, `resource` met toegestane kinderen. Geen templates (headless). `titel` = de naam van het item (al per taal).
  - **Block-level variance:** `blokken` is taalonafhankelijk, de tekstvelden *in* de blokken variëren per taal. EN heeft dus dezelfde blokken en hetzelfde beeld als NL.
  - Voorbeeldpad uit `docs/contentmodel.md` (10 documenten, 2 media) ingevuld via de Management API met een API-gebruiker (client credentials).
  - Round-trip-test: lege database `LlaserCmsRoundtrip` → unattended install → uSync `ImportOnFirstBoot` → identiek resultaat (8 types, 10 documenten, 19 gepubliceerde taalversies, 2 media). Import duurde ±11 s.
- **Wat werkte:**
  - Contentmodel en content komen volledig uit git terug, zonder handwerk.
  - Block-level variance doet precies wat de spec vraagt.
  - API-gebruikers met client credentials maken scripting tegen de Management API eenvoudig.
- **Wat niet / opvallend (K1):**
  - De schakelaar voor variatie per taal werd consequent omgekeerd ingesteld, ook met een checklist ernaast: alle variaties stonden verkeerd na de eerste poging. Voor een Admin die Umbraco niet kent is dit verwarrend.
  - Een eigenschap kon zonder alias worden toegevoegd; de fout kwam pas bij opslaan, als technische JSON-melding (`$.properties[0].alias`).
  - Bij nieuwe eigenschappen maakt de backoffice snel een *nieuw* data type aan (bv. `GuideStapSamenvattingTextArea`) i.p.v. het bestaande te hergebruiken.
  - Een typfout in een alias hernoemen kan, en uSync legt een `Rename`-markering vast zodat andere omgevingen meeverhuizen.
  - Radio Button List heeft geen standaardwaarde; `toegangsniveau` is daarom verplicht in plaats van "standaard publiek" (afwijking van de spec).
  - De backoffice-login werkt enkel via HTTPS (OpenIddict weigert HTTP), ook lokaal. In Container Apps met TLS-terminatie aan de ingress vraagt dat forwarded headers.
- **Wat niet / opvallend (K5):**
  - Gratis uSync exporteert enkel de *metadata* van media, niet de bestanden. Zonder `wwwroot/media` in git verwijst de content op een verse omgeving naar ontbrekende bestanden. Voor de POC staat `wwwroot/media` daarom in git; in productie hoort media in Blob Storage.
  - Umbraco waarschuwt dat een root met meerdere talen zonder domeinen "routing and caching issues" geeft. Voor headless gebruik via de Delivery API is dat (nog) niet relevant; te bekijken in stap 5.

### Stap 4 – User groups Editor en Admin

- **Criterium:** K2 (en K5)
- **Tijd:** ±30 min
- **Wat gedaan:**
  - De standaardgroepen van Umbraco nagekeken. Ze dekken onze rollen zonder aanpassing:

    | Rol | Umbraco-groep | Sections | Belangrijkste rechten | Start node |
    |---|---|---|---|---|
    | Editor | `Editors` (`editor`) | Content, Media (+ Forms) | Create, Update, Delete, Move, Sort, Rollback, **Publish, Unpublish** | geen (hele boom) |
    | Admin | `Administrators` (`admin`) | alle, incl. **Settings** en **Users** | alles, incl. Permissions, Culture and Hostnames | geen |

    Umbraco heeft ook `Writers` (bewerken maar **niet publiceren**) en `Translators`: een goedkeuringsflow (Writer dient in, Editor publiceert) zit er dus standaard in, al gebruiken we die niet.
  - Uitnodigingsmail gemockt via SMTP pickup directory (`Umbraco:CMS:Global:Smtp:DeliveryMethod = SpecifiedPickupDirectory`, map `umbraco/Mail`, niet in git). Daarvoor moest ook `Umbraco:CMS:WebRouting:UmbracoApplicationUrl` gezet worden.
  - `editor@llaser.test` uitgenodigd in `Editors`: de mail landt als `.eml` met een werkende `invite-user`-link.
  - Rechten automatisch getest met een tijdelijke API-gebruiker in `Editors` (daarna uitgeschakeld):

    | Actie | Resultaat |
    |---|---|
    | Document lezen | 200 ✅ |
    | Document publiceren | 200 ✅ |
    | Document type lezen | 200 (nodig om te kunnen bewerken) |
    | Document type aanmaken | 403 ✅ |
    | Gebruikers oplijsten / uitnodigen | 403 ✅ |
    | Taal aanmaken | 403 ✅ |
- **Wat werkte:** de standaardgroepen passen exact; geen configuratie nodig. Het rechtenmodel is fijnmazig (per actie, per taal, per start node).
- **Wat niet / opvallend:**
  - **User groups zitten niet in de gratis uSync** (enkel in uSync.Complete). Elke nieuwe omgeving moet de groepen dus handmatig nakijken; hier valt dat mee omdat we de standaardgroepen gebruiken. Minpunt K5.
  - De eerste uitnodiging faalde (`ApplicationUrlNotConfigured`), maar de gebruiker was **toch al aangemaakt** met status "Invited". Een tweede poging gaf `DuplicateEmail`; enkel "resend invite" werkte. De API-call is dus niet atomair.
  - Een gebruiker die ooit ingelogd heeft, kan niet verwijderd worden, enkel uitgeschakeld (audit trail). Goed voor productie, verrassend in een sandbox.
  - In productie vervangt Entra ID (stap 9) de uitnodiging grotendeels: wie in de juiste Entra-groep zit, krijgt automatisch een account met de juiste Umbraco-groep.
- **Nog handmatig te testen:** inloggen als Editor via de uitnodigingslink in een privévenster en controleren dat Settings en Users niet zichtbaar zijn (zie demoscript).

### Stap 5 – Delivery API dicht met API-key

- **Criterium:** K3 (en K4)
- **Tijd:** ±30 min
- **Wat gedaan:**
  - `Umbraco:CMS:DeliveryApi` → `Enabled: true`, `PublicAccess: false`, `RichTextOutputAsJson: false` (HTML); `.AddDeliveryApi()` in `Program.cs`. API-key in user-secrets.
  - Requests in `http/poc.http` (5.1–5.10), secrets via `http/http-client.private.env.json` (niet in git).
- **Wat werkte:**
  - Zonder of met foute key → 401; met key → 200.
  - De media-API (`/media/item/...`) moet **apart** aangezet worden (`DeliveryApi:Media:Enabled`, `PublicAccess: false`); daarna vraagt ook die de key. Ontdekt in stap 6: zonder die instelling geeft ze 401, ook mét key.
  - Blokken komen **standaard volledig uitgeklapt** terug, inclusief de media-URL, breedte en hoogte van beelden. Geen `expand` nodig.
  - `Accept-Language` kiest de taal; block-level variance levert dezelfde blokken met vertaalde tekst.
  - `fetch=children:` met `sort=sortOrder:asc` geeft de boomvolgorde; `fields=` beperkt de velden op het hoogste niveau.
  - `filter=contentType:` werkt zonder extra code.
- **Wat niet / opvallend:**
  - **De Delivery API-index was leeg** na het aanzetten: content die vóór het aanzetten gepubliceerd werd, moet je eerst herindexeren (`POST /indexer/DeliveryApiContentIndex/rebuild`). Zonder dat geeft elke query `total: 0`, zonder foutmelding.
  - **Geen taalterugval:** een enkel-NL item geeft in `en-US` een 404, en ontbreekt stil uit lijsten. De stub moet zelf terugvallen op NL.
  - `fetch=ancestors:` geeft het item zelf niet terug → voor "strengste wint" zijn 2 requests nodig (item + voorouders).
  - `fields=` werkt niet binnen blokken: blokken komen altijd volledig mee.
  - **Lek:** `/media/aadk4oql/checklist-llm-review.pdf` (een leden-Resource) is zonder key bereikbaar (200). De Delivery API beschermt enkel de *JSON*, niet de bestanden.
  - Eén statische API-key, die ook drafts/preview opent. Geen scopes, geen rotatie zonder herstart.
  - Umbraco logt bij elke publicatie dat een root met meerdere talen zonder domeinen "routing and caching issues" kan geven. Voor headless gebruik via de Delivery API merkten we daar niets van; de taal komt uit `Accept-Language`.

### Stap 6 – `Llaser.ContentStub`

- **Criterium:** K3, K4
- **Tijd:** ±1,5 u (incl. media-proxy)
- **Wat gedaan:**
  - `src/Llaser.ContentStub`: minimal API op .NET 10, **zonder extra packages**.
  - `Cms/ICmsClient.cs`: de poort, met een CMS-neutraal model (`CmsStap`, `CmsBlok`, …). Elke methode levert precies de gevraagde taal, of niets.
  - `Cms/UmbracoCmsClient.cs`: de enige plek die Umbraco kent (aliassen, cultures `nl-BE`/`en-US`, JSON van blokken en media, API-key). ±170 regels.
  - `Program.cs`: endpoints + `ContentService` met de regels **buiten** de adapter, zodat een Payload-adapter ze gratis krijgt: strengste wint, Teaser, terugval op NL, media enkel via de stub.
  - Media-proxy (stretch, gehaald): `/media/{id}` geeft een bestand enkel vrij als de lezer de strengste content mag zien die het gebruikt; media die geen gepubliceerde content gebruikt → 404.
  - `tests/stub-contract.mjs`: 19 controles op de regels uit de spec, **CMS-onafhankelijk** (praat enkel met de stub). Alle 19 slagen.
- **Wat werkte:**
  - De DTO uit de spec kon 1-op-1 gemaakt worden; geen enkele regel vroeg Umbraco-specifieke kennis buiten de adapter.
  - Mapping naar de DTO is eenvoudig: blokken komen al volledig uitgeklapt, rich text is al HTML.
- **Wat niet / opvallend:**
  - **Aantal requests:** `GET /guide?taal=en` kost ±8 Delivery API-requests (guide + hoofdstukken + stappen per hoofdstuk, elk in NL én EN voor de terugval). Een stap ophalen kost 3 requests (item, voorouders voor het parent-id, hoofdstuk). Het item bevat geen parent-id.
  - De terugval op NL voor lijsten moest zelf geschreven worden (NL-lijst als basis, EN-versie per item als die bestaat).
  - De media-proxy weet niet bij welk item een media-item hoort: Umbraco biedt geen "waar wordt dit gebruikt" in de Delivery API. De POC doorloopt de hele (kleine) boom; in productie moet de Content Service een index bijhouden, bijgewerkt via de webhook.
  - Het eigenlijke lek blijft: Umbraco serveert `/media/...` zelf publiek. De proxy helpt pas als `/media` aan de ingress dichtgezet wordt, of als media in private Blob Storage zit (zie architectuurvragen).

### Stap 7 – Webhook bij publiceren/depubliceren

- **Criterium:** K7
- **Tijd:** ±45 min
- **Wat gedaan:**
  - `src/Llaser.Cms/Webhooks/WebhookRegistratie.cs`: maakt bij opstart de webhook naar de stub aan (of werkt het secret bij) via `IWebhookService`. **In code, niet via uSync**, zodat het secret niet in git komt (uSync-`WebhookHandler` staat uit). ±70 regels.
  - Events `Umbraco.ContentPublish` en `Umbraco.ContentUnpublish`; `Umbraco:CMS:Webhook:PayloadType = Minimal` (expliciet, de v17-default is nog `Legacy`).
  - Stub: `POST /webhooks/cms` controleert `X-Webhook-Secret` (constant-time), vertaalt naar domeinevents `ContentPublished` / `ContentUnpublished`, logt, en is **idempotent** (hash van event + payload). `GET /webhooks/cms/events` toont de laatste events voor de demo.
  - Eind-tot-eind getest door via de Management API te publiceren en depubliceren.
- **Wat werkte:**
  - Webhook in code aanmaken: eenvoudige, goed gedocumenteerde service; werkte meteen.
  - Secret in een eigen header; zonder of met fout secret → 401. Een herhaalde webhook wordt als duplicaat herkend.
  - Publish-payload (Minimal) bevat het id en de gepubliceerde talen: genoeg voor een `ContentPublished`-event.
- **Wat niet / opvallend:** (eind-tot-eindtest)

  | Actie in Umbraco | Wat de stub ontving |
  |---|---|
  | Publiceren NL + EN | `ContentPublished [en-US, nl-BE]` |
  | **Enkel EN offline** | `ContentPublished [nl-BE]` (géén Unpublished) |
  | Alles depubliceren | `ContentUnpublished []` **én** een extra `ContentPublished []` (lege taallijst) |
  | Opnieuw publiceren | `ContentPublished [en-US, nl-BE]` |

  - **Volgorde niet gegarandeerd:** drie acties met telkens 15 s ertussen kwamen tegelijk binnen, en de "EN offline"-webhook kwam vóór de eerste publicatie aan. Umbraco verwerkt de wachtrij in batches en verstuurt die parallel.
  - **Vertraging:** tot ±30 s tussen de actie en de webhook.
  - **Geen event-id:** idempotentie moet op basis van de payload; twee echte publicaties met dezelfde datum zijn niet te onderscheiden van een retry.
  - Een "taal offline" moet de consument zelf afleiden door de taallijst te vergelijken met de vorige toestand.
  - Het secret staat in plaintext in de database en in de webhook-logs van Umbraco (alle request headers worden gelogd).
  - Na ±1 minuut retries gaat een webhook stil verloren (vooronderzoek); niet apart getest.
  - **Gevolg voor productie:** de ingebouwde webhook is goed genoeg voor een demo, maar niet als betrouwbare bron voor Service Bus-events. Zie de architectuurvraag over de outbox.

### Stap 8 – Verslag afwerken

- **Criterium:** alle
- **Tijd:** ±45 min (incl. demopagina en demoscript ±1 u extra, buiten de timebox)
- **Wat gedaan:** scores, architectuurvragen en conclusie hieronder; `docs/demoscript.md` en een CMS-onafhankelijke demopagina (`src/Llaser.ContentStub/wwwroot/index.html`, `https://localhost:5001`).

### Stap 9 – Stretch: Azurite, Entra-login backoffice

- **Criterium:** K6, K8 (telt niet mee in de timebox)
- **Status:** **niet gebouwd**, wel uitgezocht.
- **Entra-login backoffice:** vraagt een app-registratie in een Entra-tenant (client-id, secret, redirect-URI `/umbraco-signin-oidc`) en de package `Microsoft.AspNetCore.Authentication.OpenIdConnect`. Umbraco ondersteunt *external login providers* voor de backoffice, met auto-linking (account aanmaken bij eerste login) en, met wat eigen code, het omzetten van Entra-groepen naar Umbraco-groepen. Schatting: 2–3 u. Wacht op een tenant waarin we een app-registratie mogen maken.
- **Azurite / Blob Storage:** vraagt de package `Umbraco.StorageProviders.AzureBlob` (eerst te vragen). Dit lost het media-lek niet vanzelf op: Umbraco serveert de blobs nog steeds via `/media/...` (zie architectuurvragen).

### Extra – Headless preview (K1)

- **Criterium:** K1 (telt niet mee in de timebox)
- **Tijd:** ±45 min
- **Aanleiding:** "Save and preview" in de backoffice gaf "No template exists to render the document". Umbraco wil standaard een eigen Razor-template tonen; headless is er geen.
- **Wat gedaan:**
  - `src/Llaser.Cms/Preview/HeadlessPreviewUrlProvider.cs`: eigen `IUrlProvider` met `GetPreviewUrlAsync` (bestaat in v17), die `https://localhost:5001/?preview=1&taal=en&stap={id}` teruggeeft. ±40 regels.
  - De hoofdknop "Save and preview" vraagt altijd de provider met alias `umbDocumentUrlProvider`. Een eigen alias plus een backoffice-manifest (`previewOption`) zet de headless preview enkel in het uitklapmenu naast de knop, en dat vond niemand. Daarom neemt de eigen provider de alias `umbDocumentUrlProvider` over en staat hij vooraan: de hoofdknop opent nu de demopagina. Dat stond nergens duidelijk; gevonden in de backoffice-broncode.
  - `src/Llaser.Cms/Publiceren/OuderNietGepubliceerdMelding.cs`: publiceren van een item onder een niet-gepubliceerde ouder geeft nu een leesbare melding ("'X' kan pas online als 'Hoofdstuk' gepubliceerd is. Tip: Publish with descendants op …") in plaats van "check logs". ±40 regels.
  - Stub: `/preview/guide`, `/preview/guide/stappen/{id}` en `/preview/media/{id}`, met dezelfde adapter en regels maar de Delivery API-header `Preview: true` (concepten). **Enkel in Development**: in productie moet de BFF eerst nagaan dat de gebruiker een Editor is.
  - Demopagina: in preview-modus een balk "Voorvertoning", altijd als Lid.
- **Wat werkte:** een opgeslagen maar niet gepubliceerde stap is meteen zichtbaar in de preview met alle blokken; de publieke route geeft er een 404 voor.
- **Wat niet / opvallend:**
  - Preview is in Umbraco **niet** headless "out of the box": het vraagt een eigen URL-provider plus een preview-route in de frontend. (Payload heeft Live Preview ingebouwd; te vergelijken.)
  - **Publiceren faalde met "check logs"**, zonder fout in de log. De echte reden (via de API): `PathNotPublished`, de bovenliggende Guide en het Hoofdstuk waren nog niet gepubliceerd. Een Editor krijgt die uitleg niet te zien. Bovendien bestond de stap enkel in EN, waardoor publiceren in NL `ContentInvalid` gaf. Twee bevindingen voor K1/K2.
  - Een tweede Guide aanmaken kan zonder meer (Umbraco kan het aantal items van een type in de root niet beperken). De stub gaat uit van één Guide.
  - uSync exporteert ook **niet-gepubliceerde** content naar bestanden. Testcontent belandt zo makkelijk in git.

### Extra – Meerdere Guides en EN-only content

- **Aanleiding:** bij het zelf testen in de backoffice ontstonden een tweede Guide en items die enkel in EN bestonden; die verschenen niet op de demopagina.
- **Wijziging van de spec** (`docs/contentmodel.md`): meerdere Guides in de root (`GET /guides`, `GET /guides/{id}`), een stap verwijst naar zijn Guide, en een item dat enkel in EN bestaat verschijnt in de EN-weergave op zijn plaats in de boom.
- **Demopagina:** opent met een overzicht van de Guides; klik opent de Guide.
- **Tijd:** ±45 min. Contracttest uitgebreid: 20 controles, alle geslaagd.
- **Opvallend:** in Umbraco kan een Editor een item enkel in EN publiceren, ook al is NL de standaardtaal, tenzij NL als *mandatory* staat ingesteld. Bewust zo gelaten.

## Totaaltijd

| Stap | Tijd |
|---|---|
| 0 Repo en docs | 0,25 u |
| 1 Umbraco + SQL Server | 0,5 u |
| 2 uSync | 0,3 u |
| 3 Contentmodel + voorbeeldpad + round-trip | ±2 u (modelleren door een Umbraco-beginner, tijd geschat) |
| 4 Rollen | 0,5 u |
| 5 Delivery API | 0,5 u |
| 6 Stub | 1,5 u |
| 7 Webhook | 0,75 u |
| 8 Verslag | 0,75 u |
| **Totaal** | **±7 u**, ruim binnen de timebox van 2 dagen |

Buiten de timebox: vooronderzoek (3 research-tickets), demopagina en demoscript (±1 u).

## Bevindingen uit het vooronderzoek

Bronnen: de research-branches gelinkt vanuit de [Wayfinder-map](https://github.com/WannesForierPXL/Umbraco-POC/issues/1).

- **Versies (K6):** `dotnet new umbraco` installeert standaard 18.x (STS); 17.7.1 moet expliciet gepind worden.
- **Model als code (K5):** gratis uSync bewaart geen user groups; uSync importeert niet automatisch bij opstart.
- **Delivery API (K3):** geen ingebouwde taalterugval; voorouders ophalen vraagt een extra request; `/media/...` blijft publiek.
- **Webhooks (K7):** geen HMAC en geen event-id; één taal offline halen stuurt een Publish-webhook; het secret komt in uSync-XML terecht.

## Architectuurvragen

### Hoe blijft de Delivery API "enkel intern" als de backoffice publiek is?

Umbraco serveert backoffice, Management API, Delivery API en `/media` uit één app. Drie opties:

| Optie | Wat | Oordeel |
|---|---|---|
| Enkel API-key | Delivery API publiek bereikbaar, maar 401 zonder key | Eén statische key, die ook drafts opent, zonder scopes. Lekt de key, dan is alles open. **Enkel als extra laag.** |
| Pad blokkeren aan de ingress | `/umbraco/delivery/*` en `/media/*` weigeren van buiten | Azure Container Apps-ingress heeft geen padregels; daarvoor is Front Door of Application Gateway (WAF-regel) nodig. Werkt, maar extra component en kosten. |
| **Twee instanties** van dezelfde image | Een **publieke** backoffice-instantie (rol *SchedulingPublisher*) en een **interne** delivery-instantie (rol *Subscriber*, interne ingress), op dezelfde database | Umbraco ondersteunt dit officieel (load balancing). De Delivery API is dan echt niet van buiten bereikbaar. Let op: webhooks en geplande publicaties draaien enkel op de SchedulingPublisher. |

**Advies:** twee instanties + API-key als tweede laag. Op de publieke instantie de Delivery API uitzetten en `/media` enkel voor aangemelde backoffice-gebruikers serveren.

### Wat betekent het databasetype voor kosten en beheer?

- Umbraco ondersteunt officieel **SQL Server / Azure SQL** (en SQLite voor ontwikkeling), geen PostgreSQL. Payload werkt met PostgreSQL of MongoDB.
- Gebruiken de eigen services (bv. de outbox van de Content Service) PostgreSQL, dan krijgt LLASER met Umbraco **twee database-engines**: twee soorten backups, monitoring, migratietools en kennis in het team. Kosten zijn voor dit volume klein (Azure SQL serverless of Basic ligt rond een paar tientallen euro per maand), het beheer weegt zwaarder dan de factuur.
- **Advies:** kies Umbraco, dan ook de eigen services op Azure SQL zetten (één engine). Kies Payload, dan PostgreSQL voor alles. Het databasetype volgt dus uit de CMS-keuze, niet omgekeerd.

### Hoe wordt de webhook in productie een event op Service Bus?

De POC toont dat de ingebouwde webhook **niet betrouwbaar genoeg** is als bron: geen event-id, geen volgorde, verlies na ±1 minuut retries, "taal offline" komt binnen als Publish, en depubliceren stuurt een extra Publish mee.

**Advies:**

1. Een eigen **notification handler in Umbraco** (`ContentPublishedNotification`, `ContentUnpublishedNotification`). Die heeft per document de gepubliceerde én gedepubliceerde talen (`PublishedCultures` / `UnpublishedCultures`), dus geen gissen naar "taal offline".
2. De handler schrijft een rij in een **outbox-tabel** (in de Umbraco-database, met eigen migratie), met een eigen event-id en volgnummer.
3. Een achtergrondproces stuurt de outbox naar **Azure Service Bus** (`ContentPublished` / `ContentUnpublished`, sessions per content-id voor volgorde) en markeert ze als verzonden.
4. Als vangnet: de Content Service doet periodiek een **reconciliatie** via de Delivery API (items gewijzigd sinds X).

Dit vraagt C#-code in het CMS. Voor een .NET-team is dat een voordeel (zelfde taal als de eigen services); het koppelt wel een stukje logica aan Umbraco. Bij Payload is het equivalent een `afterChange`-hook in TypeScript.

### Hoe schermen we media van afgeschermde content af?

- De POC bevestigt het lek: `/media/<map>/<bestand>.pdf` van een leden-Resource is zonder key bereikbaar (200). De Delivery API beschermt enkel de JSON.
- De media-proxy van de stub werkt (403 voor Bezoekers, 200 voor Leden), maar heeft pas zin als Umbraco de bestanden niet meer zelf publiek serveert.
- **Advies:**
  - Media in een **private Blob Storage-container** (via `Umbraco.StorageProviders.AzureBlob`).
  - `/media` op de Umbraco-instanties enkel voor aangemelde backoffice-gebruikers (middleware), of van buiten blokkeren.
  - De Content Service geeft bestanden vrij via een proxy (zoals de stub) of via een kortlevende **SAS-URL**, na de toegangscontrole.
  - De Content Service houdt bij welk media-item bij welke content hoort (index, bijgewerkt via de events); Umbraco biedt in de Delivery API geen "waar wordt dit gebruikt".

### Bijkomend: HTTPS achter de ingress

De backoffice-login werkt enkel via HTTPS (OpenIddict weigert HTTP). In Container Apps eindigt TLS aan de ingress; Umbraco moet dan de forwarded headers vertrouwen (`ForwardedHeaders` middleware) en `UmbracoApplicationUrl` kennen, anders faalt de login.

## Scores

Score 1–5 volgens [criteria.md](criteria.md). K1 is **voorlopig**: de meting met een teamlid dat de POC niet bouwde, moet nog gebeuren (zie demoscript). K8 is **op papier** (stap 9 niet gebouwd).

| # | Criterium | Gewicht | Umbraco | Motivering |
|---|---|---|---|---|
| K1 | Redactie-ervaring (visueel) | 25 % | **3** (voorlopig) | Block List met variatie per blok werkt zoals een Editor het verwacht (zelfde blokken, vertaalde tekst). Modelleren als Admin was verwarrend: de variatieschakelaar werd consequent omgekeerd gezet, een ontbrekende alias geeft een JSON-fout. Redactie door een Editor nog te meten. |
| K2 | Redactieflow en rollen | 20 % | **4** | Standaardgroepen passen exact (Editor publiceert, geen Settings/Users), rechten per actie/taal/start node, Writers-groep voor een goedkeuringsflow staat klaar. Min: uitnodiging-API niet atomair. |
| K3 | API en integratie | 15 % | **3** | API-key werkt, blokken komen volledig uitgeklapt, rich text als HTML: mapping is eenvoudig. Min: geen taalterugval, geen parent-id (3 requests per stap), lege index na aanzetten, media-API apart aan te zetten, één statische key. |
| K4 | Contentmodel en afscherming | 10 % | **3** | Boom, blokken, Resource en variatie per blok passen zonder omwegen. Min: geen standaardwaarde voor `toegangsniveau`, en media lekt via `/media`. |
| K5 | Model als code | 10 % | **3** | uSync round-trip werkt volledig, ook content (±11 s import). Min: afhankelijk van een externe package, user groups enkel in betalende uSync.Complete, mediabestanden niet mee, import bij opstart standaard uit, versienummer in de mapnaam. |
| K6 | Opzet, hosting en beheer | 10 % | **4** | ±30 min tot draaiend CMS met SQL Server in Docker, unattended install, LTS tot nov 2028, gratis (MIT), .NET zoals de eigen services. Min: versie moet gepind worden (template kiest 18 STS), template zet een secret in `appsettings.json`, HTTPS achter ingress vraagt aandacht, tweede databasetype. |
| K7 | Events en webhooks | 5 % | **2** | Werkt voor een demo (secret-header, Minimal payload). Niet betrouwbaar als bron: geen event-id, geen volgorde, taal-offline = Publish, extra Publish bij depubliceren, verlies na retries, secret in plaintext in logs. Eigen handler + outbox nodig. |
| K8 | Authenticatie backoffice (Entra) | 5 % | **3** (op papier) | External login providers met auto-linking zijn ingebouwd; groepsmapping vraagt eigen code. Niet gebouwd (geen tenant). |
| | **Gewogen totaal** | 100 % | **65 %** | (3×25 + 4×20 + 3×15 + 3×10 + 3×10 + 4×10 + 2×5 + 3×5) / 5 |

De Payload-kolom staat in [criteria.md](criteria.md) en wordt door de Payload-POC ingevuld.

## Conclusie

**Umbraco is een haalbare keuze voor LLASER.** Alles wat de POC moest aantonen, werkt: een meertalig contentmodel met blokken, Editor- en Admin-rollen, een afgeschermde Delivery API, een CMS-onafhankelijke Content Service met afscherming op `toegangsniveau`, en een webhook bij publiceren. De stub-contracttest (19 regels) slaagt volledig.

**Sterk:**

- Volwassen redactie-omgeving met een fijnmazig rechtenmodel dat zonder configuratie past.
- Variatie per blok: precies wat meertalige Guide-stappen nodig hebben.
- Zelfde stack als de eigen services (.NET 10, C#): eigen logica in het CMS (outbox, Entra-groepen) schrijft het team in de taal die het al kent.
- Gratis, LTS met 3 jaar ondersteuning.

**Zwak:**

- Model als code is niet native: het steunt op uSync, en user groups zitten enkel in de betalende versie.
- Webhooks zijn te zwak voor betrouwbare events; een eigen handler + outbox is nodig.
- Media afschermen is eigen werk (private Blob + proxy/SAS + `/media` dichtzetten).
- Delivery API en backoffice in één app: "enkel intern" vraagt twee instanties of een WAF.
- SQL Server als extra databasetype als de rest PostgreSQL wordt.

**Waar de vergelijking met Payload over moet gaan:** de redactie-ervaring voor experts (K1, nog te meten met een testpersoon in beide CMS'en), model als code (native in Payload, uSync in Umbraco), en de betrouwbaarheid van events. Op API-integratie en rollen ontlopen ze elkaar vermoedelijk weinig; de contracttest en de demopagina maken dat zichtbaar, want ze werken ongewijzigd tegen beide stubs.
