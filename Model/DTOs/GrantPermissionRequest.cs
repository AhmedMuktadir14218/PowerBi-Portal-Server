using System.ComponentModel.DataAnnotations;

namespace CRUD_Api.Model.DTOs
{
    public class GrantPermissionRequest
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public List<int> CategoryIds { get; set; } = new List<int>();
    }
}