using System.Text;
using CloudFri.Api.Data;
using CloudFri.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var conn = builder.Configuration.GetConnectionString("Default")
    ?? "server=localhost;port=3306;database=cloudfri;user=root;password=cloudfri;TreatTinyAsBoolean=true";
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseMySql(conn, new MySqlServerVersion(new Version(8, 0, 36))));

var jwtKey = builder.Configuration["Jwt:Key"] ?? "dev-only-key-change-in-production-0123456789abcdef";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "CloudFri",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "CloudFri",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddCors(opt => opt.AddPolicy("dev", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseCors("dev");
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.Now }));

// 建库 + 种子（管理员、成员名单、字典、流程模板）
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // 数据库可能尚未完全就绪（MySQL 首次初始化有窗口期），带间隔重试，避免 EF 内部 Exists 紧密重试空转
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            db.Database.Migrate();
            await SeedData.RunAsync(db, builder.Configuration);
            break;
        }
        catch (Exception ex) when (attempt < 15)
        {
            Console.WriteLine($"[startup] 数据库未就绪（第 {attempt} 次）：{ex.Message}，5 秒后重试");
            Thread.Sleep(5000);
        }
    }
}

app.Run();
