using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.AspNet.SignalR;
using System.Threading.Tasks;
using System.Threading;

namespace Mayflower.Areas.Notification.SignalR
{
    public class NotificationHub : Hub
    {
        private readonly General.WebSession _webSession;

        private readonly TimeSpan _updateInterval = TimeSpan.FromMilliseconds(250);
        private Timer _timer;

        /// <summary>
        /// Single User Message Only.
        /// </summary>
        /// <param name="dynamicHTML"></param>
        public void Notif(object dynamicHTML = null)
        {
            var username = Context.User.Identity.Name;

            //string msg = ""; // msg from state
            
            Clients.Client(Context.ConnectionId).pushSingle(dynamicHTML);
        }

        /// <summary>
        /// Public message.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="message"></param>
        public void Send(string name, string message)
        {
            // Call the broadcastMessage method to update clients.
            Clients.All.pushMsg(name, message);
        }

        public void CheckSession()
        {
            _timer = new Timer(Notif, "Session Expired", _updateInterval, _updateInterval);
        }

        public bool IsConnected(string cid)
        {
            return false;
            //return _connectedClients.Contains(cid);
        }

        public override Task OnConnected()
        {
            return base.OnConnected();
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            return base.OnDisconnected(stopCalled);
        }

        public override Task OnReconnected()
        {
            return base.OnReconnected();
        }
    }
}