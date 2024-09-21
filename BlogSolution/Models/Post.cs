using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Identity;

namespace BlogSolution.Models
{
    public class Post
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public int BlogId { get; set; }

        [ValidateNever]
        public Blog? Blog { get; set; }

        [Required]
        public string UserId { get; set; }

        [ValidateNever]
        public IdentityUser? User { get; set; }

        // Navigasjonsegenskaper
        [ValidateNever]
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }
}