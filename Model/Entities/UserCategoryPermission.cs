using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRUD_Api.Model.Entities
{
    public class UserCategoryPermission
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int CategoryId { get; set; }

        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

        public int GrantedByUserId { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        public User? User { get; set; }

        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }

        [ForeignKey("GrantedByUserId")]
        public User? GrantedBy { get; set; }
    }
}