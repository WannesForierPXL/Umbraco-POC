# LLASER – CMS POC: Umbraco

Proof of concept voor de CMS-keuze van LLASER (PXL IT Project 2026-27), naast een Payload-POC.
Umbraco 17 (LTS) als headless CMS, met een kleine Content Service (`Llaser.ContentStub`) en een demopagina in de LLASER-look.

- Bevindingen, scores en conclusie: [docs/poc-verslag.md](docs/poc-verslag.md)
- Demo voor de Klant: [docs/demoscript.md](docs/demoscript.md)
- Contentmodel en API (gedeeld met Payload): [docs/contentmodel.md](docs/contentmodel.md)
- Beoordelingscriteria: [docs/criteria.md](docs/criteria.md)
- Werkwijze en beslissingen: [CLAUDE.md](CLAUDE.md) en de [Wayfinder-map](https://github.com/WannesForierPXL/Umbraco-POC/issues/1)

## Lokaal opzetten

Reken op ±15 minuten, waarvan de meeste tijd naar downloads gaat.

### 1. Installeer de vereisten

| Wat | Versie | Controle |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | 10.0.x | `dotnet --list-sdks` |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | recent, en **gestart** | `docker version` |
| PowerShell | Windows PowerShell 5.1 (standaard op Windows) of PowerShell 7 | op macOS/Linux: [PowerShell 7 installeren](https://learn.microsoft.com/powershell/scripting/install/installing-powershell) |
| Git | – | `git --version` |

Vertrouw daarna eenmalig het HTTPS-ontwikkelcertificaat van .NET (de backoffice-login werkt enkel via HTTPS):

```
dotnet dev-certs https --trust
```

### 2. Haal de repo op

```
git clone https://github.com/WannesForierPXL/Umbraco-POC.git
cd Umbraco-POC
```

### 3. Stel de secrets in

Windows:

```
powershell -ExecutionPolicy Bypass -File .\scripts\setup.ps1
```

macOS/Linux (of Windows met PowerShell 7):

```
pwsh ./scripts/setup.ps1
```

Het script maakt een `.env` aan met een SQL Server-wachtwoord en zet de sleutels (database, API-key, webhook-secret) in `dotnet user-secrets`. Het eindigt met **"alle secrets aanwezig"**; zie je een rode fout, los die eerst op. Niets hiervan komt in git.

### 4. Start de database

```
docker compose up -d
```

Wacht tot `docker ps` voor `llaser-sql` **(healthy)** toont (±30 s).

### 5. Start Umbraco

```
dotnet run --project src/Llaser.Cms --launch-profile Umbraco.Web.UI
```

Bij de **eerste** start gebeurt automatisch:

1. Umbraco maakt de database `LlaserCms` aan en installeert zichzelf (geen wizard).
2. uSync importeert het contentmodel én de voorbeeldcontent uit `src/Llaser.Cms/uSync/v17/` (±15 s).

Klaar zodra je `Now listening on: https://localhost:44310` ziet. Laat dit venster open.

### 6. Start de Content Stub (tweede terminal)

```
dotnet run --project src/Llaser.ContentStub --launch-profile https
```

Klaar bij `Now listening on: https://localhost:5001`.

### 7. Openen en inloggen

| Wat | URL | Login |
|---|---|---|
| **Demopagina** (wat de bezoeker ziet) | https://localhost:5001 | rechtsboven **Sign in** = gemockte login als Lid |
| **Backoffice** (waar de Editor werkt) | https://localhost:44310/umbraco | e-mail **`admin@llaser.test`**, wachtwoord **`LlaserPoc2026!`** |

> De admin-login staat bewust vast in `appsettings.Development.json`, zodat testers meteen kunnen inloggen. Dat kan enkel omdat het een **lokale demodatabase** is: gebruik deze gegevens nooit voor een gedeelde of online omgeving.

In de backoffice:

1. Kies boven de contentboom de taal **Dutch (Belgium)**; nieuwe items openen dan in het Nederlands.
2. Open **Content → Guides → Veilig LLM's integreren** en bewerk een stap.
3. **Save and preview** opent de demopagina met je niet-gepubliceerde wijziging; **Publish** zet ze online (herlaad de demopagina).

De flow voor de Klant staat in [docs/demoscript.md](docs/demoscript.md).

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
| Umbraco stopt meteen met "LLASER: de lokale secrets ontbreken" | Stap 3 is niet (volledig) gelukt. Voer het setupscript opnieuw uit en start Umbraco opnieuw. |
| Oudere versie: crash met `If any of the UnattendedUserName, UnattendedUserEmail, UnattendedUserPassword are set, all of them are required` | Zelfde oorzaak: haal de laatste versie op (`git pull`), voer het setupscript uit en start opnieuw. |
| Installatiewizard in de browser in plaats van de backoffice | De secrets ontbreken. Voer het setupscript (opnieuw) uit en herstart Umbraco. |
| Inloggen met `LlaserPoc2026!` lukt niet | De database werd aangemaakt met een ouder wachtwoord. Begin opnieuw met een lege database (zie onderaan). |
| `Cannot open database` / `Login failed for user 'sa'` | De database draait niet of `.env` hoort bij een ander volume. `docker compose up -d`; na een nieuw wachtwoord: `docker compose down -v` (wist de database) en opnieuw. |
| Login geeft `This server only accepts HTTPS requests` | Gebruik `https://localhost:44310`, niet `http://…:15019`. |
| Certificaatwaarschuwing in de browser | `dotnet dev-certs https --trust` en de browser herstarten. |
| Demopagina toont "Nog geen gepubliceerde Guides" | Umbraco draait niet, of de index van de Delivery API is leeg: backoffice → **Settings → Examine Management → DeliveryApiContentIndex → Rebuild**. |
| `global.json`: "A compatible .NET SDK was not found" | Installeer een .NET 10 SDK; elke 10.0.x-versie werkt. |
| Build faalt met "file is locked by Llaser.Cms" | Een vorige `dotnet run` draait nog. Stop die met Ctrl+C. |
| Poort 1433, 44310 of 5001 is bezet | Een andere SQL Server of app gebruikt die poort; stop die eerst. |

Opnieuw beginnen met een lege database: `docker compose down -v`, daarna stap 4 en 5. Model en voorbeeldcontent komen terug uit uSync; zelf gemaakte content en gebruikers niet.
