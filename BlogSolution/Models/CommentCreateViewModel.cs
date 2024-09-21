using System.ComponentModel.DataAnnotations;

namespace BlogSolution.Models
{
    public class CommentCreateViewModel
    {
        [Required]
        [StringLength(500)]
        public string Content { get; set; }

        [Required]
        public int PostId { get; set; }
    }
}