# LLASER – CMS POC: Umbraco

Proof of concept voor de CMS-keuze van LLASER (PXL IT Project 2026-27), naast een Payload-POC.
Umbraco 17 (LTS) als headless CMS, met een kleine Content Service (`Llaser.ContentStub`) en een demopagina in de LLASER-look.


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

#### Op Linux

Getest op Ubuntu 24.04 (ook in WSL): alles werkt, met deze verschillen.

- **.NET:** installeer de SDK via je pakketbeheerder of met Microsofts script, zonder sudo in je eigen map:
  ```
  curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
  export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"
  ```
- **Docker:** Docker Engine met de `docker compose`-plugin volstaat; Docker Desktop is niet nodig.
- **`dotnet dev-certs https --trust`** vraagt op Linux je **sudo-wachtwoord** (om het certificaat ook aan je browser te geven). Typ het in. Voor Umbraco en de stub is het certificaat al vertrouwd vóór die vraag, dus lukt sudo niet, dan werkt de POC toch; je browser toont dan enkel een certificaatwaarschuwing die je kunt aanvaarden.
- **WSL:** open je de site in een browser op Windows, dan kent Windows het certificaat van WSL niet: aanvaard de waarschuwing, of draai alles in Windows zelf.
- **ARM (bv. Raspberry Pi, sommige cloud-VM's):** werkt niet, de SQL Server-image bestaat enkel voor x86_64.

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

## Een nieuwe Guide maken

Zo bouw je in de backoffice een eigen Guide op, van lege map tot een stap die op de demopagina staat. Een Guide bestaat uit **Hoofdstukken**, en elk Hoofdstuk uit **Guide-stappen**; een stap is opgebouwd uit **blokken** (tekst, afbeelding, callout).

```
Guides
└ Guide                 ← titel + intro
  └ Hoofdstuk           ← titel + beschrijving + toegangsniveau
    └ Guide-stap        ← titel + samenvatting + blokken + toegangsniveau
```

### Voor je begint

- Kies boven de contentboom de taal **Dutch (Belgium)**. Nederlands is verplicht, Engels is een optionele vertaling. Zolang een item geen Engelse versie heeft, ziet een Engelstalige bezoeker de Nederlandse versie met het label "Nog niet vertaald".
- Wissel voor het vertalen van taal **in het item zelf** (de taalkeuze bovenaan, naast de naam), niet boven de boom. Zo openen nieuwe items in het Nederlands.

### 1. De Guide

1. Ga naar **Content**, beweeg over **Guides** en klik op **…** → **Create** → **Guide**.
2. Vul de **naam** in (dat is de titel op de site) en eventueel een **intro**.
3. Klik **Save**. Publiceren mag nu of later, samen met de rest (zie stap 4).

### 2. Een Hoofdstuk

1. Beweeg over je nieuwe Guide → **…** → **Create** → **Hoofdstuk**.
2. Vul de **naam** en eventueel een **beschrijving** in.
3. Kies het **toegangsniveau**:
   - **publiek**: iedereen ziet het;
   - **leden**: enkel aangemelde leden. Dit geldt voor **alle** stappen in het hoofdstuk, ook als een stap zelf op publiek staat.
4. Klik **Save**.

### 3. Een Guide-stap

1. Beweeg over het Hoofdstuk → **…** → **Create** → **Guide-stap**.
2. Vul de **naam** en de **samenvatting** in (1 à 2 zinnen; die zien ook bezoekers die de stap niet mogen openen).
3. Voeg bij **Blokken** minstens één blok toe met **Add content**:
   - **Blok tekst**: opgemaakte tekst. Gebruik kopjes (Heading 2) voor de secties; die verschijnen rechts op de site onder "Op deze pagina".
   - **Blok afbeelding**: kies een afbeelding uit Media (of upload er een), vul een **alt-tekst** in, en eventueel een bijschrift.
   - **Blok callout**: een kort kader, met als soort **tip**, **waarschuwing** of **info**.

   Sleep blokken om de volgorde te wijzigen.
4. Kies het **toegangsniveau** (publiek of leden).
5. Klik **Save and preview** om de stap te zien zoals hij op de site zal staan. De demopagina opent met een gele balk "Voorvertoning"; alleen jij ziet dit.

Afbeeldingen die enkel voor leden bedoeld zijn, zet je in een **Blok afbeelding** en niet in de tekst: alleen dan schermt de site ze af.

### 4. Publiceren

Een item kan pas online als alles erboven online staat. Publiceer daarom van boven naar beneden, of alles in één keer:

1. Beweeg over de **Guide** → **…** → **Publish** (of open de Guide en kies bij de knop **Publish** het pijltje → **Publish with descendants**).
2. Vink **Include unpublished content items** aan, zodat ook de nieuwe hoofdstukken en stappen mee gaan.
3. Kies de talen: **Dutch** altijd; **English** enkel als je al vertaald hebt.
4. Herlaad de demopagina (https://localhost:5001). Je Guide staat in het overzicht; klik erop om hem te openen.

Probeer je een stap te publiceren terwijl zijn Hoofdstuk of Guide nog een concept is, dan zegt Umbraco welk item eerst gepubliceerd moet worden.

### 5. Vertalen naar het Engels (optioneel)

1. Open het item en kies bovenaan, naast de naam, **English (United States)**.
2. Vul de naam, de samenvatting en de tekst in de blokken in. De blokken zelf, de afbeeldingen en het toegangsniveau gelden voor beide talen; je vertaalt enkel de tekst.
3. Klik **Publish** en vink **English** aan.
4. Zet de demopagina op **EN**: het label "Nog niet vertaald" is weg.

### Wijzigen, verplaatsen, verwijderen

- **Wijzigen:** open het item, pas aan, en kies **Publish**. Zonder publiceren blijft de oude versie online; met **Save and preview** zie je de nieuwe eerst.
- **Volgorde:** beweeg over de ouder → **…** → **Sort**, en sleep. De site volgt de volgorde van de boom.
- **Offline halen:** open het item → pijltje naast **Publish** → **Unpublish**. Verwijderen gaat via **…** → **Delete** (het item gaat naar de prullenbak).

### Mag je nieuwe Guide mee in git?

uSync schrijft elk item ook als bestand weg in `src/Llaser.Cms/uSync/v17/Content/` (en afbeeldingen in `Media/` en `wwwroot/media/`). Commit je die bestanden, dan krijgt elke nieuwe omgeving je Guide mee bij de eerste start. Commit je ze niet, dan blijft hij enkel in je eigen database.

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
