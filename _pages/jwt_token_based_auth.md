### ASP.NET Core JWT Token Tabanlı Doğrulama (JWT Token Based Authentication)

Bu yazıda JWT'nin ne olduğuna dair bir anlatım olmayacaktır. 
Sadece ASP.NET Core projemizede nasıl kullanabiliriz, onun örneklerle anlatımı olacak.
Ayrıca API'lerle daha rahat çalışmak için swagger entegrasyonunu da yapacağız. 
Şimdi adım adım neler yapacağımıza bakalım.

JWT için gerekli olan parametreleri taşımak için bir nesneye ihtiyacımız var. Bunun için aşağıdaki gibi bir sınıf oluşturuyoruz.

#### JwtTokenConfiguration.cs

````c#
public class JwtTokenConfiguration
{
    public string Issuer { get; set; }

    public string Audience { get; set; }

    public SigningCredentials SigningCredentials { get; set; }

    public DateTime? EndDate { get; set; }

    public DateTime? StartDate { get; set; }
}
````

**Startup** dosyası da aşağıdaki gibi olacak. 
Kodların açıklamasını, adımların akışını bozmamak için, yorum satırı olarak ekleyeceğim.
Kullanılan kütüphane referanslarını da kodlara ekliyorum.
Böylece gerekli olan kütüphanleri görüp, yükleyebilirsiniz.

#### Startup.cs

````c#
using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;

namespace JwtBasedAuthExample
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // JWT için gerekli olan nesneyi oluşturuyoruz. 
            var signingKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes("JwtBasedAuthExample_8CFB2EC534E14D56"));
            var jwtTokenConfiguration = new JwtTokenConfiguration
            {
                Issuer = "JwtBasedAuthExample",
                Audience = "http://localhost:44341",
                SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256),
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(60),
            };
            
            // Oluşturduğumuz nesneyi "singleton" olarak register(dependency injection) eder.
            // Böylece bu nesneyi, istedigimiz zaman "inject" edip değerlerini alabiliriz.
            services.Configure<JwtTokenConfiguration>(config =>
            {
                config.Audience = jwtTokenConfiguration.Audience;
                config.EndDate = jwtTokenConfiguration.EndDate;
                config.Issuer = jwtTokenConfiguration.Issuer;
                config.StartDate = jwtTokenConfiguration.StartDate;
                config.SigningCredentials = jwtTokenConfiguration.SigningCredentials;
            });

            // JWT için gerekli olan ayarların yapılması
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(jwtBearerOptions =>
            {
                jwtBearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateActor = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtTokenConfiguration.Issuer,
                    ValidAudience = jwtTokenConfiguration.Audience,
                    IssuerSigningKey = signingKey
                };
            });

            // Swagger için gerekli olan ayarların yapılması.
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "JwtBasedAuthExample", Version = "v1" });
                
                // Swagger için bir doğrulama (authentication) mekanizmasının ayarlanması. 
                options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
                {
                    Description = "Standard Authorization header using the Bearer scheme. Example: \"bearer {token}\"",
                    In = ParameterLocation.Header,
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey
                });

                options.OperationFilter<SecurityRequirementsOperationFilter>();
            });

            services.AddControllers();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Swagger middleware 
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "JwtBasedAuthExample API V1");
            });

            app.UseHttpsRedirection();
            app.UseRouting();
            
            // JWT nin çalışması için ayrıca "Authentication-Middleware" de eklenmesi lazım
            app.UseAuthentication();
            app.UseAuthorization();
            
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
````

Şimdi kullanının JWT tabanlı bir giriş (login) gerekli olan API'yi yazalım.
Yine kullanılan kütüphanelerle birlikte paylaşıyorum kodu.

#### AccountController.cs

````c#
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using JwtBasedAuthExample.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace JwtBasedAuthExample.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly JwtTokenConfiguration _jwtTokenConfiguration;

        public AccountController(IOptions<JwtTokenConfiguration> jwtTokenConfiguration)
        {
            _jwtTokenConfiguration = jwtTokenConfiguration.Value;
        }

        [HttpPost("/api/[action]")]
        public async Task<ActionResult<LoginOutput>> Login([FromBody]LoginInput input)
        {
            if (string.IsNullOrEmpty(input.UserNameOrEmail) || string.IsNullOrEmpty(input.Password))
            {
                return BadRequest("User name or password is not valid!");
            }

            // Kullanıcının sistemde kayıtlı olup olmadığı kontrol ediliyor.
            //var userToVerify = await _identityAppService.FindUserByUserNameOrEmailAsync(input.UserNameOrEmail);
            //if (userToVerify == null)
            //{
            //    return NotFound("User name or password is not correct!");
            //}

            // Kullanıcı için şifre kontrolü yapılıyor.
            //if (!await _identityAppService.CheckPasswordAsync(userToVerify, input.Password)) 
            //{
            //    return BadRequest("User name or password is not correct!");
            //}
            var claims = new List<Claim>(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, input.UserNameOrEmail),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("Id", Guid.NewGuid().ToString())
            });

            //var roles = identityAppService.GetRolesByUserName(userToVerify.UserName);
            //foreach (var role in roles)
            //{
            //    claims.Add(new Claim(ClaimTypes.Role, role.Name));
            //}

            // HttpContext içinde, o an login olan kullanıcıların rol kontrolü (User.IsInRole("RoleName")) için, bu rolleri de Claim olarak eklemek gerekiyor 
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));

            var claimsIdentity =  new ClaimsIdentity(new ClaimsIdentity(new GenericIdentity(input.UserNameOrEmail, "Token"), claims));

            var token = new JwtSecurityToken
            (
                issuer: _jwtTokenConfiguration.Issuer,
                audience: _jwtTokenConfiguration.Audience,
                claims: claimsIdentity.Claims,
                expires: _jwtTokenConfiguration.EndDate,
                notBefore: _jwtTokenConfiguration.StartDate,
                signingCredentials: _jwtTokenConfiguration.SigningCredentials
            );

            return Ok(new LoginOutput
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token)
            });
        }
    }
    
    public class LoginInput
    {
        public string UserNameOrEmail { get; set; }

        public string Password { get; set; }
    }
    
    public class LoginOutput
    {
        public string Token { get; set; }
    }
}
````

Burada yaptığımız şey, kullanıcının adına ve girdiği şifreye göre kullanıcıyı bulup, şifresini doğrulayıp, sonuçta bir tken üretip dönüyoruz.

**Not:** Kullanıcının veritabanından çekilmesi ve şifre doğrulaması kısımlarını yorum satırı olarak ekledim.
Şuan ki hali ile her kullanıcı adı ve şifre için bir token dönecek şekilde çalışıyor. 

Artık uygulama, JWT tabanlı doğrulama yapabilir. Doğrulama isteyen herhangi bir API-enpoint için bu token ile giriş yapabilirsiniz. 
Giriş yaptıkdan sonra oluşan token ile istek yapmak için örnek:


> curl -X GET "https://localhost:44341/api/Get" -H "accept: text/plain" -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1lIjoic3RyaW5nIiwic3ViIjoic3RyaW5nIiwianRpIjoiYWZmZjYxMDEtZjEyNi00YjI0LThkNzAtNzMyMGExM2YyNjAzIiwiSWQiOiJkYzYyZTdlOS05N2Y1LTRhYzAtOTI3OS04M2U3OGVlMGU2YTMiLCJodHRwOi8vc2NoZW1hcy5taWNyb3NvZnQuY29tL3dzLzIwMDgvMDYvaWRlbnRpdHkvY2xhaW1zL3JvbGUiOiJBZG1pbiIsIm5iZiI6MTU4OTAyMjY4MywiZXhwIjoxNTk0MjA2NjgzLCJpc3MiOiJKd3RCYXNlZEF1dGhFeGFtcGxlIiwiYXVkIjoiaHR0cDovL2xvY2FsaG9zdDo0NDM0MSJ9.DAN5fBA7sEijtrDtcpgFuSeE3dMhFGrDU-sWYcdBpZo"









