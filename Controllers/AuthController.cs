using BCrypt.Net;
using CRUD_Api.Data;
using CRUD_Api.Model.DTOs;
using CRUD_Api.Model.Entities;
using CRUD_Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CRUD_Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly JwtService _jwt;

        public AuthController(ApplicationDbContext db, JwtService jwt)
        {
            _db = db;
            _jwt = jwt;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest model)
        {
            if (await _db.Users.AnyAsync(u => u.Username == model.Username))
                return BadRequest(new { message = "Username already taken." });

            if (await _db.Users.AnyAsync(u => u.Email == model.Email))
                return BadRequest(new { message = "Email already in use." });

            var hash = BCrypt.Net.BCrypt.HashPassword(model.Password);

            var user = new User
            {
                Username = model.Username,
                Email = model.Email,
                PasswordHash = hash,
                FullName = model.FullName,
                Role = string.IsNullOrEmpty(model.Role) ? "user" : model.Role
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Registration successful", user.Username, user.Role });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest model)
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Username == model.UsernameOrEmail || u.Email == model.UsernameOrEmail);

            if (user == null)
                return Unauthorized(new { message = "Invalid credentials" });

            var valid = BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash);
            if (!valid)
                return Unauthorized(new { message = "Invalid credentials" });

            // Generate token
            var (token, expires) = _jwt.GenerateToken(user);

            // Track login
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var ua = Request.Headers["User-Agent"].ToString();
            _db.UserLogins.Add(new UserLogin
            {
                UserId = user.Id,
                LoginTime = DateTime.UtcNow,
                IpAddress = ip,
                UserAgent = ua
            });
            await _db.SaveChangesAsync();

            var response = new AuthResponse
            {
                Token = token,
                ExpiresAt = expires,
                Role = user.Role
            };

            return Ok(response);
        }

        // Get current user profile
        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> Profile()
        {
            try
            {
                // Try different claim types that might be used for user ID
                var uidClaim = User.FindFirst("uid")?.Value ??
                              User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                              User.FindFirst("sub")?.Value ??
                              User.FindFirst("id")?.Value;

                if (string.IsNullOrEmpty(uidClaim))
                {
                    return Unauthorized(new { message = "User ID not found in token" });
                }

                if (!int.TryParse(uidClaim, out var uid))
                {
                    return Unauthorized(new { message = "Invalid user ID format" });
                }

                var user = await _db.Users.FindAsync(uid);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                return Ok(new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    user.FullName,
                    user.Role,
                    user.CreatedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        // Get login history for current user
        [Authorize]
        [HttpGet("logins")]
        public async Task<IActionResult> GetLogins()
        {
            try
            {
                var uidClaim = User.FindFirst("uid")?.Value ??
                              User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                              User.FindFirst("sub")?.Value ??
                              User.FindFirst("id")?.Value;

                if (string.IsNullOrEmpty(uidClaim))
                {
                    return Unauthorized(new { message = "User ID not found in token" });
                }

                if (!int.TryParse(uidClaim, out var uid))
                {
                    return Unauthorized(new { message = "Invalid user ID format" });
                }

                var logs = await _db.UserLogins
                            .Where(l => l.UserId == uid)
                            .OrderByDescending(l => l.LoginTime)
                            .Select(l => new
                            {
                                l.Id,
                                l.LoginTime,
                                l.IpAddress,
                                l.UserAgent
                            })
                            .ToListAsync();

                return Ok(logs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        // Update user profile
        [Authorize]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserRequest model)
        {
            try
            {
                var uidClaim = User.FindFirst("uid")?.Value ??
                              User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                              User.FindFirst("sub")?.Value ??
                              User.FindFirst("id")?.Value;

                if (string.IsNullOrEmpty(uidClaim) || !int.TryParse(uidClaim, out var uid))
                {
                    return Unauthorized(new { message = "Invalid user token" });
                }

                var user = await _db.Users.FindAsync(uid);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                // Check if username is already taken by another user
                if (!string.IsNullOrEmpty(model.Username) && model.Username != user.Username)
                {
                    if (await _db.Users.AnyAsync(u => u.Username == model.Username && u.Id != uid))
                    {
                        return BadRequest(new { message = "Username already taken" });
                    }
                    user.Username = model.Username;
                }

                // Check if email is already taken by another user
                if (!string.IsNullOrEmpty(model.Email) && model.Email != user.Email)
                {
                    if (await _db.Users.AnyAsync(u => u.Email == model.Email && u.Id != uid))
                    {
                        return BadRequest(new { message = "Email already in use" });
                    }
                    user.Email = model.Email;
                }

                // Update other fields
                if (!string.IsNullOrEmpty(model.FullName))
                    user.FullName = model.FullName;

                // Update password if provided
                if (!string.IsNullOrEmpty(model.Password))
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);
                }

                await _db.SaveChangesAsync();

                return Ok(new { message = "Profile updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        // Update user by ID (Admin only)
        [Authorize]
        [HttpPut("user/{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest model)
        {
            try
            {
                var currentUserRole = User.FindFirst("role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;

                if (currentUserRole != "admin")
                {
                    return Forbid("Only administrators can update other users");
                }

                var user = await _db.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                // Check if username is already taken by another user
                if (!string.IsNullOrEmpty(model.Username) && model.Username != user.Username)
                {
                    if (await _db.Users.AnyAsync(u => u.Username == model.Username && u.Id != id))
                    {
                        return BadRequest(new { message = "Username already taken" });
                    }
                    user.Username = model.Username;
                }

                // Check if email is already taken by another user
                if (!string.IsNullOrEmpty(model.Email) && model.Email != user.Email)
                {
                    if (await _db.Users.AnyAsync(u => u.Email == model.Email && u.Id != id))
                    {
                        return BadRequest(new { message = "Email already in use" });
                    }
                    user.Email = model.Email;
                }

                // Update other fields
                if (!string.IsNullOrEmpty(model.FullName))
                    user.FullName = model.FullName;

                if (!string.IsNullOrEmpty(model.Role))
                    user.Role = model.Role;

                // Update password if provided
                if (!string.IsNullOrEmpty(model.Password))
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);
                }

                await _db.SaveChangesAsync();

                return Ok(new { message = "User updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        // Delete user by ID (Admin only)
        [Authorize]
        [HttpDelete("user/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var currentUserRole = User.FindFirst("role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;

                if (currentUserRole != "admin")
                {
                    return Forbid("Only administrators can delete users");
                }

                var user = await _db.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                // Optional: Prevent admin from deleting themselves
                var currentUserId = User.FindFirst("uid")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(currentUserId, out var uid) && uid == id)
                {
                    return BadRequest(new { message = "You cannot delete your own account" });
                }

                // Delete related login records first (if you want to keep referential integrity)
                var userLogins = _db.UserLogins.Where(ul => ul.UserId == id);
                _db.UserLogins.RemoveRange(userLogins);

                // Delete the user
                _db.Users.Remove(user);
                await _db.SaveChangesAsync();

                return Ok(new { message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        // Get all users (Admin only)
        [Authorize]
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var currentUserRole = User.FindFirst("role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;

                if (currentUserRole != "admin")
                {
                    return Forbid("Only administrators can view all users");
                }

                var users = await _db.Users
                    .Select(u => new
                    {
                        u.Id,
                        u.Username,
                        u.Email,
                        u.FullName,
                        u.Role,
                        u.CreatedAt
                    })
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        // Get user by ID (Admin only)
        [Authorize]
        [HttpGet("user/{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            try
            {
                var currentUserRole = User.FindFirst("role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;

                if (currentUserRole != "admin")
                {
                    return Forbid("Only administrators can view other users");
                }

                var user = await _db.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                return Ok(new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    user.FullName,
                    user.Role,
                    user.CreatedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }
    }
}