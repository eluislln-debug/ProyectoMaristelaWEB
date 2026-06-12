using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using SemillerosApp.Models;

namespace SemillerosApp.Controllers
{
    public class AccountController : Controller
    {
        private SemillerosContext db = new SemillerosContext();
        
        // GET: Account
        public ActionResult Index()
        {
            return View();
        }

        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            if (User.Identity.IsAuthenticated)
            {
                // Si ya está logueado, mandarlo a su panel correspondiente
                var identity = User.Identity as System.Security.Principal.IIdentity;
                string userData = (User.Identity as System.Web.Security.FormsIdentity)?.Ticket.UserData ?? "";
                string rol = userData.Split('|').Length >= 3 ? userData.Split('|')[2] : "";

                if (rol == "Admin")
                    return RedirectToAction("Index", "AdminDashboard");
                else
                    return RedirectToAction("Index", "LiderDashboard");
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Buscar usuario activo con esas credenciales
            var usuario = db.Usuarios.FirstOrDefault(u =>
                u.emailUsuario == model.Email &&
                u.contrasenaUsuario == model.Password &&
                u.estadoUsuario == "Activo");

            if (usuario == null)
            {
                ModelState.AddModelError("", "Correo o contraseña incorrectos, o la cuenta está inactiva.");
                return View(model);
            }

            // Crear ticket con: id | nombre completo | rol
            string userData = $"{usuario.idUsuario}|{usuario.nombreUsuario} {usuario.apellidoUsuario}|{usuario.rolUsuario}";

            FormsAuthenticationTicket ticket = new FormsAuthenticationTicket(
                version: 1,
                name: usuario.emailUsuario,
                issueDate: DateTime.Now,
                expiration: DateTime.Now.AddHours(8),
                isPersistent: model.RememberMe,
                userData: userData
            );

            string encrypted = FormsAuthentication.Encrypt(ticket);
            HttpCookie cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encrypted);
            if (model.RememberMe)
                cookie.Expires = DateTime.Now.AddDays(7);
            Response.Cookies.Add(cookie);

            // ── Redirección por rol ──────────────────────────────
            if (usuario.rolUsuario == "Admin")
            {
                TempData["Bienvenida"] = $"¡Bienvenido de nuevo, {usuario.nombreUsuario}! Tienes acceso completo al sistema.";
                return RedirectToAction("Index", "AdminDashboard");
            }
            else
            {
                TempData["Bienvenida"] = $"¡Hola, {usuario.nombreUsuario}! Bienvenido a tu panel de semillero.";
                return RedirectToAction("Index", "LiderDashboard");
            }
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Clear();
            Session.Abandon();
            Response.Cookies.Add(new HttpCookie(FormsAuthentication.FormsCookieName)
            {
                Expires = DateTime.Now.AddDays(-1)
            });
            return RedirectToAction("Login", "Account");
        }

        // GET: /Account/AccessDenied
        [AllowAnonymous]
        public ActionResult AccessDenied()
        {
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}