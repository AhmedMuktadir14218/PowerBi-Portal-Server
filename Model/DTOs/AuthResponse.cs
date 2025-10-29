namespace CRUD_Api.Model.DTOs
{
    public class AuthResponse
    {
        public string Token { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }

        public string Role { get; set; }
    }
}
