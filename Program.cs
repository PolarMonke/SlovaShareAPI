using Backend;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 21)),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5, 
            maxRetryDelay: TimeSpan.FromSeconds(30), 
            errorNumbersToAdd: null 
        )
    )
);

builder.Services.AddCors(options => 
{
    options.AddPolicy("ReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.ASCII.GetBytes("your-32-character-secret-key-here")),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true
        };
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseHttpsRedirection();
}
var uploadsPath = Path.Combine(builder.Environment.WebRootPath, "uploads");
Directory.CreateDirectory(uploadsPath);

app.UseStaticFiles();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads",
    ContentTypeProvider = new FileExtensionContentTypeProvider()
});

app.UseRouting();
app.UseCors("ReactApp");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();


app.Run();