# Umbraco-webhooks bij publiceren en depubliceren

Onderzoek voor issue #4. Datum: 2026-10-22.

**Versie.** De huidige LTS is **Umbraco 17** (.NET 10). Laatste stabiele release is 17.7.1 (17.8.0-rc loopt). Daarnaast bestaat **Umbraco 18** (STS, laatste release 18.2.1). Alle broncode-verwijzingen hieronder gaan over tag `release-17.7.1`, tenzij anders vermeld.
Bron: `gh release list -R umbraco/Umbraco-CMS`.

Afkortingen voor de bronlinks:
- `[src]` = `https://github.com/umbraco/Umbraco-CMS/blob/release-17.7.1/src/`
- `[docs]` = https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/headless-and-apis/webhooks

---

## TL;DR

- De webhooks die standaard beschikbaar zijn: **Content Published / Unpublished / Deleted** en **Media Saved / Deleted**. De aliassen zijn `Umbraco.ContentPublish`, `Umbraco.ContentUnpublish`, `Umbraco.ContentDelete`, `Umbraco.MediaSave` en `Umbraco.MediaDelete`. Let op: de alias is `ContentPublish`, niet `ContentPublished`. Ongeveer 70 andere events bestaan wel, maar je moet ze zelf registreren via een composer.
- Het payloadtype stel je in met `Umbraco:CMS:Webhook:PayloadType`. In v17 is de standaard nog **Legacy**; vanaf v18 is dat **Minimal**. **Kies `Minimal` expliciet.** Bij publish krijg je dan `{ id, cultures:[{culture,date}] }`, bij unpublish alleen `{ id }`.
- **Een unpublish van één cultuur (bv. alleen EN) stuurt géén Unpublished-webhook.** Je krijgt een **Published**-webhook. Unpublished komt pas wanneer het hele document offline gaat. Verwijderen naar de prullenbak stuurt evenmin een Unpublished.
- Je kunt per webhook custom headers instellen, dus een shared secret in een header werkt. **Er is geen ingebouwde HMAC-signing.** Het secret staat in plaintext in de database, in de webhook-logs en (met uSync) in de XML-bestanden.
- Levering gaat via een DB-wachtrij die elke `Period` (standaard 10 s) wordt verwerkt. Verzoeken gaan **parallel** en er komt **geen event-id** mee. Retries gebeuren met een vaste interval (standaard 5 retries, dus maximaal 6 pogingen) en daarna wordt het verzoek weggegooid. Je krijgt dus at-least-once binnen dat budget, **zonder ordering-garantie**.
- **De gratis uSync synchroniseert webhooks** (`WebhookHandler` in uSync.BackOffice), inclusief headers. Daarvoor is uSync.Complete niet nodig.

---

## 1. Ingebouwde webhook-events en aliassen

### Standaard geregistreerd

Umbraco registreert bij opstart `builder.WebhookEvents().AddCms(true, payloadType)`. `onlyDefault: true` betekent dat alleen de vijf standaardevents in de collectie zitten.
Bron: `[src]Umbraco.Core/DependencyInjection/UmbracoBuilder.Collections.cs` r. 103-114; `[src]Umbraco.Core/Webhooks/WebhookEventCollectionBuilderCmsExtensions.cs` (`_defaultTypes`).

| Naam in backoffice | Alias (header `Umb-Webhook-Event`) | Notification |
|---|---|---|
| Content Published | `Umbraco.ContentPublish` | `ContentPublishedNotification` |
| Content Unpublished | `Umbraco.ContentUnpublish` | `ContentUnpublishedNotification` |
| Content Deleted | `Umbraco.ContentDelete` | `ContentDeletedNotification` |
| Media Saved | `Umbraco.MediaSave` | `MediaSavedNotification` |
| Media Deleted | `Umbraco.MediaDelete` | `MediaDeletedNotification` |

Bron: `[src]Umbraco.Core/Constants-WebhookEvents.cs` (klasse `Aliases`); `[src]Umbraco.Core/Webhooks/Events/Content/ContentPublishedWebhookEvent.cs` e.a.

> Let op: de docs geven als voorbeeld `umb-webhook-event: Umbraco.ContentPublished` [docs, "Default Headers"]. De broncode stuurt echter de **alias** `Umbraco.ContentPublish` (`WebhookFiring.cs`, header = `request.EventAlias`). Laat de stub daarom op de code-waarde matchen.

### Beschikbaar maar niet standaard actief

De volgende events bestaan in `Constants.WebhookEvents.Aliases`. Je activeert ze met `builder.WebhookEvents().Clear().AddCms(false, WebhookPayloadType.Minimal)` of selectief met `.AddContent(...)`, `.AddMedia(...)` enzovoort [docs, "Adding Custom Events"]:

- **Content:** `Umbraco.ContentSaved`, `Umbraco.ContentMoved`, `Umbraco.ContentMovedToRecycleBin`, `Umbraco.ContentEmptiedRecycleBin`, `Umbraco.ContentCopied`, `Umbraco.ContentSorted`, `Umbraco.ContentRolledBack`, `Umbraco.ContentSavedBlueprint`, `Umbraco.ContentDeletedBlueprint`, `Umbraco.ContentDeletedVersions`.
- **Media:** `mediaMoved`, `mediaMovedToRecycleBin`, `mediaEmptiedRecycleBin`.
- **Overige** (camelCase-aliassen, inconsistent met de `Umbraco.`-prefix): `documentTypeSaved/Changed/Moved/Deleted`, `mediaType*`, `memberType*`, `dataType*`, `dictionaryItem*`, `domainSaved/Deleted`, `languageSaved/Deleted`, `templateSaved/Deleted`, `partialView*`, `script*`, `stylesheet*`, `member*`, `memberGroup*`, `assignedMemberRoles`, `removedMemberRoles`, `exportedMember`, `user*` (login, wachtwoord, lock, ...), `UserGroupDeleted`, `userGroupSaved`, `relation*`, `relationType*`, `publicAccessEntry*`, `packageImported`, `healthCheckCompleted`.

Bron: `[src]Umbraco.Core/Constants-WebhookEvents.cs`; map `[src]Umbraco.Core/Webhooks/Events/`.

Per webhook kun je in de backoffice filteren op **content types** (`ContentTypeKeys`). Een lege lijst betekent alle types (`WebhookEventContentBase.ProcessWebhooks`) [docs, "Content Type Filtering"].

---

## 2. Payload bij publish en unpublish

### Payloadtypes

`WebhookPayloadType { Minimal = 0, Extended = 1, Legacy = 2 }`. In v17 is `Legacy` `[Obsolete("Scheduled for removal in Umbraco 18.")]`. In v18 is de tekst veranderd naar "removal in Umbraco 19", dus Legacy bestaat in v18 nog.
Bron: `[src]Umbraco.Core/Webhooks/WebhookPayloadType.cs`; idem op `release-18.2.1`.

| | v17 (LTS) | v18 |
|---|---|---|
| `Constants.Webhooks.DefaultPayloadType` | **Legacy** | **Minimal** |

Bron: `[src]Umbraco.Core/Constants-Webhooks.cs` op beide tags.

Configureer het payloadtype via appsettings (`Umbraco:CMS:Webhook:PayloadType`) of in code via `AddCms(..., payloadType)`. De appsetting wordt gelezen **voordat composers lopen**. Wie de `WebhookEventCollectionBuilder` zelf aanpast, moet het type daarom zelf meegeven [docs, "Configuring Payload Types"].

De JSON wordt geserialiseerd met System.Text.Json in **camelCase**, met enums als string (`SystemTextWebhookJsonSerializer.cs`). De request is altijd een `POST` met `Content-Type: application/json; charset=utf-8` (`WebhookFiring.SendRequestAsync`).

### Publish (`Umbraco.ContentPublish`)

| Type | Payload | Bron |
|---|---|---|
| **Minimal** | `{ "id": "<guid>", "cultures": [ { "culture": "nl-BE", "date": "..." }, ... ] }` | `Webhooks/Events/Content/ContentPublishedWebhookEvent.cs` |
| **Extended** | `{ id, contentType, name, createDate, updateDate, cultures: { "<culture>": { path, startItem, properties } } }`, opgebouwd via de Delivery API-builder | `Webhooks/ExtendedEvents/Content/ExtendedContentPublishedWebhookEvent.cs` |
| **Legacy** | Het volledige Delivery API-contentobject (`IApiContentBuilder.Build`). `null` als het item niet in de published cache staat | `Webhooks/LegacyEvents/Content/LegacyContentPublishedWebhookEvent.cs` |

Over `cultures` bij Minimal: dit is `entity.PublishCultureInfos`, dus **alle cultures die na de operatie gepubliceerd zijn**, niet alleen de cultuur die net gepubliceerd werd. Wie wil weten "wat veranderde er", moet zelf diffen of de Delivery API raadplegen. Voor invariante content verwacht ik een lege lijst. Dat is afgeleid uit de code; verifieer het in de POC.

### Unpublish (`Umbraco.ContentUnpublish`)

Bij alle drie de types is de payload **alleen het id**: `{ "id": "<guid>" }` (`DefaultPayloadModel`). Extended valt voor unpublish terug op het Minimal-event (`_extendedDefaultTypes` bevat `ContentUnpublishedWebhookEvent`). Er zit **geen cultuurinformatie** in, terwijl de `ContentUnpublishedNotification` die wel bevat.
Bron: `[src]Umbraco.Core/Webhooks/Events/Content/ContentUnpublishedWebhookEvent.cs`, `LegacyContentUnpublishedWebhookEvent.cs`.

### Meertalig (NL/EN): wanneer vuurt wat?

Uit `ContentService.CommitDocumentChangesInternal` (`[src]Umbraco.Core/Services/ContentService.cs` r. ~1775-2056):

| Actie van de editor | Notification, dus webhook |
|---|---|
| NL en/of EN publiceren | `ContentPublishedNotification`, dus **Published** (cultures = alle nu gepubliceerde) |
| **Alleen EN depubliceren, NL blijft online** | De operatie loopt via het publish-pad, dus **Published** met `cultures: [nl]`. **Geen Unpublished.** |
| Laatste cultuur of verplichte (mandatory) cultuur depubliceren | **Unpublished** `{id}` (het hele document gaat offline) |
| Hele document depubliceren | **Unpublished** `{id}` |
| Parent opnieuw publiceren waardoor gepubliceerde descendants weer zichtbaar worden | Extra **Published** per descendant (r. ~2053-2056) |
| Parent depubliceren | **Alleen Unpublished voor de parent.** Descendants blijven technisch "published" maar zijn gemaskeerd en krijgen geen eigen event. |
| Naar prullenbak verplaatsen | **Geen Unpublished.** Alleen `ContentMovedToRecycleBinNotification`, en het bijbehorende webhook-event staat niet standaard aan. De code zegt letterlijk: "trashing is equivalent to moving under an unpublished node so it's NOT unpublishing" (r. ~2845). |
| Definitief verwijderen van gepubliceerd, niet-getrasht item | Unpublished én Deleted (r. ~2686-2695) |
| Publish with descendants (branch) | Eén notification met alle documenten, dus **één HTTP-request per document** (`WebhookEventContentBase.ProcessWebhooks` loopt per entity) |

**Gevolg voor het ontwerp:** het ContentUnpublished-event naar Service Bus moet worden afgeleid. "Unpublished" plus "Published waarbij een cultuur verdwenen is" betekent in de praktijk "deze taal is offline". Voeg eventueel ook `Umbraco.ContentMovedToRecycleBin` toe. Voor depubliceren van een parent met kinderen moet de consumer zelf de subtree invalideren.

---

## 3. Beveiliging: custom headers, signing

- **Custom headers per webhook: ja.** Je stelt ze in via de backoffice of de Management API (`Webhook.Headers`, tabel `umbracoWebhook2Headers`). `WebhookFiring` voegt ze toe met `request.Headers.Add(key, value)` [docs, "Custom Headers"]; `[src]Umbraco.Infrastructure/BackgroundJobs/Jobs/DistributedJobs/WebhookFiring.cs`.
- **Standaardheaders:** `User-Agent: Umbraco-Cms/<versie>`, `Umb-Webhook-Event: <alias>`, `Umb-Webhook-RetryCount: <n>` [docs]; `Constants-WebhookEvents.cs` (`HeaderNames`).
- **Ingebouwde signing/HMAC: nee.** De verzendcode voegt alleen bovenstaande headers en de body toe. Er is geen signature, timestamp of delivery-id (`WebhookFiring.SendRequestAsync`).
- **Aandachtspunten:**
  - Het secret staat **plaintext** in `umbracoWebhook2Headers` (`Webhook2HeadersDto.cs`).
  - Elke poging wordt gelogd met **alle request headers** (`WebhookLogFactory.CreateAsync`: `RequestHeaders = ...requestMessage.Headers`). Het secret is dus zichtbaar in `umbracoWebhookLog` en in de backoffice-logs voor iedereen met toegang tot Settings.
  - Met uSync komt het secret in de XML in source control terecht (zie §5).
- **Alternatieven:**
  1. *POC:* een header `X-Webhook-Secret: <secret>` (of `Authorization: Bearer ...`). In de stub vergelijk je constant-time (`CryptographicOperations.FixedTimeEquals`). Dat is voldoende voor de POC.
  2. *HMAC zelf toevoegen:* vervang `IWebhookFiringService` niet; de HTTP-verzending zit in het internal `WebhookFiring`-job. De nette weg is een **DelegatingHandler op de named HttpClient** `Constants.HttpClients.WebhookFiring`, die body plus timestamp signeert met een secret uit configuratie of Key Vault (`builder.Services.AddHttpClient(Constants.HttpClients.WebhookFiring).AddHttpMessageHandler<HmacHandler>()`). Het client-registratiepunt staat in `[src]Umbraco.Web.Common/DependencyInjection/UmbracoBuilderExtensions.cs` r. ~255. Het secret staat dan niet in de DB of logs, maar de signature-header wordt wel gelogd. Dit is afgeleid uit de code en nog niet getest.
  3. *Netwerk:* IP-restrictie of private endpoint tussen Umbraco en de API, of mTLS.
  4. *Productie:* de webhook helemaal overslaan en in een eigen `INotificationAsyncHandler<ContentPublishedNotification>` direct naar de outbox schrijven (zie §6).

---

## 4. Retry, logs en configuratie

### Configuratie: `Umbraco:CMS:Webhook`

| Key | Default | Betekenis |
|---|---|---|
| `Enabled` | `true` | Zet webhooks globaal aan of uit (geldt voor het vuren én voor de firing-job) |
| `MaximumRetries` | `5` | Maximaal aantal retries |
| `Period` | `00:00:10` | Interval van de firing-job, en dus ook de **retry-interval** en de maximale vertraging van de eerste poging |
| `EnableLoggingCleanup` | `true` | Ruimt oude logs automatisch op |
| `KeepLogsForDays` | `30` | Bewaartermijn van de logs |
| `PayloadType` | v17: `Legacy`; v18: `Minimal` | Payloadformaat |

Bron: `[src]Umbraco.Core/Configuration/Models/WebhookSettings.cs`; [docs, "Webhook Settings"].

### Leveringsmechanisme (uit de broncode)

1. De notification handler (`WebhookEventBase.HandleAsync`) draait **alleen op servers met rol `Single` of `SchedulingPublisher`**. Hij zoekt de webhooks op alias en roept per webhook en per entity `IWebhookFiringService.FireAsync` aan.
2. `FireAsync` **verstuurt niets.** Het schrijft een rij naar de tabel `umbracoWebhookRequest` (webhookKey, eventName, requestObject = geserialiseerde JSON, retryCount = 0).
   Bron: `[src]Umbraco.Infrastructure/Services/Implement/WebhookFiringService.cs`, `[src]Umbraco.Core/Services/WebhookRequestService.cs`.
3. De distributed background job `WebhookFiring` draait elke `Period` op één server. Hij leest **alle** openstaande requests en verstuurt ze **parallel** (`Task.WhenAll`).
4. Bij een 2xx-antwoord wordt de rij verwijderd. Anders wordt `RetryCount++` gedaan en volgt een nieuwe poging bij de volgende run. Als `RetryCount >= MaximumRetries` bereikt is, wordt de rij **verwijderd, ook bij een fout**. Met de defaults zijn dat 6 pogingen (retry 0 tot en met 5) over ongeveer een minuut, zonder exponential backoff en zonder dead-letter.
5. De HttpClient `WebhookFiring` krijgt geen eigen timeout en gebruikt dus de .NET-default van 100 s (`UmbracoBuilderExtensions.cs`).

Bron: `[src]Umbraco.Infrastructure/BackgroundJobs/Jobs/DistributedJobs/WebhookFiring.cs`.

### Delivery logs

Elke poging, ook een geslaagde, levert een `WebhookLog`-rij op met URL, statuscode, datum, eventAlias, retryCount, request headers en body, response headers en body, `ExceptionOccured` en `IsSuccessStatusCode`. De logs zijn zichtbaar in de backoffice (Settings > Webhooks > Logs) en via de Management API (`Controllers/Webhook/Logs/*`). Ze worden opgeruimd door de job `WebhookLoggingCleanup`.
Bron: `[src]Umbraco.Core/Models/WebhookLog.cs`, `[src]Umbraco.Core/Services/WebhookLogFactory.cs`.

---

## 5. uSync en webhooks

- **De gratis uSync (open source, MPL-2.0) heeft een `WebhookHandler`** in `uSync.BackOffice`. Die exporteert en importeert webhooks bij save en delete, net als de andere settings-handlers. Daarvoor is uSync.Complete niet nodig. Complete gaat over content, media en publisher-functionaliteit.
  Bron: https://github.com/KevinJump/uSync/blob/v17.4.3/uSync.BackOffice/SyncHandlers/Handlers/WebhookHandler.cs ; registratie in `uSync.BackOffice/uSyncBackOfficeBuilderExtensions.cs` (r. ~178, ~217).
- De serializer schrijft `Url`, `Enabled`, `Name`, `Description`, `ContentTypeKeys`, `Events` **en `Headers`** weg. Het identificeren gebeurt op `Key` (GUID).
  Bron: https://github.com/KevinJump/uSync/blob/v17.4.3/uSync.Core/Serialization/Serializers/WebhookSerializer.cs
- **Gevolgen:**
  - (a) Het shared secret komt in de uSync-XML en dus in git terecht.
  - (b) De URL is omgevingsspecifiek (lokaal, test, productie) en wordt ook gesynchroniseerd.
  - Bruikbare oplossingen: de webhook **in code provisionen** bij opstart (`IWebhookService.CreateAsync`/`UpdateAsync` met URL en secret uit configuratie), of de webhook-handler in uSync uitsluiten en per omgeving configureren. Of de handler in de standaard handlerset zit, heb ik niet in de uSync-config geverifieerd. Controleer in de POC of er een map `uSync/v17/Webhooks` verschijnt.
- Zonder uSync staan webhook-definities alleen in de database: `umbracoWebhook`, `umbracoWebhook2Events`, `umbracoWebhook2ContentTypeKeys` en `umbracoWebhook2Headers` (`[src]Umbraco.Infrastructure/Persistence/Dtos/Webhook*Dto.cs`).

---

## 6. Verrassingen voor het productieontwerp

1. **At-least-once binnen een begrensd budget, daarna stil verlies.** Na `MaximumRetries` wordt het request verwijderd. Alleen de log blijft over. Is de API meer dan ongeveer een minuut onbereikbaar (bv. bij een deploy), dan gaan events verloren. Je kunt `MaximumRetries`/`Period` verhogen, maar dat geldt globaal en blijft een vaste interval.
2. **Duplicaten zijn mogelijk.** Twee gevallen: een timeout of verbroken verbinding na verwerking door de ontvanger, of een crash tussen verzenden en de DELETE. Er is **geen delivery-id of event-id**. De ontvanger moet dus idempotent zijn op bijvoorbeeld (`id`, event, `cultures[].date`).
3. **Geen ordening.** Alle openstaande requests gaan parallel en retries schuiven op. Een publish en een unpublish kort na elkaar kunnen in omgekeerde volgorde aankomen. Je kunt de volgorde niet afleiden uit de payload: unpublish bevat geen tijdstip. De ontvanger moet bij twijfel de actuele staat ophalen (Delivery API) in plaats van de volgorde te vertrouwen.
4. **Niet transactioneel met de publish.** Scoped notifications worden pas gepubliceerd **na** de DB-commit van de content (`Scope.DisposeLastScope`: eerst `CompleteTransaction()`, daarna `RobustExit` en `HandleScopedNotifications`; `[src]Umbraco.Infrastructure/Scoping/Scope.cs` r. ~543-580, `[src]Umbraco.Core/Scoping/CoreScope.cs` r. 335). De insert in `umbracoWebhookRequest` is een aparte transactie. Een crash daartussen betekent dat het event verloren gaat. Umbraco's wachtrij is dus geen echte outbox ten opzichte van de content-wijziging. Voor de productie-outbox is dat aanvaardbaar zolang de outbox in de Content Service staat en de webhook als "trigger" geldt, met een periodieke reconcile als vangnet.
5. **Vertraging van maximaal `Period`** (10 s) voor de eerste poging. Dat is geen realtime levering.
6. **Batch publishes:** publish-with-descendants en geplande publicaties leveren één request per document op, en die worden allemaal tegelijk verstuurd. Bij grote boomstructuren krijg je een burst van parallelle POSTs. De stub en API moeten dat aankunnen of throttlen.
7. **Semantiek van cultuur-unpublish** (zie §2): een taal offline halen geeft "Published" en geen "Unpublished".
8. **Prullenbak = geen unpublish**, en een parent depubliceren geeft geen events voor de kinderen.
9. **Load balancing:** events worden alleen in de wachtrij gezet op een server met rol `Single` of `SchedulingPublisher` (`WebhookEventBase.HandleAsync`). Editing moet dus op die server gebeuren, en dat is de gangbare opzet.
10. **Het standaard payloadtype verandert tussen v17 en v18** (Legacy naar Minimal). Zet `PayloadType: "Minimal"` expliciet in appsettings, zodat een upgrade het contract niet breekt.
11. **Header-alias in de docs klopt niet:** de docs zeggen `Umbraco.ContentPublished`, maar de code stuurt `Umbraco.ContentPublish`.

### Aanbeveling

- **POC:** gebruik de ingebouwde webhooks `Umbraco.ContentPublish` en `Umbraco.ContentUnpublish` (eventueel ook `Umbraco.ContentMovedToRecycleBin` via een composer), met `PayloadType: Minimal` en de header `X-Webhook-Secret`. De stub valideert het secret, is idempotent en haalt bij twijfel de staat op via de Delivery API.
- **Productie:** overweeg een eigen `INotificationAsyncHandler<ContentPublishedNotification/ContentUnpublishedNotification/ContentMovedToRecycleBinNotification>`. Die heeft toegang tot de volledige cultuurmaps, zoals `PublishedCultures` en `UnpublishedCultures`. Hij kan een event-id en timestamp toevoegen en direct naar de outbox van de Content Service schrijven of de API aanroepen met HMAC. Daarmee omzeil je de beperkingen 1-3 en 7.
