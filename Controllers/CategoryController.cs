using CRUD_Api.Data;
using CRUD_Api.Model.DTOs;
using CRUD_Api.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CRUD_Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CategoryController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public CategoryController(ApplicationDbContext db)
        {
            _db = db;
        }

        // Get current user's accessible categories
        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var userId = GetCurrentUserId();
                var userRole = GetCurrentUserRole();

                if (userId == null)
                    return Unauthorized(new { message = "Invalid user token" });

                List<CategoryResponse> categories;

                if (userRole == "admin")
                {
                    // Admin can see all categories
                    categories = await _db.Categories
                        .Include(c => c.CreatedBy)
                        .Select(c => new CategoryResponse
                        {
                            Id = c.Id,
                            Name = c.Name,
                            Content = c.Content,
                            Link = c.Link,
                            CreatedAt = c.CreatedAt,
                            UpdatedAt = c.UpdatedAt,
                            CreatedByUsername = c.CreatedBy!.Username,
                            CreatedByUserId = c.CreatedByUserId
                        })
                        .ToListAsync();
                }
                else
                {
                    // Regular users can only see categories they have permission for
                    categories = await _db.Categories
                        .Include(c => c.CreatedBy)
                        .Where(c => c.UserPermissions.Any(up => up.UserId == userId))
                        .Select(c => new CategoryResponse
                        {
                            Id = c.Id,
                            Name = c.Name,
                            Content = c.Content,
                            Link = c.Link,
                            CreatedAt = c.CreatedAt,
                            UpdatedAt = c.UpdatedAt,
                            CreatedByUsername = c.CreatedBy!.Username,
                            CreatedByUserId = c.CreatedByUserId
                        })
                        .ToListAsync();
                }

                return Ok(categories);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        // Get category by ID
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCategory(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var userRole = GetCurrentUserRole();

                if (userId == null)
                    return Unauthorized(new { message = "Invalid user token" });

                var category = await _db.Categories
                    .Include(c => c.CreatedBy)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (category == null)
                    return NotFound(new { message = "Category not found" });

                // Check permissions
                if (userRole != "admin")
                {
                    var hasPermission = await _db.UserCategoryPermissions
                        .AnyAsync(up => up.UserId == userId && up.CategoryId == id);

                    if (!hasPermission)
                        return Forbid("You don't have permission to access this category");
                }

                var response = new CategoryResponse
                {
                    Id = category.Id,
                    Name = category.Name,
                    Content = category.Content,
                    Link = category.Link,
                    CreatedAt = category.CreatedAt,
                    UpdatedAt = category.UpdatedAt,
                    CreatedByUsername = category.CreatedBy?.Username ?? "Unknown",
                    CreatedByUserId = category.CreatedByUserId
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        // Create new category (Admin only)
        [HttpPost]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var userRole = GetCurrentUserRole();

                if (userId == null)
                    return Unauthorized(new { message = "Invalid user token" });

                if (userRole != "admin")
                    return Forbid("Only administrators can create categories");

                // Check if category name already exists
                if (await _db.Categories.AnyAsync(c => c.Name.ToLower() == request.Name.ToLower()))
                    return BadRequest(new { message = "Category name already exists" });

                var category = new Category
                {
                    Name = request.Name,
                    Content = request.Content,
                    Link = request.Link,
                    CreatedByUserId = userId.Value,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Categories.Add(category);
                await _db.SaveChangesAsync();

                return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, new { message = "Category created successfully", categoryId = category.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        // Update category (Admin only)
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateCategoryRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var userRole = GetCurrentUserRole();

                if (userId == null)
                    return Unauthorized(new { message = "Invalid user token" });

                if (userRole != "admin")
                    return Forbid("Only administrators can update categories");

                var category = await _db.Categories.FindAsync(id);
                if (category == null)
                    return NotFound(new { message = "Category not found" });

                // Check if new name already exists (excluding current category)
                if (!string.IsNullOrEmpty(request.Name) && request.Name != category.Name)
                {
                    if (await _db.Categories.AnyAsync(c => c.Name.ToLower() == request.Name.ToLower() && c.Id != id))
                        return BadRequest(new { message = "Category name already exists" });
                    category.Name = request.Name;
                }

                if (!string.IsNullOrEmpty(request.Content))
                    category.Content = request.Content;

                if (request.Link != null)
                    category.Link = request.Link;

                category.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                return Ok(new { message = "Category updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        // Delete category (Admin only)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            try
            {
                var userRole = GetCurrentUserRole();

                if (userRole != "admin")
                    return Forbid("Only administrators can delete categories");

                var category = await _db.Categories
                    .Include(c => c.UserPermissions)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (category == null)
                    return NotFound(new { message = "Category not found" });

                // Remove all permissions for this category
                _db.UserCategoryPermissions.RemoveRange(category.UserPermissions);

                // Remove the category
                _db.Categories.Remove(category);

                await _db.SaveChangesAsync();

                return Ok(new { message = "Category deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        // Grant permissions to user (Admin only)
        [HttpPost("grant-permission")]
        public async Task<IActionResult> GrantPermission([FromBody] GrantPermissionRequest request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var userRole = GetCurrentUserRole();

                if (currentUserId == null)
                    return Unauthorized(new { message = "Invalid user token" });

                if (userRole != "admin")
                    return Forbid("Only administrators can grant permissions");

                // Check if target user exists
                var targetUser = await _db.Users.FindAsync(request.UserId);
                if (targetUser == null)
                    return NotFound(new { message = "Target user not found" });

                // Check if all categories exist
                var existingCategories = await _db.Categories
                    .Where(c => request.CategoryIds.Contains(c.Id))
                    .Select(c => c.Id)
                    .ToListAsync();

                var nonExistentCategories = request.CategoryIds.Except(existingCategories).ToList();
                if (nonExistentCategories.Any())
                    return BadRequest(new { message = $"Categories not found: {string.Join(", ", nonExistentCategories)}" });

                // Remove existing permissions for this user
                var existingPermissions = await _db.UserCategoryPermissions
                    .Where(up => up.UserId == request.UserId)
                    .ToListAsync();

                _db.UserCategoryPermissions.RemoveRange(existingPermissions);

                // Add new permissions
                var newPermissions = request.CategoryIds.Select(categoryId => new UserCategoryPermission
                {
                    UserId = request.UserId,
                    CategoryId = categoryId,
                    GrantedByUserId = currentUserId.Value,
                    GrantedAt = DateTime.UtcNow
                }).ToList();

                _db.UserCategoryPermissions.AddRange(newPermissions);

                await _db.SaveChangesAsync();

                return Ok(new { message = $"Permissions granted successfully to {targetUser.Username}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        // Revoke specific category permission from user (Admin only)
        [HttpDelete("revoke-permission/{userId}/{categoryId}")]
        public async Task<IActionResult> RevokePermission(int userId, int categoryId)
        {
            try
            {
                var userRole = GetCurrentUserRole();

                if (userRole != "admin")
                    return Forbid("Only administrators can revoke permissions");

                var permission = await _db.UserCategoryPermissions
                    .FirstOrDefaultAsync(up => up.UserId == userId && up.CategoryId == categoryId);

                if (permission == null)
                    return NotFound(new { message = "Permission not found" });

                _db.UserCategoryPermissions.Remove(permission);
                await _db.SaveChangesAsync();

                return Ok(new { message = "Permission revoked successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        // Get all user permissions (Admin only)
        [HttpGet("user-permissions")]
        public async Task<IActionResult> GetAllUserPermissions()
        {
            try
            {
                var userRole = GetCurrentUserRole();

                if (userRole != "admin")
                    return Forbid("Only administrators can view user permissions");

                var userPermissions = await _db.Users
                    .Where(u => u.Role != "admin")
                    .Select(u => new UserPermissionResponse
                    {
                        UserId = u.Id,
                        Username = u.Username,
                        Email = u.Email,
                        Permissions = u.Id == 0 ? new List<CategoryPermissionInfo>() :
                            _db.UserCategoryPermissions
                                .Where(up => up.UserId == u.Id)
                                .Include(up => up.Category)
                                .Include(up => up.GrantedBy)
                                .Select(up => new CategoryPermissionInfo
                                {
                                    CategoryId = up.CategoryId,
                                    CategoryName = up.Category!.Name,
                                    GrantedAt = up.GrantedAt,
                                    GrantedByUsername = up.GrantedBy!.Username
                                })
                                .ToList()
                    })
                    .ToListAsync();

                // Fix for the EF Core limitation - load permissions separately
                foreach (var userPermission in userPermissions)
                {
                    userPermission.Permissions = await _db.UserCategoryPermissions
                        .Where(up => up.UserId == userPermission.UserId)
                        .Include(up => up.Category)
                        .Include(up => up.GrantedBy)
                        .Select(up => new CategoryPermissionInfo
                        {
                            CategoryId = up.CategoryId,
                            CategoryName = up.Category!.Name,
                            GrantedAt = up.GrantedAt,
                            GrantedByUsername = up.GrantedBy!.Username
                        })
                        .ToListAsync();
                }

                return Ok(userPermissions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        // Get permissions for specific user (Admin only)
        [HttpGet("user-permissions/{userId}")]
        public async Task<IActionResult> GetUserPermissions(int userId)
        {
            try
            {
                var userRole = GetCurrentUserRole();

                if (userRole != "admin")
                    return Forbid("Only administrators can view user permissions");

                var user = await _db.Users.FindAsync(userId);
                if (user == null)
                    return NotFound(new { message = "User not found" });

                var permissions = await _db.UserCategoryPermissions
                    .Where(up => up.UserId == userId)
                    .Include(up => up.Category)
                    .Include(up => up.GrantedBy)
                    .Select(up => new CategoryPermissionInfo
                    {
                        CategoryId = up.CategoryId,
                        CategoryName = up.Category!.Name,
                        GrantedAt = up.GrantedAt,
                        GrantedByUsername = up.GrantedBy!.Username
                    })
                    .ToListAsync();

                var response = new UserPermissionResponse
                {
                    UserId = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    Permissions = permissions
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        private int? GetCurrentUserId()
        {
            var uidClaim = User.FindFirst("uid")?.Value ??
                          User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                          User.FindFirst("sub")?.Value ??
                          User.FindFirst("id")?.Value;

            return int.TryParse(uidClaim, out var uid) ? uid : null;
        }

        private string? GetCurrentUserRole()
        {
            return User.FindFirst("role")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;
        }
    }
}