using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using BlogSolution.Controllers;
using BlogSolution.Data;
using BlogSolution.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.Linq;


namespace BlogSolution.Tests.Controllers
{
    public class BlogControllerTests:IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<UserManager<IdentityUser>> _userManagerMock;
        private readonly Mock<ILogger<BlogController>> _loggerMock;
        private readonly Mock<IAuthorizationService> _authorizationServiceMock;
        private readonly DbContextOptions<ApplicationDbContext> _options;
        
        public BlogControllerTests()
        {
            _options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique database name for isolation
                .Options;

            _context = new ApplicationDbContext(_options);

            _userManagerMock = new Mock<UserManager<IdentityUser>>(
                Mock.Of<IUserStore<IdentityUser>>(), null, null, null, null, null, null, null, null);

            _loggerMock = new Mock<ILogger<BlogController>>();
            _authorizationServiceMock = new Mock<IAuthorizationService>();
        }
        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private ClaimsPrincipal GetUserPrincipal(string userId, string userName)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, userName)
            };
            var identity = new ClaimsIdentity(claims, "TestAuthentication");
            return new ClaimsPrincipal(identity);
        }

        [Fact]
        public async Task Index_ReturnsViewWithListOfBlogs()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = "user-id",
                UserName = "user@example.com",
                Email = "user@example.com"
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var blogs = new List<Blog>
            {
                new Blog { Id = 1, Title = "Blog 1", Description = "Description 1", UserId = user.Id },
                new Blog { Id = 2, Title = "Blog 2", Description = "Description 2", UserId = user.Id }
            };
            _context.Blogs.AddRange(blogs);
            await _context.SaveChangesAsync();

            var controller = new BlogController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object);

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<List<Blog>>(viewResult.Model);
            Assert.Equal(2, model.Count);
            Assert.Equal("Blog 1", model[0].Title);
            Assert.Equal("Blog 2", model[1].Title);
        }

        [Fact]
        public void Create_Get_ReturnsView()
        {
            // Arrange
            var controller = new BlogController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object);

            // Act
            var result = controller.Create();

            // Assert
            Assert.IsType<ViewResult>(result);
        }
        
        [Fact]
        public async Task Details_ValidId_ReturnsViewWithBlog()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = "user-id",
                UserName = "user@example.com",
                Email = "user@example.com"
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var blog = new Blog
            {
                Id = 1,
                Title = "Test Blog",
                Description = "A blog for testing.",
                UserId = user.Id
            };
            _context.Blogs.Add(blog);
            await _context.SaveChangesAsync();

            var controller = new BlogController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object);

            // Act
            var result = await controller.Details(blog.Id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Blog>(viewResult.Model);
            Assert.Equal(blog.Id, model.Id);
            Assert.Equal(blog.Title, model.Title);
        }
        
        /// <summary>
        /// Tester at Create (POST) med gyldig modell og bruker oppretter en blogg og omdirigerer til Index.
        /// </summary>
        [Fact]
        public async Task Create_Post_ValidModel_UserFound_CreatesBlogAndRedirects()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = "user-id",
                UserName = "testuser",
                Email = "testuser@example.com"
            };

            // Mock UserManager to return the user
            _userManagerMock.Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            var model = new Models.BlogCreateViewModel
            {
                Title = "Test Blog",
                Description = "En testblogg"
            };

            var controller = new BlogController(
                _context,
                _userManagerMock.Object,
                _loggerMock.Object,
                _authorizationServiceMock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = GetUserPrincipal(user.Id, user.UserName) }
                }
            };

            // Act
            var result = await controller.Create(model);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(BlogController.Index), redirectResult.ActionName);

            // Verify that the blog was added to the database
            var blog = await _context.Blogs.FirstOrDefaultAsync(b => b.Title == "Test Blog");
            Assert.NotNull(blog);
            Assert.Equal("En testblogg", blog.Description); // Updated assertion
            Assert.Equal(user.Id, blog.UserId);
        }
        
        /// <summary>
        /// Tester at Create (POST) med gyldig modell men bruker ikke funnet omdirigerer til Login.
        /// </summary>
        [Fact]
        public async Task Create_Post_ValidModel_UserNotFound_RedirectsToLogin()
        {
            // Arrange
            // Mock UserManager til å returnere null (bruker ikke funnet)
            _userManagerMock.Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync((IdentityUser)null);

            var model = new Models.BlogCreateViewModel
            {
                Title = "Test Blog",
                Description = "En testblogg"
            };

            var controller = new BlogController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
                }
            };

            // Act
            var result = await controller.Create(model);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Login", redirectResult.ActionName);
            Assert.Equal("Account", redirectResult.ControllerName);
        }
        
        /// <summary>
        /// Tester at Create (POST) med ugyldig modell returnerer View med modellen.
        /// </summary>
        [Fact]
        public async Task Create_Post_InvalidModel_ReturnsViewWithModel()
        {
            // Arrange
            var controller = new BlogController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object);

            // Legg til ModelState-feil for å simulere ugyldig modell
            controller.ModelState.AddModelError("Title", "Tittel er påkrevd");

            var model = new Models.BlogCreateViewModel
            {
                Description = "En testblogg uten tittel"
            };

            // Act
            var result = await controller.Create(model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var returnedModel = Assert.IsType<BlogSolution.Models.BlogCreateViewModel>(viewResult.Model);
            Assert.Equal(model.Description, returnedModel.Description);
        }
        [Fact]
        public async Task Details_NullId_ReturnsNotFound()
        {
            // Arrange
            var controller = new BlogController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object);

            // Act
            var result = await controller.Details(null);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_InvalidId_ReturnsNotFound()
        {
            // Arrange
            var controller = new BlogController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object);

            // Act
            var result = await controller.Details(999); // Antatt ugyldig ID

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
    }

    // Antatt modell for BlogCreateViewModel
    public class BlogCreateViewModel
    {
        public string Title { get; set; }
        public string Description { get; set; }
    }
}
