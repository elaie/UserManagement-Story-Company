using Asp.Versioning;
using Azure;
using Azure.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SharedLibrary.Configurations;
using SharedLibrary.Models.Authentication.Login;
using SharedLibrary.Models.Login;
using SharedLibrary.Models.RefreshToken;
using SharedLibrary.Models.Sign_Up;
using SharedLibrary.Models.SignUp;
using SharedLibrary.Models.User;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace GymSync.Server.Controllers.Authentication
{
    [Route("api/v{version:apiVersion}/authentication")]
    [ApiVersion("1.0")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AppDbContext _context;
        public AuthenticationController(UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            SignInManager<IdentityUser> signInManager,
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            AppDbContext context)
        {
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _context = context;
        }

        [HttpPost]
        [Route("register")]
        public async Task<IActionResult> Register([FromBody] RegisterUser registerUser, string role)
        {
            var userExist = await _userManager.FindByEmailAsync(registerUser.Email);
            if (userExist != null)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                        new Response { Status = "Error", Message = "User already exists!" });
            }

            IdentityUser user = new()
            {
                Email = registerUser.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = registerUser.Username
            };
            if (await _roleManager.RoleExistsAsync(role))
            {
                var result = await _userManager.CreateAsync(user, registerUser.Password);
                if (!result.Succeeded)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError,
                            new Response { Status = "Error", Message = "User Failed to Create" });
                }

                await _userManager.AddToRoleAsync(user, role);


               


                return StatusCode(StatusCodes.Status200OK,
                  new Response { Status = "Success", Message = $"User created SuccessFully" });
            }
            else
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                           new Response { Status = "Error", Message = "This Role Does not Exist" });
            }
        }

      

      //  [HttpGet("confirm-email")]
      //  public async Task<IActionResult> ConfirmEmail(string token, string email)
      //  {
      //      var user = await _userManager.FindByEmailAsync(email);
      //      if (user != null)
      //      {
      //          var result = await _userManager.ConfirmEmailAsync(user, token);
      //          if (result.Succeeded)
      //          {
      //              var isInRole = await _userManager.IsInRoleAsync(user, "Owner");
      //              if (isInRole)
      //              {
      //                  var gymOwner = new CreateGymOwnerDTO(user.Id);
      //                  var newGymOwner = _mapper.Map<GymOwnerModel>(gymOwner);
      //                  _context.GymOwnerContext.Add(newGymOwner);
      //                  await _context.SaveChangesAsync();
      //              }
      //              return StatusCode(StatusCodes.Status200OK,
      //new Response { Status = "Success", Message = "Email Verified Successfully" });
      //          }
      //      }
      //      return StatusCode(StatusCodes.Status500InternalServerError,
      //                 new Response { Status = "Error", Message = "This User Does not exist!" });
      //  }

        [HttpPost]
        [Route("login")]
        public async Task<IActionResult> Login([FromBody] Login loginModel)
        {
            var user = await _userManager.FindByNameAsync(loginModel.Email)
                       ?? await _userManager.FindByEmailAsync(loginModel.Email);

            if (user == null || !await _userManager.CheckPasswordAsync(user, loginModel.Password))
            {
                return Unauthorized();
            }


            var jti = Guid.NewGuid().ToString();

            var accessToken = await GetToken(user.Id, jti);
            var refreshToken = GenerateRefreshToken();
            await SaveRefreshToken(user.Id, refreshToken, jti);

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(accessToken),
                expiration = accessToken.ValidTo,
                refreshToken = refreshToken,
                userId = user.Id,
                userName = user.UserName,
                userEmail = user.Email,
            });
        }

        [HttpGet]
        [Route("getallusers")]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetAllUsers()
        {
            var users = await _context.Users
                .AsNoTracking()
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserDto updatedUser)
        {


            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound();

            // Only update if data is provided
            if (!string.IsNullOrWhiteSpace(updatedUser.UserName))
                user.UserName = updatedUser.UserName;

            if (!string.IsNullOrWhiteSpace(updatedUser.Email))
                user.Email = updatedUser.Email; 
            
            if (!string.IsNullOrWhiteSpace(updatedUser.PhoneNumber))
                user.PhoneNumber = updatedUser.PhoneNumber;

            var isModified = _context.Entry(user).Properties.Any(p => p.IsModified);
            if (!isModified)
                return BadRequest("No fields to update");

            await _context.SaveChangesAsync();
            return NoContent();
        }


        [HttpPost]
        [Route("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] TokenApiModel model)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(model.AccessToken);
                var currentJti = jwtToken.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Jti)?.Value;
                var refreshToken = await _context.RefreshTokens.FirstOrDefaultAsync(x =>
                    x.Token == model.RefreshToken
                    && x.Jti == currentJti
                    && x.Revoked == null
                    && x.Expires > DateTime.UtcNow);

                if (refreshToken == null)
                {
                    return BadRequest("Invalid token");
                }
                _context.RefreshTokens.Remove(refreshToken);
                var newJti = Guid.NewGuid().ToString();

                var newAccessToken = await GetToken(refreshToken.UserId, newJti);
                var newRefreshToken = GenerateRefreshToken();
                await SaveRefreshToken(refreshToken.UserId, newRefreshToken, newJti);


                await _context.SaveChangesAsync();

                return Ok(new
                {
                    token = new JwtSecurityTokenHandler().WriteToken(newAccessToken),
                    refreshToken = newRefreshToken
                });
            }
            catch (ArgumentException ex)
            {

                return BadRequest($"Invalid token format: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Handle unexpected errors
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred: {ex.Message}");
            }
        }



        [HttpPost]
        [Route("logout")]
        public async Task<IActionResult> Logout([FromBody] string refreshToken)
        {
            var token = await _context.RefreshTokens.FirstOrDefaultAsync(x => x.Token == refreshToken);
            if (token == null) return NotFound();

            token.Revoked = DateTime.UtcNow;
            _context.Update(token);
            await _context.SaveChangesAsync();
            return Ok("Logged out succesfully");
        }




        private async Task<JwtSecurityToken> GetToken(string userId, string jti)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                throw new ArgumentException("Invalid user ID");
            }
            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, jti)
            };

            var userRoles = await _userManager.GetRolesAsync(user);
            foreach (var role in userRoles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, role));
            }

            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:ValidIssuer"],
                audience: _configuration["JWT:ValidAudience"],
                //  Issued: DateTime.Now,
                expires: DateTime.Now.AddDays(7),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
            );

            return token;
        }

        private async Task SaveRefreshToken(string userId, string refreshToken, string jti)
        {
            var refreshTokenEntity = new RefreshToken
            {
                Token = refreshToken,
                UserId = userId,
                Jti = jti,
                Expires = DateTime.UtcNow.AddDays(30),
                Created = DateTime.UtcNow
            };
            _context.RefreshTokens.Add(refreshTokenEntity);
            await _context.SaveChangesAsync();
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
                return Convert.ToBase64String(randomNumber);
            }
        }

    }
}
