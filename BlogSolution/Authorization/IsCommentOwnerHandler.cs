using Microsoft.AspNetCore.Authorization;
using BlogSolution.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace BlogSolution.Authorization
{
    public class IsCommentOwnerHandler : AuthorizationHandler<IsCommentOwnerRequirement, Comment>
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<IsCommentOwnerHandler> _logger;

        public IsCommentOwnerHandler(UserManager<IdentityUser> userManager, ILogger<IsCommentOwnerHandler> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
            IsCommentOwnerRequirement requirement,
            Comment resource)
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
                _logger.LogInformation("User is owner of the Comment. Authorization succeeded.");
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogWarning("User is not the owner of the Comment. Authorization failed.");
            }

            await Task.CompletedTask;
        }
    }
}