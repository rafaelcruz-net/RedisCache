using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace RedisCache.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {

            //Gravando na Sessão utilizando Redis
            String messageSession = "Agora Estou utilizando Sessão";

            Session["RedisMessage"] = messageSession;

            //Obtendo o objeto da sessão utilizando Redis
            var messageFromSession = Session["RedisMessage"];
            
            String message = "Gravando a mensagem no cache redis";

            //Grava no cache
            CacheManager.Set<String>("message", message);

            //Obtem do cache
            String messageFromCache = CacheManager.Get<String>("message");

            ViewBag.Message = messageFromCache;

            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your About page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}