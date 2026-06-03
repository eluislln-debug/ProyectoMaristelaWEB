using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;

namespace SemillerosApp.Filters
{
    public class RoleAuthorizeAttribute : AuthorizeAttribute
    {
        private readonly string[] _roles;

        public RoleAuthorizeAttribute(params string[] roles)
        {
            _roles = roles;
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            if (!httpContext.User.Identity.IsAuthenticated)
                return false;

            if (_roles == null || _roles.Length == 0)
                return true;

            string userRole = GetUserRole(httpContext);
            foreach (var role in _roles)
            {
                if (userRole.Equals(role, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (!filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                // No autenticado → ir al Login
                base.HandleUnauthorizedRequest(filterContext);
            }
            else
            {
                // Autenticado pero sin permiso → acceso denegado
                filterContext.Result = new RedirectToRouteResult(
                    new System.Web.Routing.RouteValueDictionary(
                        new { controller = "Account", action = "AccessDenied" }
                    )
                );
            }
        }

        // ── Helpers estáticos usables desde cualquier Controller ──

        public static string GetUserRole(HttpContextBase httpContext)
        {
            var ticket = GetTicket(httpContext);
            if (ticket == null) return string.Empty;
            var parts = ticket.UserData.Split('|');
            return parts.Length >= 3 ? parts[2] : string.Empty;
        }

        public static int GetUserId(HttpContextBase httpContext)
        {
            var ticket = GetTicket(httpContext);
            if (ticket == null) return 0;
            var parts = ticket.UserData.Split('|');
            return parts.Length >= 1 && int.TryParse(parts[0], out int id) ? id : 0;
        }

        public static string GetUserName(HttpContextBase httpContext)
        {
            var ticket = GetTicket(httpContext);
            if (ticket == null) return string.Empty;
            var parts = ticket.UserData.Split('|');
            return parts.Length >= 2 ? parts[1] : string.Empty;
        }

        private static FormsAuthenticationTicket GetTicket(HttpContextBase httpContext)
        {
            return (httpContext.User.Identity as FormsIdentity)?.Ticket;
        }
    }
}