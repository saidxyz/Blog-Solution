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

        /// <summary>
        /// Tester at Details (GET) med gyldig id returnerer View med korrekt modell.
        /// </summary>
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
                Description = "A blog for testing."
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

        /// <summary>
        /// Tester at Create (GET) returnerer View med korrekt BlogId.
        /// </summary>
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

        /// <summary>
        /// Tester at Create (POST) med gyldig modell oppretter en post og omdirigerer til Blog Details.
        /// </summary>
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

        /// <summary>
        /// Tester at Create (POST) med ugyldig modell returnerer View med samme modell.
        /// </summary>
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

        /// <summary>
        /// Tester at Edit (GET) med gyldig id og autorisert bruker returnerer View med korrekt modell.
        /// </summary>
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
                Title  = "Test Blog",
                Description = "A blog for testing."
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

        /// <summary>
        /// Tester at Edit (GET) med null id returnerer NotFound.
        /// </summary>
        [Fact]
        public async Task Edit_Get_NullId_ReturnsNotFound()
        {
            // Arrange
            var controller = new PostController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object);

            // Act
            var result = await controller.Edit(null);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        /// <summary>
        /// Tester at Edit (GET) med ugyldig id returnerer NotFound.
        /// </summary>
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

        /// <summary>
        /// Tester at Edit (GET) med gyldig id, men uautorisert bruker returnerer Forbid.
        /// </summary>
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
                Description = "A blog for testing."
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

        /// <summary>
        /// Tester at Edit (POST) med gyldig modell oppdaterer posten og omdirigerer til Blog Details.
        /// </summary>
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
                Description = "A blog for testing."
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

            // Sjekk at posten ble oppdatert i databasen
            var updatedPost = await _context.Posts.FindAsync(post.Id);
            Assert.Equal(model.Title, updatedPost.Title);
            Assert.Equal(model.Content, updatedPost.Content);
        }

        /// <summary>
        /// Tester at Edit (POST) med ugyldig modell returnerer View med samme modell.
        /// </summary>
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

        /// <summary>
        /// Tester at Edit (POST) med gyldig modell, men en koncurrensfeil oppstår, returnerer NotFound.
        /// </summary>
        [Fact]
        public async Task Edit_Post_ConcurrencyException_ReturnsNotFound()
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
                Description = "A blog for testing."
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

            // Act & Assert
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => controller.Edit(post.Id, model));
        }

        /// <summary>
        /// Tester at Delete (GET) med gyldig id og autorisert bruker returnerer View med korrekt modell.
        /// </summary>
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

        /// <summary>
        /// Tester at Delete (GET) med null id returnerer NotFound.
        /// </summary>
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

        /// <summary>
        /// Tester at Delete (GET) med ugyldig id returnerer NotFound.
        /// </summary>
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

        /// <summary>
        /// Tester at Delete (GET) med gyldig id, men uautorisert bruker returnerer Forbid.
        /// </summary>
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

        /// <summary>
        /// Tester at DeleteConfirmed (POST) med gyldig id og autorisert bruker sletter posten og omdirigerer til Blog Details.
        /// </summary>
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
                Description = "A blog for testing."
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

        /// <summary>
        /// Tester at DeleteConfirmed (POST) med ugyldig id returnerer NotFound.
        /// </summary>
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

        /// <summary>
        /// Tester at DeleteConfirmed (POST) med gyldig id, men uautorisert bruker returnerer Forbid.
        /// </summary>
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
                Title  = "Test Blog",
                Description = "A blog for testing."
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
            var result = await controller.DeleteConfirmed(post.Id);

            // Assert
            Assert.IsType<ForbidResult>(result);

            // Sjekk at posten fortsatt finnes i databasen
            var existingPost = await _context.Posts.FindAsync(post.Id);
            Assert.NotNull(existingPost);
        }

        /// <summary>
        /// Tester at Edit (POST) med gyldig modell, men en koncurrensfeil oppstår, kaster DbUpdateConcurrencyException.
        /// </summary>
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
                Title  = "Test Blog",
                Description = "A blog for testing."
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

            // Act & Assert
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => controller.Edit(post.Id, model));
        }

        /// <summary>
        /// Tester at DeleteConfirmed (POST) med gyldig id, men en koncurrensfeil oppstår, kaster DbUpdateConcurrencyException.
        /// </summary>
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
            
            // Act & Assert
            var exception = await Record.ExceptionAsync(() => controller.DeleteConfirmed(post.Id));
            Assert.IsType<DbUpdateConcurrencyException>(exception);

        }

        /// <summary>
        /// Rydd opp etter tester ved å slette databasen.
        /// </summary>
        public void Dispose()
        {
            _context.Dispose();
            _connection.Close();
        }
    }
}
