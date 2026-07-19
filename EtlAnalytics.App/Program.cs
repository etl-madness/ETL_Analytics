using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using EtlAnalytics.App.Services;
using Radzen;
using EtlAnalytics.RulesEngine.Services;
using EtlAnalytics.RulesEngine.Interfaces;
using EtlAnalytics.RulesEngine.Providers;
using EtlAnalytics.App.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<TooltipService>();
builder.Services.AddScoped<ContextMenuService>();
builder.Services.AddScoped<SqlDatabaseService>();
builder.Services.AddScoped<IBusinessRuleStore>(sp => sp.GetRequiredService<SqlDatabaseService>());
builder.Services.AddScoped<IRuleDbProvider, SqlServerRuleDbProvider>();
builder.Services.AddScoped<DtsxLoaderSettingsService>();
builder.Services.AddScoped<DtsxLoaderExecutionService>();
builder.Services.AddSingleton<IEncryptionService, AesEncryptionService>();
builder.Services.AddScoped<BusinessRuleEngine<BusinessRuleContext>>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Initialize database tables
using (var scope = app.Services.CreateScope())
{
    var dbService = scope.ServiceProvider.GetRequiredService<SqlDatabaseService>();
    await dbService.CreateBusinessRuleTablesIfNotExistsAsync();
}

app.Run();
