using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace HttpServer
{
    public class Request
    {
        public string Method { get; set; }
        public string NameOfFile { get; set; }

        public string Protocol { get; set; }
        public string Version { get; set; }
        public string Host { get; set; }

        public Dictionary<string, string> Items { get; set; }

        public Request(string method, string protocol, string version, string nameOfFile)
        {
            Method = method;
            Protocol = protocol;
            Version = version;
            NameOfFile = nameOfFile;
            Host = null;
            Items = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
        }   
        

    }
}
