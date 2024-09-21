using System.ComponentModel.DataAnnotations;

namespace BlogSolution.Models
{
    public class PostCreateViewModel
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        // Valgfritt: Kan inkludere BlogId hvis du vil knytte til en spesifikk blogg via skjemaet
        public int BlogId { get; set; }
    }
}