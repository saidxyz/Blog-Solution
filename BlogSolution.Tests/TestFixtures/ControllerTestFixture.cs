using BlogSolution.Controllers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using BlogSolution.Data;
using Microsoft.AspNetCore.Authorization;

namespace BlogSolution.Tests.TestFixtures
{
    public class ControllerTestFixture : IDisposable
    {
        public ApplicationDbContext Context { get; private set; }
        public Mock<UserManager<IdentityUser>> UserManagerMock { get; private set; }
        public Mock<ILogger<PostController>> PostControllerLoggerMock { get; private set; }
        public Mock<ILogger<CommentController>> CommentControllerLoggerMock { get; private set; }
        public Mock<ILogger<BlogController>> BlogControllerLoggerMock { get; private set; }
        public Mock<IAuthorizationService> AuthorizationServiceMock { get; private set; }

        public ControllerTestFixture()
        {
            // Setup In-Memory Database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            Context = new ApplicationDbContext(options);

            // Setup UserManager Mock
            var store = new Mock<IUserStore<IdentityUser>>();
            UserManagerMock = new Mock<UserManager<IdentityUser>>(
                store.Object, null, null, null, null, null, null, null, null);

            // Setup Logger Mocks
            PostControllerLoggerMock = new Mock<ILogger<PostController>>();
            CommentControllerLoggerMock = new Mock<ILogger<CommentController>>();
            BlogControllerLoggerMock = new Mock<ILogger<BlogController>>();

            // Setup AuthorizationService Mock
            AuthorizationServiceMock = new Mock<IAuthorizationService>();
        }

        public void Dispose()
        {
            Context.Dispose();
        }
    }
}