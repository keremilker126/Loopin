using Loopin.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using Loopin.Services;
using Loopin.Models;

var builder = WebApplication.CreateBuilder(args);

// MVC + JSON ayarı
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=loopin.db"));

// Session
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();

// 🔓 CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

// 🔧 Upload limitlerini yükselt


builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10737418240; // 10 GB
});


builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10737418240; // 10 GB
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(60); // bağlantıyı uzun süre açık tut
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(60);
});

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<EmailService>();



var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseStaticFiles();

app.UseRouting();



app.UseSession();

app.UseAuthorization();

// MVC route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// API route
app.MapControllers();

app.Run();
