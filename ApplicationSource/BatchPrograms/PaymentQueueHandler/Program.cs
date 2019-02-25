using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace PaymentQueueHandler
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                PaymentQueueService service1 = new PaymentQueueService();
                service1.ConsoleStartupAndStop(args);
            }
            else
            {
                ServiceBase[] ServicesToRun;
                var service1 = new PaymentQueueService();

                ServicesToRun = new ServiceBase[]
                {
                    service1
                };

                service1.ImmediateStartup(args);
                ServiceBase.Run(ServicesToRun);
            }
        }
    }
}
