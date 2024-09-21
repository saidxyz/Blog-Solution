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

namespace BlogSolution.Tests.Controllers
{
    public class PostControllerTests
    {
        private readonly Mock<UserManager<IdentityUser>> _userManagerMock;
        private readonly Mock<ILogger<PostController>> _loggerMock;
        private readonly Mock<IAuthorizationService> _authorizationServiceMock;
        private readonly ApplicationDbContext _context;

        public PostControllerTests()
        {
            var store = new Mock<IUserStore<IdentityUser>>();
            _userManagerMock = new Mock<UserManager<IdentityUser>>(
                store.Object, null, null, null, null, null, null, null, null);

            _loggerMock = new Mock<ILogger<PostController>>();
            _authorizationServiceMock = new Mock<IAuthorizationService>();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "BlogSolutionTest_Post")
                .Options;
            _context = new ApplicationDbContext(options);
        }
        
        
        [Fact]
        public async Task Create_Post_ValidModel_RedirectsToBlogDetails()
        {
            // Arrange
            var user = new IdentityUser { Id = "test-user-id", UserName = "test@example.com", Email = "test@example.com" };
            _userManagerMock.Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            var controller = new PostController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object);

            var model = new PostCreateViewModel
            {
                Title = "Test Post",
                Content = "This is a test post.",
                BlogId = 1
            };

            // Simuler en autentisert bruker
            var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName),
            }, "mock"));

            controller.ControllerContext = new ControllerContext()
            {
                HttpContext = new DefaultHttpContext() { User = userClaims }
            };

            // Act
            var result = await controller.Create(model);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirectResult.ActionName);
            Assert.Equal("Blog", redirectResult.ControllerName);
            Assert.Equal(1, redirectResult.RouteValues["id"]);

            // Rydd opp
            _context.Posts.RemoveRange(_context.Posts);
            await _context.SaveChangesAsync();
        }

        [Fact]
        public async Task Create_Post_InvalidModel_ReturnsView()
        {
            // Arrange
            var controller = new PostController(_context, _userManagerMock.Object, _loggerMock.Object, _authorizationServiceMock.Object);
            controller.ModelState.AddModelError("Title", "Required");

            var model = new PostCreateViewModel
            {
                Title = "", // Ugyldig fordi det er påkrevd
                Content = "This is a test post.",
                BlogId = 1
            };

            // Act
            var result = await controller.Create(model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(model, viewResult.Model);
        }

        // Flere tester kan legges til for andre handlinger og scenarier
    }
}
