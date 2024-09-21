using Microsoft.AspNetCore.Authorization;

namespace BlogSolution.Authorization
{
    public class IsPostOwnerRequirement : IAuthorizationRequirement
    {
        // Kan utvides med ekstra egenskaper om nødvendig
    }
}