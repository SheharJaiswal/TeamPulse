using System.Text;
using Identity.Application.Abstractions;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder=WebApplication.CreateBuilder(args);
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwt=builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is missing.");
if(Encoding.UTF8.GetByteCount(jwt.SigningKey)<32)
    throw new InvalidOperationException("JWT signing key must be at least 32 bytes.");
var cs=builder.Configuration.GetConnectionString("IdentityDatabase")
    ?? throw new InvalidOperationException("IdentityDatabase connection string is missing.");

builder.Services.AddDbContext<IdentityDbContext>(o=>o.UseNpgsql(cs));
builder.Services.AddScoped<IEmployeeRepository,EmployeeRepository>();
builder.Services.AddScoped<ITokenService,JwtTokenService>();
builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<Identity.Domain.Entities.Employee>,
    Microsoft.AspNetCore.Identity.PasswordHasher<Identity.Domain.Entities.Employee>>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o=>{
    o.MapInboundClaims=false;
    o.TokenValidationParameters=new TokenValidationParameters{
        ValidateIssuer=true,ValidIssuer=jwt.Issuer,
        ValidateAudience=true,ValidAudience=jwt.Audience,
        ValidateLifetime=true,ValidateIssuerSigningKey=true,
        IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        NameClaimType="name",RoleClaimType="role",ClockSkew=TimeSpan.FromSeconds(30)};
});
builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks().AddDbContextCheck<IdentityDbContext>();

var app=builder.Build();
if(app.Environment.IsDevelopment()){app.UseSwagger();app.UseSwaggerUI();}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();
public partial class Program;