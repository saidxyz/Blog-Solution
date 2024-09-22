using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BlogSolution.Models
{
    public class Blog
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; }
        
        [StringLength(1000)] 
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Fremmednøkkel til eieren (brukeren)
        [Required]
        public string UserId { get; set; }

        [ValidateNever]
        public IdentityUser? User { get; set; }

        // Navigasjonsegenskaper
        [ValidateNever]
        public ICollection<Post> Posts { get; set; } = new List<Post>();
    }
}