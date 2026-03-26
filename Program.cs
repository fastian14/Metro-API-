using MetroAPI.Configuration;
using MetroAPI.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/metro-api-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Metro API - SparsWeb Integration",
        Version = "v1",
        Description = "Metropolitan carrier API wrapper for SparsWeb. Exposes CreateOrder, GetOrderLabels, and GetOrderBOL endpoints."
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

// Bind Metro API settings
builder.Services.Configure<MetroApiSettings>(
    builder.Configuration.GetSection(MetroApiSettings.SectionName));

// Read settings once for HttpClient configuration
var metroSettings = builder.Configuration
    .GetSection(MetroApiSettings.SectionName)
    .Get<MetroApiSettings>()!;

// Token service — singleton so the cached token is shared across all requests.
// Uses a plain HttpClient (no base address) because it calls the absolute TokenUrl.
builder.Services.AddHttpClient<IMetroTokenService, MetroTokenService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(metroSettings.TimeoutSeconds);
});
builder.Services.AddSingleton<IMetroTokenService, MetroTokenService>();

// MetroService — scoped; uses the shared token from MetroTokenService.
builder.Services.AddHttpClient<IMetroService, MetroService>(client =>
{
    client.BaseAddress = new Uri(metroSettings.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(metroSettings.TimeoutSeconds);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Metro API v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
