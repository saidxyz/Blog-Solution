using System.ComponentModel.DataAnnotations;

namespace BlogSolution.Models
{
    public class BlogCreateViewModel
    {
        [Required]
        [StringLength(100)]
        public string Title { get; set; }

        public string? Description { get; set; }
    }
}