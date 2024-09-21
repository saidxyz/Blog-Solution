using BlogSolution.Data;
using BlogSolution.Models;
using BlogSolution.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Linq;

namespace BlogSolution.Controllers
{
    [Authorize] // Ingen rollebegrensning
    public class CommentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<CommentController> _logger;
        private readonly IAuthorizationService _authorizationService;

        public CommentController(ApplicationDbContext context, 
                                 UserManager<IdentityUser> userManager, 
                                 ILogger<CommentController> logger,
                                 IAuthorizationService authorizationService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _authorizationService = authorizationService;
        }

        // GET: Comment/Create
        public IActionResult Create(int postId)
        {
            var model = new CommentCreateViewModel
            {
                PostId = postId
            };
            return View(model);
        }

        // POST: Comment/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CommentCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("User not found while creating comment.");
                    return RedirectToAction("Login", "Account");
                }

                var comment = new Comment
                {
                    Content = model.Content,
                    PostId = model.PostId,
                    UserId = user.Id,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Comments.Add(comment);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Comment created by user {UserId} on Post {PostId}", user.Id, model.PostId);
                return RedirectToAction("Details", "Post", new { id = model.PostId });
            }

            return View(model);
        }

        // GET: Comment/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Edit action called with null id");
                return NotFound();
            }

            var comment = await _context.Comments.FindAsync(id);
            if (comment == null)
            {
                _logger.LogWarning("Comment with id {CommentId} not found", id);
                return NotFound();
            }

            var authorizationResult = await _authorizationService.AuthorizeAsync(User, comment, "IsCommentOwner");
            if (!authorizationResult.Succeeded)
            {
                _logger.LogWarning("User {UserId} unauthorized to edit Comment {CommentId}", _userManager.GetUserId(User), id);
                return Forbid();
            }

            var model = new CommentEditViewModel
            {
                Content = comment.Content,
                PostId = comment.PostId
            };

            return View(model);
        }

        // POST: Comment/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CommentEditViewModel model)
        {
            if (id <= 0)
            {
                _logger.LogWarning("Edit action called with invalid id {CommentId}", id);
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var comment = await _context.Comments.FindAsync(id);
                if (comment == null)
                {
                    _logger.LogWarning("Comment with id {CommentId} not found during edit", id);
                    return NotFound();
                }

                var authorizationResult = await _authorizationService.AuthorizeAsync(User, comment, "IsCommentOwner");
                if (!authorizationResult.Succeeded)
                {
                    _logger.LogWarning("User {UserId} unauthorized to edit Comment {CommentId}", _userManager.GetUserId(User), id);
                    return Forbid();
                }

                try
                {
                    comment.Content = model.Content;
                    _context.Update(comment);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Comment '{CommentId}' edited by user {UserId}", comment.Id, _userManager.GetUserId(User));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CommentExists(id))
                    {
                        _logger.LogWarning("Comment with id {CommentId} does not exist during concurrency check", id);
                        return NotFound();
                    }
                    else
                    {
                        _logger.LogError("Concurrency error while editing Comment {CommentId}", id);
                        throw;
                    }
                }
                return RedirectToAction("Details", "Post", new { id = comment.PostId });
            }

            _logger.LogWarning("Invalid model state for editing comment");
            return View(model);
        }

        // GET: Comment/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Delete action called with null id");
                return NotFound();
            }

            var comment = await _context.Comments
                .Include(c => c.Post)
                .Include(c => c.User)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (comment == null)
            {
                _logger.LogWarning("Comment with id {CommentId} not found", id);
                return NotFound();
            }

            var authorizationResult = await _authorizationService.AuthorizeAsync(User, comment, "IsCommentOwner");
            if (!authorizationResult.Succeeded)
            {
                _logger.LogWarning("User {UserId} unauthorized to delete Comment {CommentId}", _userManager.GetUserId(User), id);
                return Forbid();
            }

            return View(comment);
        }

        // POST: Comment/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var comment = await _context.Comments.FindAsync(id);
            if (comment == null)
            {
                _logger.LogWarning("Comment with id {CommentId} not found during delete", id);
                return NotFound();
            }

            var authorizationResult = await _authorizationService.AuthorizeAsync(User, comment, "IsCommentOwner");
            if (!authorizationResult.Succeeded)
            {
                _logger.LogWarning("User {UserId} unauthorized to delete Comment {CommentId}", _userManager.GetUserId(User), id);
                return Forbid();
            }

            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Comment '{CommentId}' deleted by user {UserId}", comment.Id, _userManager.GetUserId(User));
            return RedirectToAction("Details", "Post", new { id = comment.PostId });
        }

        // Hjelpe-metode for å sjekke om en kommentar eksisterer
        private bool CommentExists(int id)
        {
            return _context.Comments.Any(e => e.Id == id);
        }
    }
}
