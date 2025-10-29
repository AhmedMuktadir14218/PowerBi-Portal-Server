namespace CRUD_Api.Model.DTOs
{
    public class UserPermissionResponse
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<CategoryPermissionInfo> Permissions { get; set; } = new List<CategoryPermissionInfo>();
    }

    public class CategoryPermissionInfo
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public DateTime GrantedAt { get; set; }
        public string GrantedByUsername { get; set; } = string.Empty;
    }
}