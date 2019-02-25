using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace HotelBookingHandler
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
                MayflowerHotelBookingHandler service1 = new MayflowerHotelBookingHandler();
                service1.ConsoleStartupAndStop(args);
            }
            else
            {
                ServiceBase[] ServicesToRun;
                MayflowerHotelBookingHandler service1 = new MayflowerHotelBookingHandler();
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
