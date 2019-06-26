using System;
using System.Collections.Generic;
using System.Text;

namespace HttpServer.MyServer
{
  static public class ProcessKeeper
    {
        private static Dictionary<string, string> Storage =
new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);


        static public string GetSubjectAreaOfProcess(string processName)
        {

            string value;

            if (!Storage.TryGetValue(processName, out value))
            {
                value = string.Empty;
            }

            return value;
        }

        static public void WriteNewProcess(Tuple<string,string> newProcess)
        {
            Storage[newProcess.Item1] = newProcess.Item2;
        }
    }
}
