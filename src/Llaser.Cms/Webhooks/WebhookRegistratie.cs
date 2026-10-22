using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Llaser.Cms.Webhooks;

/// <summary>
/// Maakt bij het opstarten de webhook naar de Content Stub aan (of werkt hem bij).
/// Bewust in code en niet via uSync: het gedeelde secret staat in de headers van de webhook en mag niet in git.
/// Het secret komt uit configuratie (user-secrets / omgevingsvariabele Llaser__Webhook__Secret).
/// </summary>
public sealed class WebhookRegistratie(
    IWebhookService webhooks,
    IRuntimeState runtime,
    IConfiguration configuration,
    ILogger<WebhookRegistratie> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public const string SecretHeader = "X-Webhook-Secret";

    private static readonly string[] Events =
    [
        Constants.WebhookEvents.Aliases.ContentPublish,
        Constants.WebhookEvents.Aliases.ContentUnpublish,
    ];

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtime.Level != RuntimeLevel.Run) return;

        var url = configuration["Llaser:Webhook:Url"];
        var secret = configuration["Llaser:Webhook:Secret"];
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(secret))
        {
            logger.LogWarning("Geen webhook naar de Content Stub: Llaser:Webhook:Url of Llaser:Webhook:Secret ontbreekt");
            return;
        }

        var bestaand = (await webhooks.GetAllAsync(0, int.MaxValue)).Items
            .FirstOrDefault(w => string.Equals(w.Url, url, StringComparison.OrdinalIgnoreCase));

        if (bestaand is null)
        {
            var webhook = new Webhook(url, enabled: true, events: Events,
                headers: new Dictionary<string, string> { [SecretHeader] = secret });
            var resultaat = await webhooks.CreateAsync(webhook);
            logger.LogInformation("Webhook naar {Url} aangemaakt: {Status}", url, resultaat.Status);
            return;
        }

        // Secret of events gewijzigd in de configuratie → bijwerken
        if (!bestaand.Headers.TryGetValue(SecretHeader, out var huidig) || huidig != secret || !Events.All(bestaand.Events.Contains))
        {
            bestaand.Headers[SecretHeader] = secret;
            bestaand.Events = Events;
            bestaand.Enabled = true;
            var resultaat = await webhooks.UpdateAsync(bestaand);
            logger.LogInformation("Webhook naar {Url} bijgewerkt: {Status}", url, resultaat.Status);
        }
    }
}

public sealed class WebhookComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) =>
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, WebhookRegistratie>();
}
