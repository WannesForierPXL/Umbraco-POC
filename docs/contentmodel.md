# Gedeelde contentmodel-spec en DTO

Gedeeld tussen de Umbraco-POC en de Payload-POC. Beide bouwen **exact dit model** en dezelfde stub-API, zodat de vergelijking eerlijk is en `ICmsClient` uitwisselbaar blijft. Termen: zie [GLOSSARY.md](../GLOSSARY.md).

## Talen

- `nl` (standaard) en `en`.
- Ontbreekt een item in de gevraagde taal, dan geeft de stub de `nl`-versie terug (zie [DTO](#dto)).
- Bestaat een item **enkel** in `en`, dan verschijnt het in de `en`-weergave op zijn plaats in de boom, en in de `nl`-weergave niet (er is niets om op terug te vallen).

## Contenttypes

Volgorde van Hoofdstukken en Guide-stappen = volgorde in de contentboom (geen apart veld).

### Guide (meerdere mogelijk)

Elke Guide staat in de root van de contentboom; volgorde van de Guides = volgorde in de boom.

| Veld | Type | Per taal | Verplicht |
|---|---|---|---|
| `titel` | tekst | ja | ja |
| `intro` | tekst (meerdere regels) | ja | nee |

Kinderen: Hoofdstukken.

### Hoofdstuk

| Veld | Type | Per taal | Verplicht |
|---|---|---|---|
| `titel` | tekst | ja | ja |
| `beschrijving` | tekst (meerdere regels) | ja | nee |
| `toegangsniveau` | keuze `publiek` / `leden` | **nee** | ja, standaard `publiek` |

Kinderen: Guide-stappen.

### Guide-stap

| Veld | Type | Per taal | Verplicht |
|---|---|---|---|
| `titel` | tekst | ja | ja |
| `samenvatting` | tekst (1–2 zinnen) | ja | ja |
| `blokken` | lijst van Blokken (zie hieronder) | ja | ja, minstens 1 |
| `toegangsniveau` | keuze `publiek` / `leden` | **nee** | ja, standaard `publiek` |

### Blokken

| Blok | Velden | Per taal |
|---|---|---|
| **Tekst** | `tekst`: opgemaakte tekst (koppen, lijsten, links) | ja |
| **Afbeelding** | `afbeelding`: media-item (verplicht), `alt`: tekst (verplicht), `bijschrift`: tekst (optioneel) | `alt` en `bijschrift` ja, het beeld zelf nee |
| **Callout** | `soort`: `tip` / `waarschuwing` / `info`, `tekst`: korte tekst | `tekst` ja |

### Resource

Alle Resources staan in één map **Resource library**.

| Veld | Type | Per taal | Verplicht |
|---|---|---|---|
| `titel` | tekst | ja | ja |
| `beschrijving` | tekst (meerdere regels) | ja | nee |
| `soort` | keuze `document` / `link` | nee | ja |
| `bestand` | media-item (pdf e.d.) | nee | enkel bij `document` |
| `url` | url | nee | enkel bij `link` |
| `toegangsniveau` | keuze `publiek` / `leden` | **nee** | ja, standaard `publiek` |

## Regels

1. **Strengste wint.** Het effectieve toegangsniveau van een Guide-stap is het strengste van de stap en zijn Hoofdstuk. De stub past dit toe, niet het CMS.
2. **Teaser.** Een Bezoeker ziet afgeschermde items wél in lijsten (titel, samenvatting/beschrijving, `afgeschermd: true`), maar nooit blokken, bestand of url. Een afgeschermd Hoofdstuk verschijnt met zijn stappen als Teaser.
3. **Concept** (niet gepubliceerd) komt nooit uit de stub; dat is geen toegangsniveau.
4. **Media via de stub.** Media-URL's in de DTO wijzen naar `/media/{id}` op de stub, nooit rechtstreeks naar het CMS. De stub controleert het toegangsniveau van het item waar de media bij hoort.

## Stub-API (`Llaser.ContentStub`)

| Methode | Pad | Wat |
|---|---|---|
| `GET` | `/guides` | Alle Guides (id, titel, intro) |
| `GET` | `/guides/{id}` | Eén Guide met Hoofdstukken en stappen als samenvatting (geen blokken) |
| `GET` | `/guide/stappen/{id}` | Eén Guide-stap met blokken; `403` met enkel teaservelden als afgeschermd |
| `GET` | `/resources` | Alle Resources (teaser als afgeschermd) |
| `GET` | `/resources/{id}` | Eén Resource; `403` met enkel teaservelden als afgeschermd |
| `GET` | `/media/{id}` | Media-proxy (stretch) |
| `POST` | `/webhooks/cms` | Webhook van het CMS (gedeeld secret in header) |

- **Taal:** query `?taal=nl|en`, standaard `nl`.
- **Mock-login:** header `X-Mock-Lid: true` = Lid; zonder header = Bezoeker. In productie vervangen door Entra-claims via de BFF.

## DTO

- `id` is altijd een **string** (het id van het CMS).
- `toegangsniveau` is het **effectieve** niveau (na regel 1).
- `afgeschermd` is `true` als de huidige gebruiker het item niet mag zien.
- `taal` is de taal die effectief geleverd wordt; `gevraagdeTaal` wat gevraagd werd. Verschillen ze, dan is het item (nog) niet vertaald.
- Opgemaakte tekst is **HTML**.

```jsonc
// GET /guides
[ { "id": "…", "titel": "Veilig LLM's integreren", "intro": "…", "taal": "nl" } ]

// GET /guides/{id}?taal=en  (Bezoeker)
{
  "id": "…", "titel": "…", "intro": "…", "taal": "en", "gevraagdeTaal": "en",
  "hoofdstukken": [
    {
      "id": "…", "titel": "Basics", "beschrijving": "…",
      "toegangsniveau": "publiek", "afgeschermd": false, "taal": "en",
      "stappen": [
        { "id": "…", "titel": "…", "samenvatting": "…", "toegangsniveau": "publiek", "afgeschermd": false, "taal": "en" },
        { "id": "…", "titel": "…", "samenvatting": "…", "toegangsniveau": "leden",   "afgeschermd": true,  "taal": "en" },
        { "id": "…", "titel": "Logging van prompts", "samenvatting": "…", "toegangsniveau": "publiek", "afgeschermd": false, "taal": "nl" }
      ]
    }
  ]
}

// GET /guide/stappen/{id}
{
  "id": "…", "titel": "…", "samenvatting": "…",
  "toegangsniveau": "publiek", "afgeschermd": false,
  "taal": "nl", "gevraagdeTaal": "nl",
  "guide": { "id": "…", "titel": "Veilig LLM's integreren" },
  "hoofdstuk": { "id": "…", "titel": "Basis" },
  "blokken": [
    { "type": "tekst",      "html": "<p>…</p>" },
    { "type": "afbeelding", "url": "/media/{id}", "alt": "…", "bijschrift": "…" },
    { "type": "callout",    "soort": "waarschuwing", "tekst": "…" }
  ]
}

// GET /resources/{id}
{
  "id": "…", "titel": "…", "beschrijving": "…", "soort": "document",
  "toegangsniveau": "leden", "afgeschermd": false,
  "bestandUrl": "/media/{id}", "url": null,
  "taal": "nl", "gevraagdeTaal": "nl"
}
```

## Voorbeeldpad

Eén voorbeeld-Guide met content, zo gekozen dat elke regel in de demo zichtbaar wordt. De content gaat mee in de repo (Umbraco: uSync-content; Payload: seed-script).

**Guide** "Veilig LLM's integreren" / "Integrating LLMs securely"

| Item | Toegangsniveau | Talen | Test |
|---|---|---|---|
| Hoofdstuk "Basis" | publiek | nl, en | – |
| ↳ Stap "Behandel modeloutput als onbetrouwbaar" | publiek | nl, en | gewone weg; alle drie de bloktypes |
| ↳ Stap "Red-teamresultaten van ons model" | leden | nl, en | Teaser voor Bezoeker |
| ↳ Stap "Logging van prompts" | publiek | **enkel nl** | terugval EN → NL |
| Hoofdstuk "Gevoelige data" | leden | nl, en | afgeschermd Hoofdstuk |
| ↳ Stap "Geen klantdata in prompts" | publiek (zelf) | nl, en | strengste wint → leden |

**Resource library**

| Resource | Soort | Toegangsniveau | Test |
|---|---|---|---|
| "OWASP Top 10 for LLM Applications" | link | publiek | – |
| "Interne checklist LLM-review" | document (pdf) | leden | Teaser + media-proxy |

## Bewust niet in het model

Tags, auteur, datums, slugs/URL's, per taal een ander Resource-bestand, nieuws. Kandidaatvragen voor later, niet voor de vergelijking.
