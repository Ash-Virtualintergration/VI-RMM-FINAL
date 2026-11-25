using ViRMM.Components;
using ViRMM.Hubs;
using ViRMM.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 32 * 1024 * 1024; // 32MB for screen frames and file transfers
    options.EnableDetailedErrors = true;
});

builder.Services.AddSingleton<UserStore>();
builder.Services.AddScoped<SessionState>();
builder.Services.AddSingleton<AgentService>();
builder.Services.AddSingleton<AuditService>();
builder.Services.AddSingleton<ScriptService>();
builder.Services.AddSingleton<PolicyService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapHub<AgentHub>("/agentHub");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
