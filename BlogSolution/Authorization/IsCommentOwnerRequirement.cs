using Microsoft.AspNetCore.Authorization;

namespace BlogSolution.Authorization
{
    public class IsCommentOwnerRequirement : IAuthorizationRequirement
    {
        // Kan utvides med ekstra egenskaper om nødvendig
    }
}