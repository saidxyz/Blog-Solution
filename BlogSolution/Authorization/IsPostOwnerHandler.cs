using Microsoft.AspNetCore.Authorization;
using BlogSolution.Models;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BlogSolution.Authorization
{
    public class IsPostOwnerHandler : AuthorizationHandler<IsPostOwnerRequirement, Post>
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<IsPostOwnerHandler> _logger;

        public IsPostOwnerHandler(UserManager<IdentityUser> userManager, ILogger<IsPostOwnerHandler> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
            IsPostOwnerRequirement requirement,
            Post resource)
        {
            if (context.User == null || resource == null)
            {
                _logger.LogWarning("Authorization failed: User or resource is null.");
                return;
            }

            // Sjekk om brukeren er i Admin-rollen
            if (context.User.IsInRole("Admin"))
            {
                _logger.LogInformation("User is in Admin role. Authorization succeeded.");
                context.Succeed(requirement);
                return;
            }

            var userId = _userManager.GetUserId(context.User);
            if (resource.UserId == userId)
            {
                _logger.LogInformation("User is owner of the Post. Authorization succeeded.");
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogWarning("User is not the owner of the Post. Authorization failed.");
            }

            await Task.CompletedTask;
        }
    }
}