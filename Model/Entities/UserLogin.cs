using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRUD_Api.Model.Entities
{
    public class UserLogin
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        public DateTime LoginTime { get; set; } = DateTime.UtcNow;

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }
    }
}
