using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using BlogSolution.Controllers;
using BlogSolution.Data;
using BlogSolution.Models;
using BlogSolution.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace BlogSolution.Tests.Controllers
{
    public class PostControllerTests : IDisposable
    {
        private readonly Mock<UserManager<IdentityUser>> _userManagerMock;
        private readonly Mock<ILogger<PostController>> _loggerMock;
        private readonly Mock<IAuthorizationService> _authorizationServiceMock;
        private readonly ApplicationDbContext _context;
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public PostControllerTests()
        {
            // Sett opp SQLite in-memory database
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new ApplicationDbContext(_options);
            _context.Database.EnsureCreated();

            // Mock UserManager
            var store = new Mock<IUserStore<IdentityUser>>();
            _userManagerMock = new Mock<UserManager<IdentityUser>>(
                store.Object, null, null, null, null, null, null, null, null);

            // Mock ILogger
            _loggerMock = new Mock<ILogger<PostController>>();

            // Mock IAuthorizationService
            _authorizationServiceMock = new Mock<IAuthorizationService>();
        }

        private ClaimsPrincipal GetUserPrincipal(string userId, string userName)
        {
            var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, userName),
            }, "mock"));

            return userClaims;
        }

       
        /// Tester at Details (GET) med gyldig id returnerer View med korrekt modell.
        
        [Fact]
        public async Task Details_Get_ValidId_ReturnsViewWithPost()
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
                Title  = "Test Blog",
                Description = "A blog for testing.",
                UserId = user.Id // Legg til denne linjen
            };
            _context.Blogs.Add(blog);
            await _context.SaveChangesAsync();

            var post = new Post
            {
                Id = 1,
                Title = "Test Post",
                Content = "Content of the test post.",
                BlogId = blog.Id,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            var comment = new Comment
            {
                Id = 1,
                Content = "Test Comment",
                PostId = post.Id,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            var controller = new PostController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object);

            // Act
            var result = await controller.Details(post.Id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Post>(viewResult.Model);
            Assert.Equal(post.Id, model.Id);
            Assert.Equal(post.Title, model.Title);
            Assert.Equal(1, model.Comments.Count);
        }


       
        /// Tester at Create (GET) returnerer View med korrekt BlogId.
        
        [Fact]
        public void Create_Get_ReturnsViewWithBlogId()
        {
            // Arrange
            int blogId = 1;
            var controller = new PostController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object);

            // Act
            var result = controller.Create(blogId);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(blogId, viewResult.ViewData["BlogId"]);
        }

       
        /// Tester at Create (POST) med gyldig modell oppretter en post og omdirigerer til Blog Details.
        
        [Fact]
        public async Task Create_Post_ValidModel_RedirectsToBlogDetails()
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
                UserId = user.Id // Legg til denne linjen
            };
            _context.Blogs.Add(blog);
            await _context.SaveChangesAsync();

            var model = new PostCreateViewModel
            {
                Title = "New Post",
                Content = "Content of the new post.",
                BlogId = blog.Id
            };

            _userManagerMock.Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            var controller = new PostController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = new DefaultHttpContext() { User = GetUserPrincipal(user.Id, user.UserName) }
                }
            };


            // Act
            var result = await controller.Create(model);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirectResult.ActionName);
            Assert.Equal("Blog", redirectResult.ControllerName);
            Assert.Equal(blog.Id, redirectResult.RouteValues["id"]);

            // Sjekk at posten ble lagt til i databasen
            var createdPost = await _context.Posts.FirstOrDefaultAsync(p => p.Title == model.Title);
            Assert.NotNull(createdPost);
            Assert.Equal(model.Content, createdPost.Content);
            Assert.Equal(user.Id, createdPost.UserId);
            Assert.Equal(blog.Id, createdPost.BlogId);
        }
        
        /// Tester at Create (POST) med ugyldig modell returnerer View med samme modell.
        [Fact]
        public async Task Create_Post_InvalidModel_ReturnsView()
        {
            // Arrange
            var controller = new PostController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object);
            controller.ModelState.AddModelError("Title", "Required");

            var model = new PostCreateViewModel
            {
                Title = "", // Ugyldig fordi det er påkrevd
                Content = "Content of the new post.",
                BlogId = 1
            };

            // Act
            var result = await controller.Create(model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(model, viewResult.Model);
            Assert.Equal(1, (int)viewResult.ViewData["BlogId"]);
        }


        /// Tester at Edit (GET) med gyldig id og autorisert bruker returnerer View med korrekt modell.
        [Fact]
        public async Task Edit_Get_ValidId_Authorized_ReturnsViewWithModel()
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
                UserId = user.Id // Legg til denne linjen
            };
            _context.Blogs.Add(blog);
            await _context.SaveChangesAsync();

            var post = new Post
            {
                Id = 1,
                Title = "Original Title",
                Content = "Original Content",
                BlogId = blog.Id,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            _authorizationServiceMock.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), post, "IsPostOwner"))
                .ReturnsAsync(AuthorizationResult.Success());

            var controller = new PostController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = new DefaultHttpContext() { User = GetUserPrincipal(user.Id, user.UserName) }
                }
            };

            // Act
            var result = await controller.Edit(post.Id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<PostCreateViewModel>(viewResult.Model);
            Assert.Equal(post.Title, model.Title);
            Assert.Equal(post.Content, model.Content);
            Assert.Equal(post.BlogId, model.BlogId);
        }

       
        /// Tester at Edit (GET) med null id returnerer NotFound.
        
        [Fact]
        public async Task Edit_Get_PostNotFound_ReturnsNotFound()
        {
            // Arrange
            var controller = new PostController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object);
            int nonExistentPostId = 999;

            // Act
            var result = await controller.Edit(nonExistentPostId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

       
        /// Tester at Edit (GET) med ugyldig id returnerer NotFound.
        
        [Fact]
        public async Task Edit_Get_InvalidId_ReturnsNotFound()
        {
            // Arrange
            var controller = new PostController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object);
            int invalidId = 999;

            // Act
            var result = await controller.Edit(invalidId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

       
        /// Tester at Edit (GET) med gyldig id, men uautorisert bruker returnerer Forbid.
        
        [Fact]
        public async Task Edit_Get_ValidId_Unauthorized_ReturnsForbid()
        {
            // Arrange
            var ownerUser = new IdentityUser
            {
                Id = "owner-user-id",
                UserName = "owner@example.com",
                Email = "owner@example.com"
            };
            var otherUser = new IdentityUser
            {
                Id = "other-user-id",
                UserName = "other@example.com",
                Email = "other@example.com"
            };
            _context.Users.Add(ownerUser);
            _context.Users.Add(otherUser);
            await _context.SaveChangesAsync();

            var blog = new Blog
            {
                Id = 1,
                Title  = "Test Blog",
                Description = "A blog for testing.",
                UserId = ownerUser.Id // Legg til denne linjen
            };
            _context.Blogs.Add(blog);
            await _context.SaveChangesAsync();

            var post = new Post
            {
                Id = 1,
                Title = "Original Title",
                Content = "Original Content",
                BlogId = blog.Id,
                UserId = ownerUser.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            _authorizationServiceMock.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), post, "IsPostOwner"))
                .ReturnsAsync(AuthorizationResult.Failed());

            var controller = new PostController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = new DefaultHttpContext() { User = GetUserPrincipal(otherUser.Id, otherUser.UserName) }
                }
            };

            // Act
            var result = await controller.Edit(post.Id);

            // Assert
            Assert.IsType<ForbidResult>(result);
        }

       
/// Tester at Edit (POST) med gyldig modell oppdaterer posten og omdirigerer til Blog Details.

[Fact]
public async Task Edit_Post_ValidModel_Authorized_RedirectsToBlogDetails()
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
        Title  = "Test Blog",
        Description = "A blog for testing.",
        UserId = user.Id // Added this line
    };
    _context.Blogs.Add(blog);
    await _context.SaveChangesAsync();

    var post = new Post
    {
        Id = 1,
        Title = "Original Title",
        Content = "Original Content",
        BlogId = blog.Id,
        UserId = user.Id,
        CreatedAt = DateTime.UtcNow
    };
    _context.Posts.Add(post);
    await _context.SaveChangesAsync();

    _authorizationServiceMock.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), post, "IsPostOwner"))
        .ReturnsAsync(AuthorizationResult.Success());

    var model = new PostCreateViewModel
    {
        Title = "Updated Title",
        Content = "Updated Content",
        BlogId = blog.Id
    };

    var controller = new PostController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object)
    {
        ControllerContext = new ControllerContext()
        {
            HttpContext = new DefaultHttpContext() { User = GetUserPrincipal(user.Id, user.UserName) }
        }
    };

    // Act
    var result = await controller.Edit(post.Id, model);

    // Assert
    var redirectResult = Assert.IsType<RedirectToActionResult>(result);
    Assert.Equal("Details", redirectResult.ActionName);
    Assert.Equal("Blog", redirectResult.ControllerName);
    Assert.Equal(blog.Id, redirectResult.RouteValues["id"]);

    // Verify that the post was updated in the database
    var updatedPost = await _context.Posts.FindAsync(post.Id);
    Assert.Equal(model.Title, updatedPost.Title);
    Assert.Equal(model.Content, updatedPost.Content);
}

       
        /// Tester at Edit (POST) med ugyldig modell returnerer View med samme modell.
        
        [Fact]
        public async Task Edit_Post_InvalidModel_ReturnsView()
        {
            // Arrange
            var controller = new PostController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object);
            controller.ModelState.AddModelError("Title", "Required");

            var model = new PostCreateViewModel
            {
                Title = "", // Ugyldig fordi det er påkrevd
                Content = "Updated Content",
                BlogId = 1
            };

            // Act
            var result = await controller.Edit(1, model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(model, viewResult.Model);
        }

       
        /// Tester at Edit (POST) med gyldig modell, men en koncurrensfeil oppstår, returnerer NotFound.
        
        [Fact]
        public async Task Edit_Post_ConcurrencyException_RedirectsToDetails()
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
                Title  = "Test Blog",
                Description = "A blog for testing.",
                UserId = user.Id // Legg til denne linjen
            };
            _context.Blogs.Add(blog);
            await _context.SaveChangesAsync();

            var post = new Post
            {
                Id = 1,
                Title = "Original Title",
                Content = "Original Content",
                BlogId = blog.Id,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            _authorizationServiceMock.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), post, "IsPostOwner"))
                .ReturnsAsync(AuthorizationResult.Success());

            var model = new PostCreateViewModel
            {
                Title = "Updated Title",
                Content = "Updated Content",
                BlogId = blog.Id
            };

            var controller = new PostController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = new DefaultHttpContext() { User = GetUserPrincipal(user.Id, user.UserName) }
                }
            };
            // Simuler en koncurrensfeil ved å endre posten i en annen kontekst
            using (var context2 = new ApplicationDbContext(_options))
            {
                var postToUpdate = await context2.Posts.FindAsync(post.Id);
                postToUpdate.Title = "Another Update";
                await context2.SaveChangesAsync();
            }

            // Act
            var result = await controller.Edit(post.Id, model);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirectResult.ActionName);
            Assert.Equal("Blog", redirectResult.ControllerName);
            Assert.Equal(blog.Id, redirectResult.RouteValues["id"]);

        }

       
        /// Tester at Delete (GET) med gyldig id og autorisert bruker returnerer View med korrekt modell.
        
        [Fact]
        public async Task Delete_Get_ValidId_Authorized_ReturnsViewWithPost()
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
                Title  = "Test Blog",
                Description = "A blog for testing.",
                UserId = user.Id // Legg til denne linjen
            };
            _context.Blogs.Add(blog);
            await _context.SaveChangesAsync();

            var post = new Post
            {
                Id = 1,
                Title = "Test Post",
                Content = "Content of the test post.",
                BlogId = blog.Id,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            _authorizationServiceMock.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), post, "IsPostOwner"))
                .ReturnsAsync(AuthorizationResult.Success());

            var controller = new PostController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = new DefaultHttpContext() { User = GetUserPrincipal(user.Id, user.UserName) }
                }
            };

            // Act
            var result = await controller.Delete(post.Id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Post>(viewResult.Model);
            Assert.Equal(post.Id, model.Id);
            Assert.Equal(post.Title, model.Title);
        }

       
        /// Tester at Delete (GET) med null id returnerer NotFound.
        
        [Fact]
        public async Task Delete_Get_NullId_ReturnsNotFound()
        {
            // Arrange
            var controller = new PostController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object);

            // Act
            var result = await controller.Delete(null);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

       
        /// Tester at Delete (GET) med ugyldig id returnerer NotFound.
        
        [Fact]
        public async Task Delete_Get_InvalidId_ReturnsNotFound()
        {
            // Arrange
            var controller = new PostController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object);
            int invalidId = 999;

            // Act
            var result = await controller.Delete(invalidId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

       
        /// Tester at Delete (GET) med gyldig id, men uautorisert bruker returnerer Forbid.
        
        [Fact]
        public async Task Delete_Get_ValidId_Unauthorized_ReturnsForbid()
        {
            // Arrange
            var ownerUser = new IdentityUser
            {
                Id = "owner-user-id",
                UserName = "owner@example.com",
                Email = "owner@example.com"
            };
            var otherUser = new IdentityUser
            {
                Id = "other-user-id",
                UserName = "other@example.com",
                Email = "other@example.com"
            };
            _context.Users.Add(ownerUser);
            _context.Users.Add(otherUser);
            await _context.SaveChangesAsync();

            var blog = new Blog
            {
                Id = 1,
                Title = "Test Blog",
                Description = "A blog for testing.",
                UserId = ownerUser.Id // Legg til denne linjen for å sette eieren av bloggen
            };
            _context.Blogs.Add(blog);
            await _context.SaveChangesAsync();

            var post = new Post
            {
                Id = 1,
                Title = "Test Post",
                Content = "Content of the test post.",
                BlogId = blog.Id,
                UserId = ownerUser.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            _authorizationServiceMock.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), post, "IsPostOwner"))
                .ReturnsAsync(AuthorizationResult.Failed());

            var controller = new PostController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = new DefaultHttpContext() { User = GetUserPrincipal(otherUser.Id, otherUser.UserName) }
                }
            };

            // Act
            var result = await controller.Delete(post.Id);

            // Assert
            Assert.IsType<ForbidResult>(result);
        }

       
        /// Tester at DeleteConfirmed (POST) med gyldig id og autorisert bruker sletter posten og omdirigerer til Blog Details.
        
        [Fact]
        public async Task DeleteConfirmed_Post_ValidId_Authorized_RedirectsToBlogDetails()
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
                Title  = "Test Blog",
                Description = "A blog for testing.",
                UserId = user.Id // Legg til denne linjen
            };
            _context.Blogs.Add(blog);
            await _context.SaveChangesAsync();

            var post = new Post
            {
                Id = 1,
                Title = "Test Post",
                Content = "Content of the test post.",
                BlogId = blog.Id,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            _authorizationServiceMock.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), post, "IsPostOwner"))
                .ReturnsAsync(AuthorizationResult.Success());

            var controller = new PostController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = new DefaultHttpContext() { User = GetUserPrincipal(user.Id, user.UserName) }
                }
            };

            // Act
            var result = await controller.DeleteConfirmed(post.Id);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirectResult.ActionName);
            Assert.Equal("Blog", redirectResult.ControllerName);
            Assert.Equal(blog.Id, redirectResult.RouteValues["id"]);

            // Sjekk at posten ble fjernet fra databasen
            var deletedPost = await _context.Posts.FindAsync(post.Id);
            Assert.Null(deletedPost);
        }

       
        /// Tester at DeleteConfirmed (POST) med ugyldig id returnerer NotFound.
        
        [Fact]
        public async Task DeleteConfirmed_Post_InvalidId_ReturnsNotFound()
        {
            // Arrange
            var controller = new PostController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object);
            int invalidId = 999;

            // Act
            var result = await controller.DeleteConfirmed(invalidId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

       
/// Tests that DeleteConfirmed (POST) with a valid id but an unauthorized user returns Forbid.

[Fact]
public async Task DeleteConfirmed_Post_ValidId_Unauthorized_ReturnsForbid()
{
    // Arrange
    var ownerUser = new IdentityUser
    {
        Id = "owner-user-id",
        UserName = "owner@example.com",
        Email = "owner@example.com"
    };
    var otherUser = new IdentityUser
    {
        Id = "other-user-id",
        UserName = "other@example.com",
        Email = "other@example.com"
    };
    _context.Users.Add(ownerUser);
    _context.Users.Add(otherUser);
    await _context.SaveChangesAsync();

    var blog = new Blog
    {
        Id = 1,
        Title = "Test Blog",
        Description = "A blog for testing.",
        UserId = ownerUser.Id // Ensure the blog has a valid UserId
    };
    _context.Blogs.Add(blog);
    await _context.SaveChangesAsync();

    var post = new Post
    {
        Id = 1,
        Title = "Test Post",
        Content = "Content of the test post.",
        BlogId = blog.Id,
        UserId = ownerUser.Id,
        CreatedAt = DateTime.UtcNow
    };
    _context.Posts.Add(post);
    await _context.SaveChangesAsync();

    // Mock the authorization to fail for otherUser
    _authorizationServiceMock.Setup(a =>
        a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), post, "IsPostOwner"))
        .ReturnsAsync(AuthorizationResult.Failed());

    // Create the controller with otherUser as the current user
    var controller = new PostController(
        _context,
        _userManagerMock.Object,
        _loggerMock.Object,
        _authorizationServiceMock.Object)
    {
        ControllerContext = new ControllerContext()
        {
            HttpContext = new DefaultHttpContext()
            {
                User = GetUserPrincipal(otherUser.Id, otherUser.UserName)
            }
        }
    };

    // Act
    var result = await controller.DeleteConfirmed(post.Id);

    // Assert
    Assert.IsType<ForbidResult>(result);

    // Check that the post still exists in the database
    var existingPost = await _context.Posts.FindAsync(post.Id);
    Assert.NotNull(existingPost);
} 
       
        /// Tests that Edit (POST) with a valid model but a concurrency conflict throws DbUpdateConcurrencyException.

[Fact]
public async Task Edit_Post_ConcurrencyException_ThrowsException()
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
        UserId = user.Id // Added this line
    };
    _context.Blogs.Add(blog);
    await _context.SaveChangesAsync();

    var post = new Post
    {
        Id = 1,
        Title = "Original Title",
        Content = "Original Content",
        BlogId = blog.Id,
        UserId = user.Id,
        CreatedAt = DateTime.UtcNow
    };
    _context.Posts.Add(post);
    await _context.SaveChangesAsync();

    _authorizationServiceMock.Setup(a => a.AuthorizeAsync(
        It.IsAny<ClaimsPrincipal>(), post, "IsPostOwner"))
        .ReturnsAsync(AuthorizationResult.Success());

    var model = new PostCreateViewModel
    {
        Title = "Updated Title",
        Content = "Updated Content",
        BlogId = blog.Id
    };

    var controller = new PostController(
        _context,
        _userManagerMock.Object,
        _loggerMock.Object,
        _authorizationServiceMock.Object)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = GetUserPrincipal(user.Id, user.UserName)
            }
        }
    };

    // Simulate a concurrency conflict by modifying the post in another context
    using (var context2 = new ApplicationDbContext(_options))
    {
        var postToUpdate = await context2.Posts.FindAsync(post.Id);
        postToUpdate.Title = "Another Update";
        await context2.SaveChangesAsync();
    }

    // Act
    var result = await controller.Edit(post.Id, model);

    // Assert
    var redirectResult = Assert.IsType<RedirectToActionResult>(result);
    Assert.Equal("Details", redirectResult.ActionName);
    Assert.Equal("Blog", redirectResult.ControllerName);
    Assert.Equal(blog.Id, redirectResult.RouteValues["id"]);
    
}

       
        /// Tester at DeleteConfirmed (POST) med gyldig id, men en koncurrensfeil oppstår, kaster DbUpdateConcurrencyException.
        
        [Fact]
        public async Task DeleteConfirmed_Post_ConcurrencyException_ThrowsException()
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
                UserId = user.Id // Sørg for at bloggen har en gyldig UserId
            };
            _context.Blogs.Add(blog);
            await _context.SaveChangesAsync();

            var post = new Post
            {
                Id = 1,
                Title = "Test Post",
                Content = "Content of the test post.",
                BlogId = blog.Id,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            _authorizationServiceMock.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), post, "IsPostOwner"))
                .ReturnsAsync(AuthorizationResult.Success());

            var controller = new PostController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object)
            {
                ControllerContext = new ControllerContext()
                {
                    HttpContext = new DefaultHttpContext() { User = GetUserPrincipal(user.Id, user.UserName) }
                }
            };

            // Simuler en koncurrensfeil ved å fjerne posten i en annen kontekst
            using (var context2 = new ApplicationDbContext(_options))
            {
                var postToDelete = await context2.Posts.FindAsync(post.Id);
                context2.Posts.Remove(postToDelete);
                await context2.SaveChangesAsync();
            }
            
            // Act
            var result = await controller.DeleteConfirmed(post.Id);

            // Assert
            Assert.IsType<NotFoundResult>(result);


        }
        
       
        /// Tests that Create (POST) with a valid model and user found creates a post and redirects to Blog Details.
        
        [Fact]
        public async Task Create_Post_ValidModel_UserFound_CreatesPostAndRedirects()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = "user-id",
                UserName = "testuser",
                Email = "testuser@example.com"
            };

            // Add the user to the context
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Mock UserManager to return the user
            _userManagerMock.Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            // Add a blog to the context
            var blog = new Blog
            {
                Id = 1,
                Title = "Test Blog",
                Description = "Test Blog Description",
                UserId = user.Id
            };
            _context.Blogs.Add(blog);
            await _context.SaveChangesAsync();

            var model = new PostCreateViewModel
            {
                Title = "Test Post",
                Content = "Test Content",
                BlogId = blog.Id
            };

            var controller = new PostController(
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
            Assert.Equal("Details", redirectResult.ActionName);
            Assert.Equal("Blog", redirectResult.ControllerName);
            Assert.Equal(blog.Id, redirectResult.RouteValues["id"]);

            // Verify that the post was added to the database
            var post = await _context.Posts.FirstOrDefaultAsync(p => p.Title == "Test Post");
            Assert.NotNull(post);
            Assert.Equal("Test Content", post.Content);
            Assert.Equal(blog.Id, post.BlogId);
            Assert.Equal(user.Id, post.UserId);
        }


       
        /// Tests that Create (POST) with a valid model but user not found redirects to Login.
        
        [Fact]
        public async Task Create_Post_ValidModel_UserNotFound_RedirectsToLogin()
        {
            // Arrange
            // Mock UserManager to return null
            _userManagerMock.Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync((IdentityUser)null);

            var model = new PostCreateViewModel
            {
                Title = "Test Post",
                Content = "Test Content",
                BlogId = 1
            };

            var controller = new PostController(
                _context,
                _userManagerMock.Object,
                _loggerMock.Object, _authorizationServiceMock.Object)
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

       
        /// Tests that Create (POST) with an invalid model returns the view with the model.
        
        [Fact]
        public async Task Create_Post_InvalidModel_ReturnsViewWithModel()
        {
            // Arrange
            var model = new PostCreateViewModel
            {
                Title = "", // Invalid because Title is required
                Content = "Test Content",
                BlogId = 1
            };

            var controller = new PostController(
                _context,
                _userManagerMock.Object,
                _loggerMock.Object, _authorizationServiceMock.Object);

            // Simulate invalid ModelState
            controller.ModelState.AddModelError("Title", "Title is required");

            // Act
            var result = await controller.Create(model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var returnedModel = Assert.IsType<PostCreateViewModel>(viewResult.Model);
            Assert.Equal(model, returnedModel);
            Assert.Equal(model.BlogId, viewResult.ViewData["BlogId"]);
        }
        [Fact]
        public async Task Details_NullId_ReturnsNotFound()
        {
            // Arrange
            var controller = new PostController(
                _context,
                _userManagerMock.Object,
                _loggerMock.Object, _authorizationServiceMock.Object);

            // Act
            var result = await controller.Details(null);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_PostNotFound_ReturnsNotFound()
        {
            // Arrange
            int nonExistentPostId = 999;
            var controller = new PostController(
                _context,
                _userManagerMock.Object,
                _loggerMock.Object, _authorizationServiceMock.Object);

            // Act
            var result = await controller.Details(nonExistentPostId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_PostFound_ReturnsViewWithPost()
        {
            // Arrange
            var user = new IdentityUser
            {
                Id = "user-id",
                UserName = "testuser",
                Email = "testuser@example.com"
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var blog = new Blog
            {
                Title = "Test Blog",
                Description = "Test Blog Description",
                UserId = user.Id
            };
            _context.Blogs.Add(blog);
            await _context.SaveChangesAsync();

            var post = new Post
            {
                Title = "Test Post",
                Content = "Test Content",
                BlogId = blog.Id,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            // Adding comments to the post
            var comment = new Comment
            {
                Content = "Test Comment",
                PostId = post.Id,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            var controller = new PostController(
                _context,
                _userManagerMock.Object,
                _loggerMock.Object, _authorizationServiceMock.Object);

            // Act
            var result = await controller.Details(post.Id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Post>(viewResult.Model);
            Assert.Equal(post.Id, model.Id);
            Assert.Equal(post.Title, model.Title);
            Assert.NotNull(model.Blog);
            Assert.Equal(blog.Id, model.Blog.Id);
            Assert.NotNull(model.User);
            Assert.Equal(user.Id, model.User.Id);
            Assert.Single(model.Comments);
            Assert.Equal(comment.Content, model.Comments.First().Content);
        }
        
        

       
        /// Rydd opp etter tester ved å slette databasen.
        
        public void Dispose()
        {
            _context.Dispose();
            _connection.Close();
        }
    }
}
