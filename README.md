# LLASER – CMS POC: Umbraco

Proof of concept voor de CMS-keuze van LLASER (PXL IT Project 2026-27), naast een Payload-POC.
Umbraco 17 (LTS) als headless CMS, met een kleine Content Service (`Llaser.ContentStub`) en een demopagina in de LLASER-look.

- Bevindingen, scores en conclusie: [docs/poc-verslag.md](docs/poc-verslag.md)
- Demo voor de Klant: [docs/demoscript.md](docs/demoscript.md)
- Contentmodel en API (gedeeld met Payload): [docs/contentmodel.md](docs/contentmodel.md)
- Beoordelingscriteria: [docs/criteria.md](docs/criteria.md)
- Werkwijze en beslissingen: [CLAUDE.md](CLAUDE.md) en de [Wayfinder-map](https://github.com/WannesForierPXL/Umbraco-POC/issues/1)

## Lokaal opzetten

Geen setup of secrets nodig: alle lokale instellingen staan in de repo. Reken op ±10 minuten, vooral downloads.

### 1. Installeer de vereisten

| Wat | Versie | Controle |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | elke 10.0.x | `dotnet --list-sdks` |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | recent, en **gestart** | `docker version` |
| Git | – | `git --version` |

Vertrouw daarna eenmalig het HTTPS-ontwikkelcertificaat van .NET (de backoffice-login werkt enkel via HTTPS):

```
dotnet dev-certs https --trust
```

### 2. Haal de repo op en start de database

```
git clone https://github.com/WannesForierPXL/Umbraco-POC.git
cd Umbraco-POC
docker compose up -d
```

Wacht tot `docker ps` voor `llaser-sql` **(healthy)** toont (±30 s).

### 3. Start Umbraco (terminal 1)

```
dotnet run --project src/Llaser.Cms --launch-profile Umbraco.Web.UI
```

De **eerste** start maakt de database aan, installeert Umbraco en importeert het contentmodel en de voorbeeldcontent (±30 s). Klaar bij `Now listening on: https://localhost:44310`. Laat dit venster open.

### 4. Start de Content Stub (terminal 2)

```
dotnet run --project src/Llaser.ContentStub --launch-profile https
```

Klaar bij `Now listening on: https://localhost:5001`.

### 5. Openen en inloggen

| Wat | URL | Login |
|---|---|---|
| **Demopagina** (wat de bezoeker ziet) | https://localhost:5001 | rechtsboven **Sign in** = gemockte login als Lid |
| **Backoffice** (waar de Editor werkt) | https://localhost:44310/umbraco | e-mail **`admin@llaser.test`**, wachtwoord **`LlaserPoc2026!`** |

In de backoffice:

1. Kies boven de contentboom de taal **Dutch (Belgium)**; nieuwe items openen dan in het Nederlands.
2. Open **Content → Guides → Veilig LLM's integreren** en bewerk een stap.
3. **Save and preview** opent de demopagina met je niet-gepubliceerde wijziging; **Publish** zet ze online (herlaad de demopagina).

De flow voor de Klant staat in [docs/demoscript.md](docs/demoscript.md).

> **Lokale demowaarden.** Wachtwoorden en sleutels (admin-login, SQL Server, API-key, webhook-secret) staan bewust vast in `appsettings.Development.json` en `docker-compose.yml`, zodat de POC zonder setup start. Dat kan enkel omdat alles lokaal draait. Gebruik ze nooit voor een gedeelde of online omgeving.

## Een Editor-account (optioneel)

Gebruikers zitten niet in git (gratis uSync bewaart ze niet). Zo maak je een Editor:

1. Backoffice → **Users** → **Create** → **User**: e-mail bv. `editor@llaser.test`, groep **Editors**, kies **Invite**.
2. De uitnodigingsmail wordt niet verstuurd maar als `.eml` bewaard in `src/Llaser.Cms/umbraco/Mail/`. Open die en klik de link (in een privévenster).
3. Kies een wachtwoord. Een Editor kan bewerken en publiceren, maar ziet geen **Settings** en **Users**.

## Testen

```
dotnet dev-certs https -ep devcert.pem --format PEM --no-password
NODE_EXTRA_CA_CERTS=devcert.pem node tests/stub-contract.mjs
```

20 controles op de regels uit de spec (Teaser, strengste wint, taalterugval, media-proxy). Vereist [Node.js](https://nodejs.org) 20+, en Umbraco en de stub moeten draaien. Requests om met de hand te proberen staan in `http/poc.http`.

## Problemen

| Symptoom | Oorzaak en oplossing |
|---|---|
| Umbraco stopt meteen met "LLASER: lokale instellingen ontbreken" | `appsettings.Development.json` is gewijzigd of oud: `git pull`, of herstel het met `git checkout src/Llaser.Cms/appsettings.Development.json`. |
| Crash met `If any of the UnattendedUserName, UnattendedUserEmail, UnattendedUserPassword are set, all of them are required` | Overblijfsel van het vroegere setupscript. Wis de oude user-secrets: `dotnet user-secrets clear --project src/Llaser.Cms` en `dotnet user-secrets clear --project src/Llaser.ContentStub`, dan opnieuw starten. |
| Installatiewizard in de browser in plaats van de backoffice | Zelfde oorzaak als hierboven: oude user-secrets wissen. |
| Inloggen met `LlaserPoc2026!` lukt niet | De database werd aangemaakt met een ouder wachtwoord. Begin opnieuw met een lege database (zie onderaan). |
| `Cannot open database` / `Login failed for user 'sa'` | De database draait niet (`docker compose up -d`), of het volume werd ooit met een ander wachtwoord aangemaakt: `docker compose down -v` (wist de database) en opnieuw `docker compose up -d`. |
| Login geeft `This server only accepts HTTPS requests` | Gebruik `https://localhost:44310`, niet `http://…:15019`. |
| Certificaatwaarschuwing in de browser | `dotnet dev-certs https --trust` en de browser herstarten. |
| Demopagina toont "Nog geen gepubliceerde Guides" | Umbraco draait niet, of de index van de Delivery API is leeg: backoffice → **Settings → Examine Management → DeliveryApiContentIndex → Rebuild**. |
| `global.json`: "A compatible .NET SDK was not found" | Installeer een .NET 10 SDK; elke 10.0.x-versie werkt. |
| Build faalt met "file is locked by Llaser.Cms" | Een vorige `dotnet run` draait nog. Stop die met Ctrl+C. |
| Poort 1433, 44310 of 5001 is bezet | Een andere SQL Server of app gebruikt die poort; stop die eerst. |

Opnieuw beginnen met een lege database: `docker compose down -v`, daarna stap 4 en 5. Model en voorbeeldcontent komen terug uit uSync; zelf gemaakte content en gebruikers niet.
