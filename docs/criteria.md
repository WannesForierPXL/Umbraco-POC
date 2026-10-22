# Beoordelingscriteria CMS-keuze

Gedeeld tussen de Umbraco-POC en de Payload-POC. Dit bestand is **identiek in beide repo's**; wijzig het enkel in overleg.

De Klant kiest op advies van het team. Het advies steunt op de gewogen score hieronder, aangevuld met de demo (`docs/demoscript.md`).

## Spelregels

- **Timebox:** 2 werkdagen (±16 u) per POC. Stappen 0–8 vallen binnen de timebox, stap 9 (stretch) niet.
- **Tijd per stap** wordt bijgehouden in `docs/poc-verslag.md`. Een overschrijding is zelf een bevinding.
- **Zelfde opdracht:** beide POC's bouwen tegen `docs/contentmodel.md` (zelfde velden, blokken, DTO).
- **Scoren:** elk criterium krijgt 1–5 volgens de ankers hieronder, met een korte motivering en bewijs (screenshot, request, tijdmeting).
- **Gewogen totaal** = Σ (score × gewicht) / 5, als percentage.

## Schaal

| Score | Betekenis |
|---|---|
| 1 | Niet haalbaar of enkel met grote omweg |
| 2 | Haalbaar, maar omslachtig of foutgevoelig |
| 3 | Werkt zoals verwacht, niets bijzonders |
| 4 | Werkt vlot, kleine minpunten |
| 5 | Uitstekend, sterker dan nodig |

## Criteria

| # | Criterium | Gewicht |
|---|---|---|
| K1 | Redactie-ervaring (visueel) | 25 % |
| K2 | Redactieflow en rollen | 20 % |
| K3 | API en integratie | 15 % |
| K4 | Contentmodel en afscherming | 10 % |
| K5 | Model als code | 10 % |
| K6 | Opzet, hosting en beheer | 10 % |
| K7 | Events en webhooks | 5 % |
| K8 | Authenticatie backoffice (Entra) | 5 % |

K1 en K2 wegen samen het zwaarst (45 %): Editors zijn experts, geen developers, en zij moeten de Guide zelf aanvullen.

### K1 – Redactie-ervaring (visueel)

Hoe ziet de backoffice eruit en voelt hij aan voor een Editor?

- Is de interface overzichtelijk en modern? Vindt een nieuwe Editor zonder uitleg de Guide-boom?
- Hoe voelt het bewerken van blokken (tekst, afbeelding, callout): toevoegen, herschikken, voorvertoning van een blok?
- Kan een Editor zien hoe de Guide-stap er ongeveer uit zal zien (preview)?
- Hoe werkt NL/EN naast elkaar: zie je in één oogopslag wat nog niet vertaald is?
- Is `toegangsniveau` duidelijk zichtbaar en moeilijk te vergeten?

**Meting:** een teamlid dat de POC niet bouwde, voert de redactietaken uit het demoscript uit. Noteer de tijd, het aantal keer vastlopen, en een score 1–5 van die persoon.

### K2 – Redactieflow en rollen

Hoe vlot loopt een Guide-stap van idee tot gepubliceerd, en wie mag wat?

- Aantal klikken/schermen van "nieuwe Guide-stap" tot gepubliceerd in NL en EN.
- Concept opslaan, later verderwerken, publiceren, depubliceren: is dat duidelijk?
- Versiegeschiedenis en terugdraaien.
- Rollen: een Editor bewerkt en publiceert, maar kan het contentmodel niet aanpassen; een Admin nodigt Editors uit.
- Hoeveel werk is het om die rollen in te stellen?

### K3 – API en integratie

Hoe goed past het CMS achter `ICmsClient` in de Content Service?

- API afschermen (geen publieke toegang, API-key).
- Blokken, media en voorouders ophalen in zo weinig mogelijk requests.
- Taalkeuze en terugval EN → NL.
- Hoeveel mappingcode is nodig naar de gedeelde DTO?

### K4 – Contentmodel en afscherming

- Laat het model de Guide-boom, de bloktypes en een Resource zonder omwegen toe?
- Kan `toegangsniveau` taalonafhankelijk zijn terwijl titel en blokken per taal verschillen?
- Lekt afgeschermde content ergens (bv. media-URL's)?

### K5 – Model als code

- Komt het volledige contentmodel als bestanden in de repo terecht?
- Is het overzetten naar een andere omgeving (dev → test → prod) betrouwbaar en reviewbaar in een PR?

### K6 – Opzet, hosting en beheer

- Tijd van lege map tot draaiend CMS met database.
- Past het in Azure Container Apps met een beheerde database en Blob Storage voor media?
- Licentie- en hostingkosten, updatebeleid (LTS), community en documentatie.

### K7 – Events en webhooks

- Webhook bij publiceren en depubliceren, met een gedeeld secret.
- Bruikbaarheid van de payload (id, type, taal) om later een event op Service Bus te maken.
- Retry en logging.

### K8 – Authenticatie backoffice (Entra)

- Hoeveel werk is login via Microsoft Entra ID voor de backoffice?
- Mapping van Entra-groepen naar Editor/Admin.

Stretch: wordt enkel gescoord als stap 9 gehaald is, anders op basis van documentatie (en dat vermelden).

## Scoretabel

| # | Gewicht | Umbraco | Payload | Motivering |
|---|---|---|---|---|
| K1 | 25 % | 3 (voorlopig) | | zie poc-verslag |
| K2 | 20 % | 4 | | |
| K3 | 15 % | 3 | | |
| K4 | 10 % | 3 | | |
| K5 | 10 % | 3 | | |
| K6 | 10 % | 4 | | |
| K7 | 5 % | 2 | | |
| K8 | 5 % | 3 (op papier) | | |
| **Totaal** | 100 % | **65 %** | | |
