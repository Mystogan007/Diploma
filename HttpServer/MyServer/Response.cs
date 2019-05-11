using System;
using System.Collections.Generic;
using System.Text;
using HttpServer.MyServer.Support;

namespace HttpServer.MyServer
{
    class Response
    {
        /// <summary>
        /// Возвращает код состояния ответа.
        /// </summary>
        public HttpStatusCode StatusCode { get; private set; }

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
