// ViewModels/CommentEditViewModel.cs

using System.ComponentModel.DataAnnotations;

namespace BlogSolution.ViewModels
{
    public class CommentEditViewModel
    {
        [Required]
        [Display(Name = "Comment")]
        public string Content { get; set; }

        public int PostId { get; set; }
    }
}