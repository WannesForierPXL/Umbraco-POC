# LLASER – CMS POC: Umbraco

## Context

Dit is een **sandbox-repo** voor een proof of concept binnen het IT Project LLASER (PXL, 2026-27).
LLASER bouwt een nieuwe website: een *single resource base* rond veilige LLM-integraties, met een
resource library (deels afgeschermd), nieuws, en een *best-practice guide* die experts zelf
aanvullen via een CMS.

In sprint 1 maken we **twee POC's** (Umbraco en Payload). Op basis daarvan kiest de **Klant** het CMS, op advies van het team.
Deze repo is de Umbraco-POC. Het doel is **een onderbouwde keuze**, geen productiecode.

### Doelarchitectuur (ter info, NIET bouwen in deze POC)

- Frontend: React/Next.js
- API-gateway/BFF: ASP.NET Core + YARP, login via Microsoft Entra ID (OIDC)
- Eigen services in ASP.NET Core (.NET 10): Content Service, Search Service (Elasticsearch),
  Notification Service
- Headless CMS: backoffice publiek (`cms.llaser.be`), Delivery API **enkel intern**
- Content Service leest content uit het CMS, mapt naar DTO's, filtert op toegangsniveau
- CMS stuurt een publish-webhook naar de Content Service → events `ContentPublished`,
  `ContentUnpublished` via Azure Service Bus
- Opslag: database voor het CMS, Azure Blob Storage voor media
- Hosting: Azure Container Apps

### Wat deze POC wel bouwt

Vuistregel: **bouw wat verschilt tussen Umbraco en Payload; wat voor beide hetzelfde is,
beredeneer je op papier** (in `docs/poc-verslag.md`).

| Onderdeel | Status |
|---|---|
| Umbraco (backoffice + Delivery API) | bouwen |
| SQL Server in Docker als CMS-database | bouwen |
| uSync: contentmodel als bestanden in de repo | bouwen |
| Contentmodel "Guide-stap" met bloktypes + toegangsniveau | bouwen |
| User groups Editor / Admin | bouwen |
| Delivery API dicht (geen public access, API-key) | bouwen |
| `Llaser.ContentStub`: minimal API die content ophaalt + webhook ontvangt | bouwen |
| Azurite / Blob Storage voor media | stretch |
| Backoffice-login via Entra ID | stretch |
| Gateway, broker, search, notification, React | **niet bouwen** |

## Structuur

```
Umbraco-POC/
├── CLAUDE.md
├── GLOSSARY.md               ← domeintaal (Guide, Hoofdstuk, Guide-stap, Toegangsniveau, Teaser, …)
├── docker-compose.yml        ← enkel SQL Server 2022
├── docs/
│   ├── criteria.md           ← beoordelingscriteria (gedeeld met Payload-POC)
│   ├── contentmodel.md       ← contentmodel, regels, stub-API en DTO (gedeeld met Payload-POC)
│   ├── demoscript.md         ← demo voor de Klant (gedeeld met Payload-POC)
│   └── poc-verslag.md        ← bevindingen per stap, architectuurvragen, scores, conclusie
├── http/poc.http             ← requests om de flow te testen
├── tests/stub-contract.mjs   ← CMS-onafhankelijke contracttest van de stub-API
└── src/
    ├── Llaser.Cms/           ← Umbraco 17.7.1 + uSync 17.4.3 (uSync/v17/) + webhookregistratie
    └── Llaser.ContentStub/   ← minimal API: Program.cs (regels + endpoints), Cms/ICmsClient + UmbracoCmsClient, wwwroot (demopagina)
```

## Versies (vastgepind)

- Umbraco **17.7.1** (LTS, .NET 10, EOL nov 2028). Niet `17.*` en niet de standaardtemplate (die kiest 18.x STS).
- uSync **17.4.3** (gratis). Geen uSync.Complete.
- SQL Server `mcr.microsoft.com/mssql/server:2022-latest`.
- .NET SDK 10.0.401 (`global.json`).

## Lokaal draaien

Zie README. Kort: `docker compose up -d`, dan `dotnet run` voor `src/Llaser.Cms` (profiel `Umbraco.Web.UI`) en
`src/Llaser.ContentStub` (profiel `https`). Eerste start op een lege database: unattended install + uSync importeert
model én voorbeeldcontent. Backoffice-login enkel via **HTTPS**: `admin@llaser.test` / `LlaserPoc2026!`.

## Secrets: bewuste uitzondering op werkregel 5

Op vraag van het team staan de **lokale demowaarden** in git, zodat testers zonder setup kunnen starten
(eerder faalde een setupscript met user-secrets bij testers):

| Waar | Wat |
|---|---|
| `docker-compose.yml` | SQL Server-wachtwoord (standaard, `.env` mag overschrijven) |
| `src/Llaser.Cms/appsettings.Development.json` | connection string, admin-login, imaging-HMAC, Delivery API-key, webhook-secret |
| `src/Llaser.ContentStub/appsettings.Development.json` | dezelfde API-key en webhook-secret |
| `http/http-client.env.json` | API-key en webhook-secret voor `poc.http` |

Deze waarden gelden **enkel lokaal** (Development). Echte secrets voor een gedeelde of online omgeving horen nooit in git
(user-secrets, omgevingsvariabelen of Key Vault). User-secrets op een machine gaan vóór deze waarden.

## Beslissingen

Alle beslissingen staan, met hun reden, in de [Wayfinder-map](https://github.com/WannesForierPXL/Umbraco-POC/issues/1). De belangrijkste:

- Contentmodel, regels en DTO: zie `docs/contentmodel.md` (bindend voor beide POC's).
- Talen `nl-BE` (standaard) en `en-US`; ontbrekende EN valt terug op NL (in de stub).
- Toegangsniveau `publiek` / `leden`; strengste van stap en Hoofdstuk wint; Bezoekers zien een Teaser. Login gemockt met `X-Mock-Lid: true`.
- Editor = Umbraco-groep `Editors` (publiceert overal), Admin = `Administrators`. User groups zitten niet in gratis uSync.
- Webhook in code aangemaakt (niet via uSync), zodat het secret buiten git blijft.
- `wwwroot/media` staat in git: gratis uSync bewaart geen mediabestanden.

## Werkregels voor Claude

1. **Stap per stap.** Werk de stappen hieronder in volgorde af. Begin pas aan een volgende stap
   als ik akkoord geef.
2. **Eerst uitleggen, dan doen.** Leg per stap kort uit wat je gaat doen en *waarom*, en welke
   Umbraco-concepten erbij komen kijken. Ik wil begrijpen wat er gebeurt, niet alleen code krijgen.
3. **Geen packages toevoegen zonder het te vragen.** Noem de package en de versie, en waarom.
4. **Versies controleren.** Gebruik de huidige Umbraco LTS op .NET 10. Controleer of packages
   (bv. uSync) compatibel zijn met die Umbraco-versie voor je ze voorstelt.
5. **Geen secrets in de repo.** Connection strings, API-keys en wachtwoorden via user-secrets of
   een `.env` die in `.gitignore` staat. Voorzie een `.env.example`.
6. **Contentmodel via uSync.** Alles wat in de backoffice gemodelleerd wordt, moet als bestand in
   de repo terechtkomen. Controleer dat na elke modelwijziging.
7. **Na elke stap het verslag bijwerken** in `docs/poc-verslag.md`: wat gedaan, wat werkte, wat
   niet, geschatte tijd, en welk criterium het raakt.
8. **Eén stap = één commit**, met een duidelijke boodschap (bv. `stap 3: contentmodel guide-stap`).
9. **Scope bewaken.** Als iets buiten de tabel "wat deze POC wel bouwt" valt, zeg het en stel voor
   om het als vraag in het verslag op te nemen in plaats van het te bouwen.

## Stappen

| # | Stap | Criterium |
|---|---|---|
| 0 | Repo, `.gitignore`, docs-skelet | – |
| 1 | Umbraco opzetten met SQL Server (Docker) en unattended install | K6 |
| 2 | uSync installeren, **vóór** er iets gemodelleerd wordt | K5 |
| 3 | Document type "Guide-stap" + bloktypes (tekst, afbeelding, callout) + eigenschap `toegangsniveau` (publiek / leden) | K1, K4 |
| 4 | User groups Editor en Admin; testen met twee accounts | K2 |
| 5 | Delivery API aan, public access uit, API-key; testen via `http/poc.http` (incl. uitklappen van blokken) | K3 |
| 6 | `Llaser.ContentStub`: `ICmsClient` + Umbraco-implementatie, mapt naar een eenvoudige DTO, filtert op toegangsniveau | K3, K4 |
| 7 | Webhook bij publiceren/depubliceren → endpoint in de stub (met gedeeld secret), event loggen | K7 |
| 8 | Verslag afwerken: scores, architectuurvragen, conclusie | alle |
| 9 | Stretch: Azurite voor media, Entra-login backoffice | K6, K8 |

## Ontwerpkeuzes (vastgelegd)

- **Afscherming via eigenschap, niet via Umbraco Members.** Ledenlogin gebeurt in productie via
  Entra in de BFF. Umbraco is enkel contentopslag; de Content Service filtert op `toegangsniveau`.
- **`ICmsClient` als poort.** De stub praat nooit rechtstreeks met de Umbraco-API buiten deze
  interface. Zo blijft de Content Service later CMS-onafhankelijk.
- **SQL Server i.p.v. PostgreSQL.** Umbraco ondersteunt PostgreSQL niet officieel; SQL Server
  komt overeen met Azure SQL, het realistische alternatief. Dit is een bevinding voor het verslag.

## Open architectuurvragen (beantwoorden in het verslag, niet bouwen)

- Umbraco serveert backoffice en Delivery API vanuit dezelfde app. Als de backoffice publiek is,
  hoe blijft de Delivery API "enkel intern"? (API-key alleen / pad blokkeren aan ingress /
  aparte instanties)
- Wat betekent een tweede databasetype (SQL Server naast PostgreSQL) voor kosten en beheer?
- Hoe zou de webhook in productie een event op Service Bus worden (outbox in de Content Service)?

De antwoorden staan in `docs/poc-verslag.md` (sectie Architectuurvragen).

## Status

Stap 0–8 gebouwd en gecommit. Stap 9 (Entra, Azurite) niet gebouwd: wacht op een Entra-tenant met
rechten voor een app-registratie, en op akkoord voor de package `Umbraco.StorageProviders.AzureBlob`.
Nog te doen door het team: de K1-meting met een teamlid (zie `docs/demoscript.md`).
