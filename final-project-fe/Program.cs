using Serilog.Formatting.Json;
using Serilog;
using final_project_fe.Utils;
using Microsoft.Extensions.Options;
using final_project_fe.Dtos.Comment;
using final_project_fe.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddSession();

//Config Logger
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


builder.Host.UseSerilog();


builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));
builder.Services.Configure<ImageSettings>(builder.Configuration.GetSection("ImageSettings"));
builder.Services.AddScoped<PayOSService>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});


//Config SignalR
builder.Services.Configure<SignalrSetting>(builder.Configuration.GetSection("SignalrSetting"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<SignalrSetting>>().Value);

builder.Services.AddHttpClient();

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseSession();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
