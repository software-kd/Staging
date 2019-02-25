using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SendPDFQueueHandler.Functions
{
    class ItineraryLog
    {
        EventLog EventLog { get; set; }

        public ItineraryLog(EventLog _event)
        {
            EventLog = _event;
        }

        public void WriteEventLog(string message, EventLogEntryType type, int eventID)
        {
            int maxLength = 31000;
            var splitedLog = message.SplitInParts(maxLength);

            foreach (var item in splitedLog)
            {
                EventLog.WriteEntry(item, type, eventID);
            }
        }

        public void WriteEventLog(StringBuilder message, EventLogEntryType type, int eventID)
        {
            string _msg = message.ToString();
            WriteEventLog(_msg, type, eventID);
        }
    }
}
