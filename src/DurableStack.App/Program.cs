using DurableStack.ControlPlane.DependencyInjection;
using DurableStack.App.Services.Api;
using DurableStack.App.Data;
using DurableStack.App.Services.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.Configure<MvcViewOptions>(options =>
{
    options.HtmlHelperOptions.CheckBoxHiddenInputRenderMode = CheckBoxHiddenInputRenderMode.None;
});
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
builder.Services.AddControlPlanePostgres(builder.Configuration);
builder.Services.AddDbContext<AppIdentityDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ControlPlane")));
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 12;
        options.Password.RequiredUniqueChars = 1;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<AppIdentityDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/auth";
    options.AccessDeniedPath = "/auth";
});
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

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var controlPlaneDb = scope.ServiceProvider.GetRequiredService<DurableStack.ControlPlane.ControlPlaneDbContext>();
    var appIdentityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    controlPlaneDb.Database.Migrate();
    appIdentityDb.Database.Migrate();

    const string defaultAdminEmail = "admin@durablestack.com";
    const string defaultAdminPassword = "Password01!*";

    var adminUser = userManager.FindByEmailAsync(defaultAdminEmail).GetAwaiter().GetResult();
    if (adminUser is null)
    {
        adminUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = defaultAdminEmail,
            Email = defaultAdminEmail,
            DisplayName = "DurableStack Admin",
            EmailConfirmed = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var createResult = userManager.CreateAsync(adminUser, defaultAdminPassword).GetAwaiter().GetResult();
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(x => x.Description));
            throw new InvalidOperationException($"Failed to create default admin user: {errors}");
        }
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();

app.UseAuthorization();

app.MapHealthChecks("/healthz");

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
