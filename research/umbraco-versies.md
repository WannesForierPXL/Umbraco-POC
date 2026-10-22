# Umbraco LTS, uSync en .NET 10: welke versies passen samen?

Onderzoek voor issue #2. Peildatum: 2026-10-22. Alle versienummers komen van de bronnen die hieronder staan, op die datum.

## TL;DR

| Onderdeel | Keuze voor de POC | Bron |
|---|---|---|
| Umbraco CMS | **17.7.1** (LTS, .NET 10), NuGet `Umbraco.Cms` | [LTS/EOL-pagina][lts], [NuGet][nuget-cms], [GitHub release][gh-cms] |
| .NET | **.NET 10** (`net10.0`) | [Requirements v17][req] |
| uSync (gratis) | **17.4.3**, NuGet `uSync` (vereist Umbraco >= 17.3.0) | [NuGet][nuget-usync], [GitHub release][gh-usync] |
| uSync.Complete | Alleen nodig voor users/user groups (laatste v17-versie: 17.5.0, betalend) | [Jumoo][complete], [People Edition docs][people] |
| SQL Server | Minimum SQL Server 2016. Het officiële Umbraco-compose-template gebruikt `mcr.microsoft.com/mssql/server:2022-latest` | [Requirements v17][req], [template Dockerfile][tpl-db] |
| Unattended install | `Umbraco:CMS:Unattended:*` + `ConnectionStrings:umbracoDbDSN` (zie hieronder) | [Unattended install v17][unattended] |

## 1. Umbraco-versie: 17 is de huidige LTS op .NET 10 (bevestigd)

- De officiële LTS/EOL-tabel noemt **Umbraco 17** als LTS: release op 27 nov 2025, security phase vanaf 27 nov 2027, **end-of-life 27 nov 2028**. Letterlijk: "The most recent LTS is Umbraco 17, supporting .NET 10 LTS." ([LTS/EOL][lts])
- Laatste stabiele 17.x op NuGet: **17.7.1** (gepubliceerd 2026-10-06). Er staat een **17.8.0-rc** klaar (2026-10-15). ([NuGet][nuget-cms], [GitHub releases][gh-cms])
- De Umbraco 17-requirements vragen **.NET 10.0 of hoger** en Node.js 24.11.1+ (alleen voor wie backoffice-extensies bouwt). Visual Studio 2022 17.14+ of Rider 2025.3.0.1+. ([Requirements v17][req])

**Opgelet:** "latest" op NuGet is nu **Umbraco 18.2.1**, en dat draait *ook* op .NET 10. Maar 18 is een **STS**-release (EOL 25 jun 2027). Een kale `dotnet add package Umbraco.Cms`, of `dotnet new umbraco` met de nieuwste templates, geeft je dus 18 en niet de LTS. Pin de versie expliciet:

```bash
dotnet new install Umbraco.Templates::17.7.1
dotnet new umbraco -n UmbracoPoc --version 17.7.1
```

Volgende LTS: Umbraco 21, gepland op 9 dec 2027 ([LTS/EOL][lts]).

## 2. uSync: versie en wat de gratis versie dekt

### Versie

- uSync volgt de majorversie van Umbraco: uSync **17.x** hoort bij Umbraco 17, uSync 18.x bij Umbraco 18. De releasenotes op NuGet zeggen: "17.0 : Umbraco v17 release". ([NuGet uSync][nuget-usync])
- Laatste 17.x: **uSync 17.4.3** (GitHub-tag `v17.4.3`, 2026-09-30). De onderhoudsbranch in [KevinJump/uSync][gh-usync] is `v17/main`. Ernaast bestaat `v18/main` (18.1.5).
- `uSync.Core` 17.4.3 hangt af van `Umbraco.Cms.Api.Management` en `Umbraco.Cms.Web.Website` met range **`[17.3.0, )`**. Je hebt dus minstens Umbraco 17.3.0 nodig, en 17.7.1 voldoet. ([NuGet catalog-entry uSync.Core 17.4.3][catalog-core])

**Opgelet:** die range heeft geen bovengrens. NuGet houdt je niet tegen als je uSync 17 met Umbraco 18 combineert, of omgekeerd. Pin daarom beide versies zelf.

### Gratis uSync of uSync.Complete?

Dit volgt uit de handlers in de broncode van de gratis versie (`uSync.BackOffice/SyncHandlers/Handlers` op branch `v17/main`) ([GitHub tree][gh-handlers]):

| Wat je als bestand wilt bewaren | Gratis uSync? | Handler / toelichting |
|---|---|---|
| Document types (ook element types) | Ja | `ContentTypeHandler` |
| Data types | Ja | `DataTypeHandler` |
| Block-configuratie (Block List / Block Grid) | Ja | Zit in de configuratie van het data type (`DataTypeSerializer`). De gebruikte blocks zelf zijn element types, en die vallen onder `ContentTypeHandler` |
| Languages | Ja | `LanguageHandler` |
| Templates, media types, member types, dictionary, domains, relation types, webhooks, content templates, content en media | Ja | Elk een eigen handler |
| **User groups (en users, members, member groups)** | **Nee** | Er is geen user- of usergroup-handler in de gratis versie. Die zitten in **uSync.PeopleEdition**, dat onderdeel is van **uSync.Complete** ([People Edition docs][people]) |

- uSync.Complete is betalend: **£1150 excl. btw**, eenmalig per project, met een proefperiode van 60 dagen zonder limieten. ([Jumoo uSync.Complete][complete])
- Ook mét People Edition staan users en members sinds v13 standaard uit in het dashboard (ze moeten via config aan). Wachtwoordhashes komen mee in de bestanden. Het hoofd-admin-account synct nooit. ([People Edition docs][people])

**Conclusie:** voor document types, data types, block-configuratie en languages volstaat gratis uSync 17.4.3. Voor **user groups** heb je uSync.Complete nodig. Een gratis alternatief is de user groups in code aanmaken, bijvoorbeeld met een migration of een composer met `IUserGroupService`. Dat is niet als bron geverifieerd en dus een suggestie.

### Relevante uSync-config (defaults uit de [uSync-configreferentie][usync-config])

- `uSync:Settings:Folders` staat standaard op `["uSync/root/", "uSync/v17"]`. De bestanden komen dus in een map met de versie in de naam.
- `ExportOnSave: "All"`. Elke save in de backoffice schrijft meteen bestanden weg.
- `ImportAtStartup: "None"` en `ImportOnFirstBoot: false`. Een verse container importeert dus **niets** vanzelf. Zet `ImportOnFirstBoot: true` (met `FirstBootGroup: "All"`), of `ImportAtStartup: "Settings"`, als de POC uit de repo moet opbouwen.
- Om alleen settings en geen content te exporteren: `"ExportOnSave": "Settings"` ([NuGet README][nuget-usync]).

## 3. SQL Server in Docker

- Umbraco 17 volgt de Microsoft-lifecycle: het minimum is **SQL Server 2016** ([Requirements v17][req]). Gebruik een **case-insensitive collation** zoals `SQL_Latin1_General_CP1_CI_AS`, want de DAL ondersteunt geen case-sensitive namen ([Requirements v17][req]).
- Het officiële `umbraco-compose`-template in release 17.7.1 bouwt de database op **`mcr.microsoft.com/mssql/server:2022-latest`** (`ACCEPT_EULA=Y`, `MSSQL_SA_PASSWORD`) ([Database/Dockerfile][tpl-db]). Een `setup.sql` maakt de database `umbracoDb` aan via `sqlcmd` uit `/opt/mssql-tools18` met `-C` (trust cert) ([setup.sql][tpl-sql], [startup.sh][tpl-startup]).
- Het Umbraco-image zelf draait op `mcr.microsoft.com/dotnet/aspnet:10.0` en bouwt met `sdk:10.0` ([UmbracoProject/Dockerfile][tpl-app]).
- Rechten: `db_owner`, of `db_datareader` + `db_datawriter` + `db_ddladmin` voor install en upgrade ([Requirements v17][req]).

Advies voor de POC: `mcr.microsoft.com/mssql/server:2022-latest`. Dat is wat Umbraco zelf in zijn template test en meelevert.

## 4. Unattended install met SQL Server in Docker

Bron: [Unattended install v17][unattended], [Unattended settings v17][unattended-settings], [Global settings][global].

Voorwaarden: er is een connection string, en die wijst naar een **lege** database (of naar een database die nog niet bestaat, zie `InstallMissingDatabase`).

Configkeys als environment variables, geschikt voor docker-compose:

```yaml
environment:
  - ConnectionStrings__umbracoDbDSN=Server=umb_database;Database=umbracoDb;User Id=sa;Password=${DB_PASSWORD};TrustServerCertificate=true;
  - ConnectionStrings__umbracoDbDSN_ProviderName=Microsoft.Data.SqlClient
  - Umbraco__CMS__Unattended__InstallUnattended=true
  - Umbraco__CMS__Unattended__UnattendedUserName=Admin
  - Umbraco__CMS__Unattended__UnattendedUserEmail=admin@example.com
  - Umbraco__CMS__Unattended__UnattendedUserPassword=${ADMIN_PASSWORD}
  - Umbraco__CMS__Unattended__UnattendedTelemetryLevel=Minimal   # Minimal | Basic | Detailed (default Detailed)
  - Umbraco__CMS__Unattended__UpgradeUnattended=true
  - Umbraco__CMS__Global__InstallMissingDatabase=true            # optioneel: Umbraco maakt de DB zelf aan (vereist CREATE DATABASE-recht)
```

- `PackageMigrationsUnattended` staat standaard op `true`.
- `UpgradeUnattended: true` staat al in de `appsettings.json` van het projecttemplate ([template appsettings.json][tpl-appsettings]).
- Wie het project aanmaakt met `dotnet new umbraco --friendly-name ... --email ... --password ... --connection-string ...`, krijgt de `InstallUnattended`-blok automatisch in `appsettings.Development.json` ([template][tpl-appsettings-dev], [unattended docs][unattended]).
- Docker-scaffold: `dotnet new umbraco -n X --add-docker` en daarna `dotnet new umbraco-compose -P "X"` (opties: `-dbpw` met default `Password1234`, en `-p` voor de poort, default `44372`) ([Docker Compose docs v17][docker-docs]). Het gegenereerde compose-bestand zet de connection string en de provider al, maar **geen** unattended-keys. Die moet je zelf toevoegen, anders verschijnt de installwizard.

## 5. Verrassingen en aandachtspunten voor de vergelijking met Payload CMS

1. **"Latest" is niet LTS.** Umbraco 18 (STS) is de standaard op NuGet en in de templates, en draait ook op .NET 10. Pin 17.7.1 voor Umbraco en 17.4.3 voor uSync.
2. **Config-as-code is niet ingebouwd.** Bij Payload staat het schema van nature in code (TypeScript-config in git). Bij Umbraco staat het schema in de database en heb je een third-party package (uSync) nodig om het als XML-bestanden in git te krijgen. Dat package is gratis, maar **user groups vragen een betaalde licentie** (uSync.Complete, £1150).
3. **Geen automatische import standaard.** uSync importeert bij boot pas als je `ImportOnFirstBoot` of `ImportAtStartup` aanzet. Anders start een verse container met een leeg schema.
4. **De uSync-map heeft de versie in de naam** (`uSync/v17`). Bij een upgrade naar 18 of 21 moet je de mapconfiguratie meenemen.
5. **De docs zijn niet consistent:**
   - Het unattended-artikel gebruikt in een SQL Server-voorbeeld `umbracoDbDSN_ProviderName: "System.Data.SqlClient"`. Het compose-template en de connection-string-docs gebruiken `Microsoft.Data.SqlClient`. Gebruik die laatste.
   - Het SQLite-Dockerfile-voorbeeld in de v17-docs staat nog op `dotnet/aspnet:9.0`. Dat werkt niet voor Umbraco 17, gebruik `10.0`.
6. **De healthcheck in het compose-template draait maar om de 5 minuten** (`interval: 5m`, `start_interval: 5s`). De opstarttijd is daardoor sterk afhankelijk van de Docker-versie, want `start_interval` vereist Docker Engine 25+. Dit is niet als bron geverifieerd.
7. **Alleen x64?** De `mssql/server`-images zijn bij Microsoft alleen voor x64 bedoeld. Op Apple Silicon draait het via emulatie. Dit is niet als bron geverifieerd en verdient een check als iemand van het team een ARM-Mac gebruikt.
8. **Lange support.** Umbraco 17 krijgt fixes tot 27 nov 2027 en security-updates tot 28 nov 2028. Dat is een sterk argument tegenover het snellere releasetempo van Payload.

## Bronnen

- `lts`: <https://umbraco.com/products/knowledge-center/long-term-support-and-end-of-life/>
- `nuget-cms`: <https://www.nuget.org/packages/Umbraco.Cms>
- `gh-cms`: <https://github.com/umbraco/Umbraco-CMS/releases/tag/release-17.7.1>
- `req`: <https://github.com/umbraco/UmbracoDocs/blob/main/17/umbraco-cms/get-started/installation/requirements.md>
- `unattended`: <https://github.com/umbraco/UmbracoDocs/blob/main/17/umbraco-cms/get-started/installation/unattended-install.md>
- `unattended-settings`: <https://github.com/umbraco/UmbracoDocs/blob/main/17/umbraco-cms/develop-with-umbraco/configuration/unattendedsettings.md>
- `global`: <https://github.com/umbraco/UmbracoDocs/blob/main/17/umbraco-cms/develop-with-umbraco/configuration/globalsettings.md>
- `docker-docs`: <https://github.com/umbraco/UmbracoDocs/blob/main/17/umbraco-cms/get-started/installation/running-umbraco-on-docker-locally.md>
- `tpl-db`: <https://github.com/umbraco/Umbraco-CMS/blob/release-17.7.1/templates/UmbracoDockerCompose/Database/Dockerfile>
- `tpl-sql`: <https://github.com/umbraco/Umbraco-CMS/blob/release-17.7.1/templates/UmbracoDockerCompose/Database/setup.sql>
- `tpl-startup`: <https://github.com/umbraco/Umbraco-CMS/blob/release-17.7.1/templates/UmbracoDockerCompose/Database/startup.sh>
- `tpl-app`: <https://github.com/umbraco/Umbraco-CMS/blob/release-17.7.1/templates/UmbracoProject/Dockerfile>
- `tpl-appsettings`: <https://github.com/umbraco/Umbraco-CMS/blob/release-17.7.1/templates/UmbracoProject/appsettings.json>
- `tpl-appsettings-dev`: <https://github.com/umbraco/Umbraco-CMS/blob/release-17.7.1/templates/UmbracoProject/appsettings.Development.json>
- `nuget-usync`: <https://www.nuget.org/packages/uSync>
- `gh-usync`: <https://github.com/KevinJump/uSync/releases/tag/v17.4.3>
- `gh-handlers`: <https://github.com/KevinJump/uSync/tree/v17/main/uSync.BackOffice/SyncHandlers/Handlers>
- `catalog-core`: <https://api.nuget.org/v3/catalog0/data/2026.09.30.12.51.09/usync.core.17.4.3.json>
- `usync-config`: <https://docs.jumoo.co.uk/usync/uSync/reference/config/>
- `people`: <https://docs.jumoo.co.uk/usync/complete/guides/People_Edition/people/>
- `complete`: <https://jumoo.co.uk/usync/complete/>

[lts]: https://umbraco.com/products/knowledge-center/long-term-support-and-end-of-life/
[nuget-cms]: https://www.nuget.org/packages/Umbraco.Cms
[gh-cms]: https://github.com/umbraco/Umbraco-CMS/releases/tag/release-17.7.1
[req]: https://github.com/umbraco/UmbracoDocs/blob/main/17/umbraco-cms/get-started/installation/requirements.md
[unattended]: https://github.com/umbraco/UmbracoDocs/blob/main/17/umbraco-cms/get-started/installation/unattended-install.md
[unattended-settings]: https://github.com/umbraco/UmbracoDocs/blob/main/17/umbraco-cms/develop-with-umbraco/configuration/unattendedsettings.md
[global]: https://github.com/umbraco/UmbracoDocs/blob/main/17/umbraco-cms/develop-with-umbraco/configuration/globalsettings.md
[docker-docs]: https://github.com/umbraco/UmbracoDocs/blob/main/17/umbraco-cms/get-started/installation/running-umbraco-on-docker-locally.md
[tpl-db]: https://github.com/umbraco/Umbraco-CMS/blob/release-17.7.1/templates/UmbracoDockerCompose/Database/Dockerfile
[tpl-sql]: https://github.com/umbraco/Umbraco-CMS/blob/release-17.7.1/templates/UmbracoDockerCompose/Database/setup.sql
[tpl-startup]: https://github.com/umbraco/Umbraco-CMS/blob/release-17.7.1/templates/UmbracoDockerCompose/Database/startup.sh
[tpl-app]: https://github.com/umbraco/Umbraco-CMS/blob/release-17.7.1/templates/UmbracoProject/Dockerfile
[tpl-appsettings]: https://github.com/umbraco/Umbraco-CMS/blob/release-17.7.1/templates/UmbracoProject/appsettings.json
[tpl-appsettings-dev]: https://github.com/umbraco/Umbraco-CMS/blob/release-17.7.1/templates/UmbracoProject/appsettings.Development.json
[nuget-usync]: https://www.nuget.org/packages/uSync
[gh-usync]: https://github.com/KevinJump/uSync/releases/tag/v17.4.3
[gh-handlers]: https://github.com/KevinJump/uSync/tree/v17/main/uSync.BackOffice/SyncHandlers/Handlers
[catalog-core]: https://api.nuget.org/v3/catalog0/data/2026.09.30.12.51.09/usync.core.17.4.3.json
[usync-config]: https://docs.jumoo.co.uk/usync/uSync/reference/config/
[people]: https://docs.jumoo.co.uk/usync/complete/guides/People_Edition/people/
[complete]: https://jumoo.co.uk/usync/complete/
