# Demoscript voor de Klant

Gedeeld tussen de Umbraco-POC en de Payload-POC. Beide demo's volgen **exact deze flow**, met het voorbeeldpad uit [contentmodel.md](contentmodel.md), zodat de Klant hetzelfde ziet en enkel het CMS verschilt. Duur: ±10 min per CMS.

## Opstelling

Twee vensters naast elkaar:

| Links | Rechts |
|---|---|
| Backoffice van het CMS (aangemeld als Editor) | Demopagina van de stub (`https://localhost:5001`) |

De demopagina is CMS-onafhankelijk: ze praat enkel met de stub-API. Bij Payload toon je **dezelfde pagina**.

Vooraf (Umbraco):

```
docker compose up -d
dotnet run --project src/Llaser.Cms --launch-profile Umbraco.Web.UI
dotnet run --project src/Llaser.ContentStub --launch-profile https
```

## Flow

Elke stap noemt wat je toont en welk criterium het raakt.

### 1. Wat de website ziet (1 min) · K4

Op de demopagina (opent met het Guide-overzicht; klik "Veilig LLM's integreren"), als **Bezoeker**, in **NL**:

- De Guide met twee Hoofdstukken. "Red-teamresultaten" en het hele Hoofdstuk "Gevoelige data" tonen een slotje.
- Klik "Red-teamresultaten": enkel titel en samenvatting (Teaser). De inhoud komt niet eens mee uit de stub.
- Klik "Geen klantdata in prompts": staat zelf op *publiek*, maar het Hoofdstuk is *leden* → toch afgeschermd (strengste wint).
- Klik **Sign in** (gemockte login: je bent dan Lid): alles opent, ook de PDF onder **Resources**.

### 2. Taal (1 min) · K1, K4

- Zet **EN**. Titels, samenvattingen en blokken zijn vertaald; het beeld is hetzelfde.
- "Logging van prompts" bestaat enkel in het Nederlands en toont het label **NL** ("nog niet vertaald") in plaats van te verdwijnen.

### 3. Een Editor voegt een Guide-stap toe (4 min) · K1, K2

In de backoffice, als **Editor**:

1. Open Hoofdstuk "Basis" → nieuwe **Guide-stap** "Gebruik least privilege voor tools".
2. Samenvatting invullen, `toegangsniveau` = **publiek**.
3. Blokken toevoegen: een **Tekst**-blok, een **Callout** (tip), een **Afbeelding** (bestaand beeld kiezen, alt-tekst invullen).
4. Wissel naar **EN**: vertaal titel, samenvatting en de tekst in de blokken. De blokken zelf en het beeld blijven staan.
5. **Publiceren** in beide talen.

Toon:

- Op de demopagina verschijnt de stap (na herladen), in NL en EN.
- In het paneel **Webhook-events** verschijnt `ContentPublished [en-US, nl-BE]`.

### 4. Rollen (1 min) · K2

- Als Editor: geen **Settings** en geen **Users** in de backoffice. Het contentmodel aanpassen kan niet.
- Als Admin: Settings en Users zijn er; een Editor uitnodigen kan van daaruit.

### 5. Depubliceren (1 min) · K7

- Haal de nieuwe stap offline. Op de demopagina verdwijnt hij; in het webhook-paneel komt `ContentUnpublished`.
- Haal enkel de EN-versie van een andere stap offline: die valt op de demopagina terug op NL. Kijk wat het CMS daarbij als webhook stuurt (bij Umbraco: een `ContentPublished` met enkel `nl-BE`).

### 6. Model als code (1 min) · K5

Toon in de repo:

- Umbraco: `src/Llaser.Cms/uSync/v17/` (XML per type en per content-item).
- Payload: de collection-definities in TypeScript.
- Vertel wat een nieuwe omgeving nodig heeft om dezelfde site te krijgen (Umbraco: opstarten met lege database → uSync importeert alles; user groups handmatig).

## Meting K1 (aparte sessie, ±15 min)

Een teamlid dat de POC **niet** bouwde, voert stap 3 uit zonder hulp. Noteer per CMS:

| Meting | Umbraco | Payload |
|---|---|---|
| Tijd tot gepubliceerd in NL en EN | | |
| Aantal keer vastgelopen (vroeg hulp of zocht > 30 s) | | |
| Fouten (bv. verkeerde taal, vergeten toegangsniveau) | | |
| Eigen score 1–5 ("hoe vlot ging dit?") | | |
| Opmerkingen | | |

## Wat je de Klant níet toont

- JSON van de CMS-API's (de Klant ziet de stub via de demopagina; JSON staat in `http/poc.http` voor het team).
- Configuratie en secrets.
