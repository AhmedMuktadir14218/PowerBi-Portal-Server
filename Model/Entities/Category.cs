using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRUD_Api.Model.Entities
{
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        [Required]
        public int CreatedByUserId { get; set; }

        public string? Link { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation property
        [ForeignKey("CreatedByUserId")]
        public User? CreatedBy { get; set; }

        // Navigation property for permissions
        public virtual ICollection<UserCategoryPermission> UserPermissions { get; set; } = new List<UserCategoryPermission>();
    }
}