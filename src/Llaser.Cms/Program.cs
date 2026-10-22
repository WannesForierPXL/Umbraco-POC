
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

#if DEBUG
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
#endif

// Lokale ontwikkeling: zonder secrets crasht Umbraco met een onduidelijke melding. Zeg dan wat er moet gebeuren.
if (builder.Environment.IsDevelopment())
{
    string[] nodig =
    [
        "ConnectionStrings:umbracoDbDSN",
        "Umbraco:CMS:DeliveryApi:ApiKey",
    ];
    var ontbrekend = nodig.Where(k => string.IsNullOrWhiteSpace(builder.Configuration[k])).ToList();
    if (ontbrekend.Count > 0)
    {
        Console.Error.WriteLine($"""

            LLASER: lokale instellingen ontbreken ({string.Join(", ", ontbrekend)}).
            Ze horen in src/Llaser.Cms/appsettings.Development.json te staan. Haal de laatste versie op (git pull)
            of herstel dat bestand (git checkout src/Llaser.Cms/appsettings.Development.json). Zie README.

            """);
        return 1;
    }
}

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers()
    .Build();

WebApplication app = builder.Build();


await app.BootUmbracoAsync();


app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
return 0;
