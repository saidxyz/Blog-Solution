using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BlogSolution.Data;
using BlogSolution.Authorization;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Legg til tjenester til containeren.
builder.Services.AddControllersWithViews();

// Konfigurer DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Konfigurer Identity
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>() // Legg til roller
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Legg til autorisasjonstjenester
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("IsPostOwner", policy =>
        policy.Requirements.Add(new IsPostOwnerRequirement()));

    options.AddPolicy("IsCommentOwner", policy =>
        policy.Requirements.Add(new IsCommentOwnerRequirement()));
});

// Registrer autorisasjonshandlere
builder.Services.AddScoped<IAuthorizationHandler, IsPostOwnerHandler>();
builder.Services.AddScoped<IAuthorizationHandler, IsCommentOwnerHandler>();

var app = builder.Build();

// Initialiser roller og adminbruker
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
    await SeedRolesAsync(roleManager, userManager);
}

// Metoden for å seed roller og adminbruker
async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, UserManager<IdentityUser> userManager)
{
    string[] roleNames = { "Admin", "User" };

    foreach (var roleName in roleNames)
    {
        var roleExist = await roleManager.RoleExistsAsync(roleName);
        if (!roleExist)
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    var adminEmail = "admin@blogsolution.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    if (adminUser == null)
    {
        var newAdmin = new IdentityUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        string adminPassword = "Admin@123"; // Bruk et sterkt passord

        var createAdmin = await userManager.CreateAsync(newAdmin, adminPassword);
        if (createAdmin.Succeeded)
        {
            await userManager.AddToRoleAsync(newAdmin, "Admin");
        }
    }

    // Valgfritt: Tilordne "User" rollen til andre brukere eller opprett nye brukere med denne rollen
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); // Legg til autentisering
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Blog}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
