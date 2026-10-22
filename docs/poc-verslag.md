# POC-verslag: Umbraco

Bevindingen per stap van de Umbraco-POC voor LLASER. Scoren gebeurt volgens [criteria.md](criteria.md).

- **Versies:** Umbraco 17.7.1 (LTS, .NET 10), uSync 17.4.3 (gratis), SQL Server 2022
- **Timebox:** 2 dagen
- **Totale tijd:** _nog in te vullen_

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
- **Tijd:**
- **Wat gedaan:**
- **Wat werkte:**
- **Wat niet:**

### Stap 8 – Verslag afwerken

- **Criterium:** alle
- **Tijd:**

### Stap 9 – Stretch: Azurite, Entra-login backoffice

- **Criterium:** K6, K8 (telt niet mee in de timebox)
- **Tijd:**
- **Wat gedaan:**
- **Wat werkte:**
- **Wat niet:**

## Bevindingen uit het vooronderzoek

Bronnen: de research-branches gelinkt vanuit de [Wayfinder-map](https://github.com/WannesForierPXL/Umbraco-POC/issues/1).

- **Versies (K6):** `dotnet new umbraco` installeert standaard 18.x (STS); 17.7.1 moet expliciet gepind worden.
- **Model als code (K5):** gratis uSync bewaart geen user groups; uSync importeert niet automatisch bij opstart.
- **Delivery API (K3):** geen ingebouwde taalterugval; voorouders ophalen vraagt een extra request; `/media/...` blijft publiek.
- **Webhooks (K7):** geen HMAC en geen event-id; één taal offline halen stuurt een Publish-webhook; het secret komt in uSync-XML terecht.

## Architectuurvragen

### Hoe blijft de Delivery API "enkel intern" als de backoffice publiek is?

_In te vullen. Opties: enkel API-key / pad blokkeren aan de ingress / aparte instanties._

### Wat betekent het databasetype voor kosten en beheer?

_In te vullen._

### Hoe wordt de webhook in productie een event op Service Bus?

_In te vullen. Vertrekpunt: eigen notification handler die naar een outbox schrijft._

### Hoe schermen we media van afgeschermde content af?

_In te vullen. Opties: proxy via de Content Service / private Blob Storage met SAS-URL's._

## Scores

Zie de scoretabel in [criteria.md](criteria.md).

## Conclusie

_In te vullen in stap 8._
