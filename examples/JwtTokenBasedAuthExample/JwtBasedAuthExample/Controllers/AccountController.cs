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
}