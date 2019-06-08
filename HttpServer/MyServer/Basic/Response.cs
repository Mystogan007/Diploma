using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using HttpServer.MyServer.Basic;
using HttpServer.MyServer.Support;

namespace HttpServer.MyServer
{
    public class Response
    {
        public const string MsgPath = @"\root\msg";
        public const string WebPath = @"\root\web";
        public const string TempPath = @"\root\temp";


        public const string ServerName = "Modeling server v0.1";

        public const string Version = "HTTP/1.1";

        private Request request;

        public byte[] responseArray { get; private set; }

        public Response(Request request)
        {
            this.request = request;
        }

        public string HandleRequest()
        {
            if (request == null)
                return "Send bad request";

            if (request.HasError)
                return "Send bad request";

            if (request.Method == HttpMethod.GET && request.Parameters.ContainsValue("favicon.ico"))
                return "Send favicon.ico";

            if (request.Method == HttpMethod.GET)
                return "Send start page";

            if (request.Method == HttpMethod.POST)
                return GetHandleResult();


            return "Send bad request";
        }

        private string GetHandleResult()
        {
            if (request.Parameters.ContainsKey("action"))
                switch (request.Parameters["action"])
                {
                    case "choose":
                        return "Send check page";
                    case "upload":
                        return "Send upload page";
                    case "check":
                        return "Send Check model status page";
                }

            else if (request.Parameters.ContainsKey("nameOfModel"))
                return "Send name of the Model";

            else if (request.Parameters.ContainsKey("resultSimulation"))
                return "Send result of simulation";

            else if (request.Parameters.ContainsKey("process"))
                return "Send status of simulation";

            return "Not Found";
        }

        public void MakeResponse(string handleResult)
        {
            StringBuilder sbHeader = new StringBuilder();

            switch (handleResult)
            {
                case "Send start page":
                    {
                        string filePath = Environment.CurrentDirectory + WebPath + @"\" + @"Start page 4.html";
                        Byte[] bodyArray = File.ReadAllBytes(filePath);
                        sbHeader.AppendLine(
                        Version + " " + HttpStatusCode.OK + "\r\n" +
                        "Server: " + ServerName + "\r\n" +
                        "Content-Type: " + "text/html" + "\r\n" +
                        "Content-Length: " + bodyArray.Length + "\r\n" +
                           "\r\n");
                        responseArray = MakeArray(sbHeader, bodyArray);
                        break;

                    }

                case "Send favicon.ico":
                    {
                        string filePath = Environment.CurrentDirectory + WebPath + @"\" + @"favicon.ico";
                        Byte[] bodyArray = File.ReadAllBytes(filePath);
                        sbHeader.AppendLine(
                        Version + " " + HttpStatusCode.OK + "\r\n" +
                        "Server: " + ServerName + "\r\n" +
                        "Content-Type: " + "text/html" + "\r\n" +
                        "Content-Length: " + bodyArray.Length + "\r\n" +
                           "\r\n");
                        responseArray = MakeArray(sbHeader, bodyArray);
                        break;

                    }

                case "Send bad request":
                    {

                        Byte[] bodyArray = Encoding.ASCII.GetBytes($"BAD REQUEST");
                        sbHeader.AppendLine(
                        Version + " " + HttpStatusCode.BadRequest + "\r\n" +
                        "Server: " + ServerName + "\r\n" +
                        "Content-Type: " + "text/html" + "\r\n" +
                        "Content-Length: " + bodyArray.Length +
                           "\r\n");

                        responseArray = MakeArray(sbHeader, bodyArray);
                        break;
                    }

                case "Send check page":
                    {
                        string filePath = Environment.CurrentDirectory + WebPath + @"\" + @"Start Simulation Page 3.html";
                        Byte[] bodyArray = File.ReadAllBytes(filePath);
                        sbHeader.AppendLine(
                        Version + " " + HttpStatusCode.OK + "\r\n" +
                        "Server: " + ServerName + "\r\n" +
                        "Content-Type: " + "text/html" + "\r\n" +
                        "Content-Length: " + bodyArray.Length +
                           "\r\n");

                        responseArray = MakeArray(sbHeader, bodyArray);
                        break;
                    }

                case "Send upload page":
                    {
                        string filePath = Environment.CurrentDirectory + WebPath + @"\" + @"Upload page 3.html";
                        Byte[] bodyArray = File.ReadAllBytes(filePath);
                        sbHeader.AppendLine(
                        Version + " " + HttpStatusCode.OK + "\r\n" +
                        "Server: " + ServerName + "\r\n" +
                        "Content-Type: " + "text/html" + "\r\n" +
                        "Content-Length: " + bodyArray.Length +
                           "\r\n");

                        responseArray = MakeArray(sbHeader, bodyArray);
                        break;
                    }

                case "Send Check model status page":
                    {
                        string filePath = Environment.CurrentDirectory + WebPath + @"\" + @"Check model status page.html";
                        Byte[] bodyArray = File.ReadAllBytes(filePath);
                        sbHeader.AppendLine(
                        Version + " " + HttpStatusCode.OK + "\r\n" +
                        "Server: " + ServerName + "\r\n" +
                        "Content-Type: " + "text/html" + "\r\n" +
                        "Content-Length: " + bodyArray.Length +
                           "\r\n");

                        responseArray = MakeArray(sbHeader, bodyArray);
                        break;
                    }


                case "Not Found":
                    {
                        string filePath = Environment.CurrentDirectory + WebPath + @"\" + @"404.html";
                        Byte[] bodyArray = File.ReadAllBytes(filePath);
                        sbHeader.AppendLine(
                        Version + " " + HttpStatusCode.NotFound + "\r\n" +
                        "Server: " + ServerName + "\r\n" +
                        "Content-Type: " + "text/html" + "\r\n" +
                        "Content-Length: " + bodyArray.Length +
                           "\r\n");

                        responseArray = MakeArray(sbHeader, bodyArray);
                        break;
                    }

                case "Send name of the Model":
                    {
                        byte[] bodyArray = Encoding.UTF8.GetBytes(request.Parameters["nameOfModel"]);
                        sbHeader.AppendLine(
                        Version + " " + HttpStatusCode.OK + "\r\n" +
                        "Server: " + ServerName + "\r\n" +
                        "Content-Type: " + "text/html" + "\r\n" +
                        "Content-Length: " + bodyArray.Length +
                           "\r\n");
                        responseArray = MakeArray(sbHeader, bodyArray);
                        break;
                    }

                case "Send result of simulation":
                    {
                        byte[] bodyArray = Encoding.UTF8.GetBytes(request.Parameters["resultSimulation"]);
                        sbHeader.AppendLine(
                        Version + " " + HttpStatusCode.OK + "\r\n" +
                        "Server: " + ServerName + "\r\n" +
                        "Content-Type: " + "text/html" + "\r\n" +
                        "Content-Length: " + bodyArray.Length +
                           "\r\n");
                        responseArray = MakeArray(sbHeader, bodyArray);
                        break;
                    }

                case "Send status of simulation":
                    {
                        byte[] bodyArray = Encoding.UTF8.GetBytes(request.Parameters["process"]);
                        sbHeader.AppendLine(
                        Version + " " + HttpStatusCode.OK + "\r\n" +
                        "Server: " + ServerName + "\r\n" +
                        "Content-Type: " + "text/html" + "\r\n" +
                        "Content-Length: " + bodyArray.Length +
                           "\r\n");
                        responseArray = MakeArray(sbHeader, bodyArray);
                        break;
                    }

            }

        }


        public byte[] MakeArray(StringBuilder sbHeader, Byte[] bodyArray)
        {

            List<byte> response = new List<byte>();
            response.AddRange(Encoding.ASCII.GetBytes(sbHeader.ToString()));
            response.AddRange(bodyArray);
            return response.ToArray();

        }





    }
}
