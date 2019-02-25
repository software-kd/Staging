using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.SessionState;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.AspNet.SignalR;
using Mayflower.Areas.Notification.SignalR;

namespace Mayflower.General
{
    public class CusSession
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class WebSession
    {
        private readonly static Lazy<WebSession> _instance = new Lazy<WebSession>(
            () => new WebSession(GlobalHost.ConnectionManager.GetHubContext<NotificationHub>().Clients));

        private readonly ConcurrentDictionary<string, CusSession> _session = new ConcurrentDictionary<string, CusSession>();

        private WebSession(IHubConnectionContext<dynamic> clients)
        {
            Clients = clients;
        }

        public static WebSession Instance
        {
            get
            {
                return _instance.Value;
            }
        }

        private IHubConnectionContext<dynamic> Clients
        {
            get;
            set;
        }

        public IEnumerable<CusSession> GetAllSession()
        {
            return _session.Values;
        }
    }
}