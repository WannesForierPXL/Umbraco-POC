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
- **Tijd:**
- **Wat gedaan:**
- **Wat werkte:**
- **Wat niet:**

### Stap 2 – uSync

- **Criterium:** K5
- **Tijd:**
- **Wat gedaan:**
- **Wat werkte:**
- **Wat niet:**

### Stap 3 – Contentmodel (Guide-boom, bloktypes, Resource, NL/EN, toegangsniveau)

- **Criterium:** K1, K4
- **Tijd:**
- **Wat gedaan:**
- **Wat werkte:**
- **Wat niet:**

### Stap 4 – User groups Editor en Admin

- **Criterium:** K2
- **Tijd:**
- **Wat gedaan:**
- **Wat werkte:**
- **Wat niet:**

### Stap 5 – Delivery API dicht met API-key

- **Criterium:** K3
- **Tijd:**
- **Wat gedaan:**
- **Wat werkte:**
- **Wat niet:**

### Stap 6 – `Llaser.ContentStub`

- **Criterium:** K3, K4
- **Tijd:**
- **Wat gedaan:**
- **Wat werkte:**
- **Wat niet:**

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
