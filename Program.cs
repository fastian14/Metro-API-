using MetroAPI.Configuration;
using MetroAPI.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ───────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/metro-api-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// ── MVC + Swagger ─────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title       = "Metro API – SparsWeb Integration",
        Version     = "v1",
        Description = "Metropolitan carrier API wrapper for SparsWeb. " +
                      "Exposes CreateOrder, GetOrderLabels, and GetOrderBOL endpoints."
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Services.Configure<MetroApiSettings>(
    builder.Configuration.GetSection(MetroApiSettings.SectionName));

var metroSettings = builder.Configuration
    .GetSection(MetroApiSettings.SectionName)
    .Get<MetroApiSettings>()!;

// ── Token service ─────────────────────────────────────────────────────────────
// Named HttpClient used internally by MetroTokenService (no base address —
// it calls the absolute TokenUrl). Registered separately from MetroService's
// client so lifetimes are independent.
builder.Services.AddHttpClient("MetroToken", client =>
{
    client.Timeout = TimeSpan.FromSeconds(metroSettings.TimeoutSeconds);
});

// Singleton: one instance per application lifetime so the cached bearer token
// is shared across all requests. Uses IHttpClientFactory internally.
builder.Services.AddSingleton<IMetroTokenService, MetroTokenService>();

// ── Metro order service ───────────────────────────────────────────────────────
// Typed HttpClient registered as scoped (default for AddHttpClient).
builder.Services.AddHttpClient<IMetroService, MetroService>(client =>
{
    client.BaseAddress = new Uri(metroSettings.BaseUrl);
    client.Timeout     = TimeSpan.FromSeconds(metroSettings.TimeoutSeconds);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Metro API v1");
        c.RoutePrefix = string.Empty; // Swagger UI at root "/"
    });
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
