using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace HttpServer
{
    public class Response
    {
       public readonly byte[] data = null;
        public readonly String status;
        public readonly string mime;

        public Response(string status, string mime, byte[] data)
        {
            this.data = data;
            this.status = status;
            this.mime = mime;
        }




    

    }
}
