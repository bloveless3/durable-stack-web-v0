using DurableStack.ControlPlane.DependencyInjection;
using DurableStack.App.Configuration;
using DurableStack.App.Data;
using DurableStack.App.Services.Api;
using DurableStack.App.Services.Identity;
using DurableStack.App.Services.Onboarding;
using DurableStack.App.Menu;
using DurableStack.App.Services.Preferences;
using DurableStack.App.Services.Reports;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddProblemDetails();
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod
        | HttpLoggingFields.RequestPath
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.Duration;
});
builder.Services.Configure<MvcViewOptions>(options =>
{
    options.HtmlHelperOptions.CheckBoxHiddenInputRenderMode = CheckBoxHiddenInputRenderMode.None;
});
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
builder.Services.AddScoped<IUserPreferenceService, UserPreferenceService>();
builder.Services.AddScoped<IGlobalFilterOptionsProvider, GlobalFilterOptionsProvider>();
builder.Services.AddScoped<IOnboardingService, OnboardingService>();
builder.Services.AddScoped<IUserApiTokenService, UserApiTokenService>();
builder.Services.AddScoped<IReportsQueryService, ReportsQueryService>();
builder.Services.AddSingleton<IAppMenuProvider, AppMenuProvider>();
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
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});
builder.Services.Configure<DurableStackApiOptions>(builder.Configuration.GetSection(DurableStackApiOptions.SectionName));
builder.Services.Configure<UserApiTokenOptions>(builder.Configuration.GetSection("Authentication:UserJwt"));
builder.Services.AddHttpClient<IReportsApiClient, ReportsApiClient>();

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

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    foreach (var roleName in AppRoles.All)
    {
        if (!roleManager.RoleExistsAsync(roleName).GetAwaiter().GetResult())
        {
            var createRole = roleManager.CreateAsync(new IdentityRole<Guid>(roleName)).GetAwaiter().GetResult();
            if (!createRole.Succeeded)
            {
                var errors = string.Join("; ", createRole.Errors.Select(x => x.Description));
                throw new InvalidOperationException($"Failed to create role '{roleName}': {errors}");
            }
        }
    }

    var adminRoles = userManager.GetRolesAsync(adminUser).GetAwaiter().GetResult();
    if (!adminRoles.Contains(AppRoles.Admin, StringComparer.OrdinalIgnoreCase))
    {
        var addRoleResult = userManager.AddToRoleAsync(adminUser, AppRoles.Admin).GetAwaiter().GetResult();
        if (!addRoleResult.Succeeded)
        {
            var errors = string.Join("; ", addRoleResult.Errors.Select(x => x.Description));
            throw new InvalidOperationException($"Failed to assign '{AppRoles.Admin}' role to default admin: {errors}");
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
app.UseHttpLogging();
app.UseRouting();

app.Use(async (context, next) =>
{
    var start = DateTimeOffset.UtcNow;

    try
    {
        await next();

        app.Logger.LogInformation(
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms (TraceId: {TraceId})",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            (DateTimeOffset.UtcNow - start).TotalMilliseconds,
            context.TraceIdentifier);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(
            ex,
            "Unhandled exception for {Method} {Path} (TraceId: {TraceId})",
            context.Request.Method,
            context.Request.Path,
            context.TraceIdentifier);

        throw;
    }
});

app.UseAuthentication();

app.UseAuthorization();

app.MapHealthChecks("/healthz");

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
