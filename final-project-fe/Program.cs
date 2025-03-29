using Serilog.Formatting.Json;
using Serilog;
using final_project_fe.Utils;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));

builder.Services.AddHttpClient();

var app = builder.Build();
// Config Logger
Log.Logger = new LoggerConfiguration()

    .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        new JsonFormatter(),
        path: "logs/console/console-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)

    .WriteTo.File(
        new JsonFormatter(),
        path: "logs/error/error-.log",
        rollingInterval: RollingInterval.Day,
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error,
        retainedFileCountLimit: 7)
    .CreateLogger();


//Config AddHttpClient
/*builder.Services.AddHttpClient("APIClient", client =>
{
    client.BaseAddress = new Uri("https://your-api-base-url.com/"); 
});*/


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

app.UseAuthorization();

app.MapRazorPages();

app.Run();
