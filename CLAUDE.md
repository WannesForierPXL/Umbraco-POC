# LLASER – CMS POC: Umbraco

## Context

Dit is een **sandbox-repo** voor een proof of concept binnen het IT Project LLASER (PXL, 2026-27).
LLASER bouwt een nieuwe website: een *single resource base* rond veilige LLM-integraties, met een
resource library (deels afgeschermd), nieuws, en een *best-practice guide* die experts zelf
aanvullen via een CMS.

In sprint 1 maken we **twee POC's** (Umbraco en Payload). Op basis daarvan kiest het team het CMS.
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
llaser-cms-poc-umbraco/
├── CLAUDE.md
├── docker-compose.yml        ← enkel SQL Server (later evt. Azurite)
├── docs/
│   ├── criteria.md           ← beoordelingscriteria (gedeeld met Payload-POC)
│   └── poc-verslag.md        ← bevindingen per stap + architectuurvragen
├── http/poc.http             ← requests om de flow te testen
└── src/
    ├── Llaser.Cms/           ← Umbraco-host + uSync-bestanden
    └── Llaser.ContentStub/   ← minimal API (één Program.cs + ICmsClient)
```

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

## Planning

Beslissingen en open vragen staan in de [Wayfinder-map](https://github.com/WannesForierPXL/Umbraco-POC/issues/1).
Deze `CLAUDE.md` wordt herzien in het ticket "Herziene CLAUDE.md voor de bouw"; tot dan gelden de
beslissingen in de map boven deze tekst.
