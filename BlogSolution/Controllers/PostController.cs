using BlogSolution.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Linq;

namespace BlogSolution.Controllers
{
    [Authorize]
    public class PostController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<PostController> _logger;

        public PostController(ApplicationDbContext context, UserManager<IdentityUser> userManager, ILogger<PostController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: Post/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Details action called with null id");
                return NotFound();
            }
            _logger.LogInformation("Fetching Post with id {PostId}", id);
            var post = await _context.Posts
                .Include(p => p.Blog)
                .Include(p => p.User)
                .Include(p => p.Comments)
                .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (post == null)
            {
                _logger.LogWarning("Post with id {PostId} not found", id);
                return NotFound();
            }

            _logger.LogInformation("Post found with id {PostId}. Number of comments: {CommentCount}", id, post.Comments.Count);
            return View(post);
        }

        // GET: Post/Create?blogId=1
        public IActionResult Create(int blogId)
        {
            ViewBag.BlogId = blogId;
            _logger.LogInformation("Navigated to Create Post view for BlogId {BlogId}", blogId);
            return View();
        }

        // POST: Post/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PostCreateViewModel model)
        {
            _logger.LogDebug("Entering Post Create POST action");

            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("User not found while creating post.");
                    return RedirectToAction("Login", "Account");
                }

                var post = new Post
                {
                    Title = model.Title,
                    Content = model.Content,
                    BlogId = model.BlogId,
                    UserId = user.Id,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Add(post);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Post '{PostTitle}' created by user {UserId} in BlogId {BlogId}", post.Title, user.Id, post.BlogId);
                return RedirectToAction("Details", "Blog", new { id = post.BlogId });
            }

            _logger.LogWarning("Invalid model state for creating post");
            ViewBag.BlogId = model.BlogId;
            return View(model);
        }

        // GET: Post/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Edit action called with null id");
                return NotFound();
            }

            var post = await _context.Posts.FindAsync(id);
            if (post == null)
            {
                _logger.LogWarning("Post with id {PostId} not found", id);
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (post.UserId != user.Id)
            {
                _logger.LogWarning("User {UserId} unauthorized to edit Post {PostId}", user.Id, id);
                return Forbid();
            }

            var model = new PostCreateViewModel
            {
                Title = post.Title,
                Content = post.Content,
                BlogId = post.BlogId
            };

            return View(model);
        }

        // POST: Post/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PostCreateViewModel model)
        {
            if (id <= 0)
            {
                _logger.LogWarning("Edit action called with invalid id {PostId}", id);
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var post = await _context.Posts.FindAsync(id);
                    if (post == null)
                    {
                        _logger.LogWarning("Post with id {PostId} not found during edit", id);
                        return NotFound();
                    }

                    var user = await _userManager.GetUserAsync(User);
                    if (post.UserId != user.Id)
                    {
                        _logger.LogWarning("User {UserId} unauthorized to edit Post {PostId}", user.Id, id);
                        return Forbid();
                    }

                    post.Title = model.Title;
                    post.Content = model.Content;

                    _context.Update(post);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Post '{PostTitle}' edited by user {UserId}", post.Title, user.Id);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PostExists(id))
                    {
                        _logger.LogWarning("Post with id {PostId} does not exist during concurrency check", id);
                        return NotFound();
                    }
                    else
                    {
                        _logger.LogError("Concurrency error while editing Post {PostId}", id);
                        throw;
                    }
                }
                return RedirectToAction("Details", "Blog", new { id = model.BlogId });
            }

            _logger.LogWarning("Invalid model state for editing post");
            return View(model);
        }

        // GET: Post/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Delete action called with null id");
                return NotFound();
            }

            var post = await _context.Posts
                .Include(p => p.Blog)
                .Include(p => p.User)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (post == null)
            {
                _logger.LogWarning("Post with id {PostId} not found", id);
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (post.UserId != user.Id)
            {
                _logger.LogWarning("User {UserId} unauthorized to delete Post {PostId}", user.Id, id);
                return Forbid();
            }

            return View(post);
        }

        // POST: Post/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var post = await _context.Posts.FindAsync(id);
            if (post == null)
            {
                _logger.LogWarning("Post with id {PostId} not found during delete", id);
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (post.UserId != user.Id)
            {
                _logger.LogWarning("User {UserId} unauthorized to delete Post {PostId}", user.Id, id);
                return Forbid();
            }

            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Post '{PostTitle}' deleted by user {UserId}", post.Title, user.Id);
            return RedirectToAction("Details", "Blog", new { id = post.BlogId });
        }

        // Hjelpe-metode for å sjekke om en post eksisterer
        private bool PostExists(int id)
        {
            return _context.Posts.Any(e => e.Id == id);
        }
    }
}
