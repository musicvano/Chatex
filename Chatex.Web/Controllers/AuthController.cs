using Chatex.Core.Services;
using Chatex.Web.Models;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace Chatex.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController(
        UserService userService,
        JwtService jwtService) : ControllerBase
    {
        [HttpPost("signup")]
        public IActionResult SignUp(SignupRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest("Invalid data");
            var user = userService.Create(request.Name, request.Email.ToLower(), request.Password);            
            return Ok();
        }

        [HttpPost("signin")]
        public IActionResult SignIn(SignInRequest request)
        {
            var user = userService.ValidateCredentials(request.Email.ToLower(), request.Password);
            if (user == null)
                return BadRequest("Invalid login or password");
            var token = jwtService.GetToken(user);
            var response = new SignInResponse
            {
                UserId = user.Id,
                Token = new JwtSecurityTokenHandler().WriteToken(token)                
            };
            return Ok(response);
        }
    }
}
