using BlogSolution.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlogSolution.Controllers
{
    [Authorize]
    public class BlogController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<BlogController> _logger;

        public BlogController(ApplicationDbContext context, UserManager<IdentityUser> userManager, ILogger<BlogController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: Blog
        public async Task<IActionResult> Index()
        {
            var blogs = await _context.Blogs
                .Include(b => b.User)
                .ToListAsync();
            _logger.LogInformation("Index action called");
            return View(blogs);
        }

        // GET: Blog/Create
        public IActionResult Create()
        {
            _logger.LogInformation("Create GET action called");
            return View();
        }

        // POST: Blog/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BlogCreateViewModel model)
        {
            _logger.LogDebug("Entering Create POST action");

            if (ModelState.IsValid)
            {
                _logger.LogDebug("ModelState is valid");

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("User not found.");
                    return RedirectToAction("Login", "Account");
                }

                var blog = new Blog
                {
                    Title = model.Title,
                    Description = model.Description,
                    UserId = user.Id
                };

                _context.Add(blog);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Blog created by user {UserId}", user.Id);
                return RedirectToAction(nameof(Index));
            }

            _logger.LogWarning("Invalid model state for creating blog");
            return View(model);
        }

        // GET: Blog/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("Details action called with null id");
                return NotFound();
            }

            var blog = await _context.Blogs
                .Include(b => b.User)
                .Include(b => b.Posts)
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (blog == null)
            {
                _logger.LogWarning("Blog with id {BlogId} not found", id);
                return NotFound();
            }

            _logger.LogInformation("Details action called for BlogId {BlogId}", id);
            return View(blog);
        }
    }
}
