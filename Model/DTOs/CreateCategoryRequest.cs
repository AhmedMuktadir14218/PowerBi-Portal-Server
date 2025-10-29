using System.ComponentModel.DataAnnotations;

namespace CRUD_Api.Model.DTOs
{
    public class CreateCategoryRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public string? Link { get; set; }
    }
}