using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Llaser.Cms.Publiceren;

/// <summary>
/// Umbraco weigert een item te publiceren zolang een bovenliggend item niet gepubliceerd is, maar toont de Editor
/// dan enkel "check logs". Deze handler vangt dat vooraf op en stopt met een leesbare melding die zegt wat te doen.
/// </summary>
public sealed class OuderNietGepubliceerdMelding(IContentService contentService)
    : INotificationHandler<ContentPublishingNotification>
{
    public void Handle(ContentPublishingNotification notification)
    {
        // Items die in dezelfde actie mee gepubliceerd worden (bv. "Publish with descendants") tellen als gepubliceerd.
        var inDezelfdeActie = notification.PublishedEntities.Select(c => c.Id).ToHashSet();

        foreach (var content in notification.PublishedEntities)
        {
            var nietGepubliceerd = contentService.GetAncestors(content)
                .Where(a => !a.Published && !inDezelfdeActie.Contains(a.Id))
                .OrderBy(a => a.Level)
                .ToList();
            if (nietGepubliceerd.Count == 0) continue;

            var hoogste = nietGepubliceerd[0];
            var namen = string.Join(" en ", nietGepubliceerd.Select(a => $"'{a.Name}'"));
            notification.CancelOperation(new EventMessage(
                "Publiceren niet mogelijk",
                $"'{content.Name}' kan pas online als {namen} gepubliceerd {(nietGepubliceerd.Count == 1 ? "is" : "zijn")}. " +
                $"Tip: kies op '{hoogste.Name}' Publish → 'Publish with descendants' en vink 'Include unpublished content items' aan.",
                EventMessageType.Error));
            return;
        }
    }
}

public sealed class PubliceerComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) =>
        builder.AddNotificationHandler<ContentPublishingNotification, OuderNietGepubliceerdMelding>();
}
