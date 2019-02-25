using Microsoft.Owin;
using Owin;
using System.Net;

[assembly: OwinStartupAttribute(typeof(Mayflower.Startup))]
namespace Mayflower
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.MapSignalR();
            ConfigureAuth(app);

            // Setup for HTTPS issues.
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11
                                                    | SecurityProtocolType.Tls12 | SecurityProtocolType.Ssl3;
        }
    }
}
