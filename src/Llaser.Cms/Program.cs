
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

            LLASER: de lokale secrets ontbreken ({string.Join(", ", ontbrekend)}).
            Voer eerst uit vanuit de map van de repo:

                pwsh ./scripts/setup.ps1        (of: powershell -ExecutionPolicy Bypass -File .\scripts\setup.ps1)

            en start Umbraco daarna opnieuw. Zie README, stap 3.

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
