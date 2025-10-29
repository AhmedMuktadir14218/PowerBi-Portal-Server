// Add this to your DTOs folder
public class UpdateUserRequest
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? Password { get; set; }
    public string? Role { get; set; } // Only for admin updates
}