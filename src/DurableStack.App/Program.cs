using DurableStack.ControlPlane.DependencyInjection;
using DurableStack.App.Services.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHealthChecks();
builder.Services.AddControlPlanePostgres(builder.Configuration);
builder.Services.Configure<DurableStackApiOptions>(
    builder.Configuration.GetSection(DurableStackApiOptions.SectionName));
builder.Services.AddHttpClient<DurableStackApiClient>((serviceProvider, client) =>
{
    var apiOptions = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<DurableStackApiOptions>>()
        .Value;

    client.BaseAddress = new Uri(apiOptions.BaseUrl);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapHealthChecks("/healthz");

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
