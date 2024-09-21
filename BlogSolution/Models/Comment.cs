using Microsoft.AspNetCore.Identity;
using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BlogSolution.Models
{
    public class Comment
    {
        public int Id { get; set; }

        [Required]
        [StringLength(500)]
        public string Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Fremmednøkkel til Post
        [Required]
        public int PostId { get; set; }

        [ValidateNever]
        public Post? Post { get; set; }

        // Fremmednøkkel til brukeren som opprettet kommentaren
        [Required]
        public string UserId { get; set; }

        [ValidateNever]
        public IdentityUser? User { get; set; }
    }
}