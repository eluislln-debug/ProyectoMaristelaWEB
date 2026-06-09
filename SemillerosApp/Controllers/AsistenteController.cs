using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.IO;
using System.Net;
using System.Text;
using SemillerosApp.Filters;
using Newtonsoft.Json;
using System.Configuration;

namespace SemillerosApp.Controllers
{
    [RoleAuthorize("Admin", "Lider")]
    public class AsistenteController : Controller
    {
        // GET: /Asistente/Index  (página completa)
        public ActionResult Index()
        {
            ViewBag.ActiveMenu = "asistente";
            ViewBag.PageTitle = "Asistente IA";
            ViewBag.PageSubtitle = "Consulta inteligente sobre tu semillero";
            return View();
        }

        // POST: /Asistente/Preguntar  (llamada AJAX)
        [HttpPost]
        public ActionResult Preguntar(string mensaje)
        {
            if (string.IsNullOrWhiteSpace(mensaje))
                return Json(new { error = "Mensaje vacío." });

            try
            {
                string apiKey = ConfigurationManager.AppSettings["GroqApiKey"];
                string url = "https://api.groq.com/openai/v1/chat/completions";

                string userName = RoleAuthorizeAttribute.GetUserName(new System.Web.HttpContextWrapper(System.Web.HttpContext.Current));
                string userRole = RoleAuthorizeAttribute.GetUserRole(new System.Web.HttpContextWrapper(System.Web.HttpContext.Current));

                string contexto = $"Eres un asistente virtual especializado en gestión de semilleros de investigación académica. El usuario se llama {userName} y tiene el rol de {userRole}. Responde siempre en español, de forma clara y profesional. No uses asteriscos ni markdown, usa texto limpio.";

                var body = new
                {
                    model = "llama-3.3-70b-versatile",
                    messages = new[]
                    {
                new { role = "system", content = contexto },
                new { role = "user",   content = mensaje  }
            },
                    max_tokens = 800,
                    temperature = 0.7
                };

                string jsonBody = JsonConvert.SerializeObject(body);
                byte[] data = Encoding.UTF8.GetBytes(jsonBody);

                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.ContentLength = data.Length;
                request.Headers.Add("Authorization", "Bearer " + apiKey);

                using (var stream = request.GetRequestStream())
                    stream.Write(data, 0, data.Length);

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    string raw = reader.ReadToEnd();
                    dynamic obj = JsonConvert.DeserializeObject(raw);
                    string respuesta = obj.choices[0].message.content;
                    return Json(new { respuesta });
                }
            }
            catch (WebException ex)
            {
                string detalle = "";
                if (ex.Response != null)
                    using (var r = new StreamReader(ex.Response.GetResponseStream()))
                        detalle = r.ReadToEnd();

                return Json(new { error = detalle });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }
    }
}