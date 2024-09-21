using System.ComponentModel.DataAnnotations;

namespace BlogSolution.ViewModels
{
    public class CommentCreateViewModel
    {
        [Required]
        [Display(Name = "Comment")]
        public string Content { get; set; }

        public int PostId { get; set; }
    }
}