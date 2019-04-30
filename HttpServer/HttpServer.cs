using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace HttpServer
{
    public class HttpServer
    {
        public const string MsgPath = @"\root\msg";
        public const string WebPath = @"\root\msg";
        public const string Version = "HTTP/1.1";
        public const string Name = "Tutorial server v0.1";

        private bool isRunning = false;

        private TcpListener listener;

        public HttpServer(int port)
        {
            listener = new TcpListener(IPAddress.Any, port);
        }

        public void Start()
        {
            Thread threadServer = new Thread(new ThreadStart(Run));
            threadServer.Start();
        }
        public void Run()
        {
            isRunning = true;
            listener.Start();

            while (isRunning)
            {
                Console.WriteLine("Waiting for connection");
                TcpClient client = listener.AcceptTcpClient();
                Console.WriteLine("Client is connected!");

                HandleClient(client);

                client.Close();
            }

            isRunning = false;

            listener.Stop();




        }

        private void HandleClient(TcpClient client)
        {
            StreamReader reader = new StreamReader(client.GetStream());

            string incomingData = "";



            while (reader.Peek() != -1)
            {
                incomingData += reader.ReadLine() + "\n";

            }
            Console.WriteLine(incomingData);

            Request request = Parser.ParseAndMakeRequest(incomingData);
            Response response = Parser.ParseAndMakeResponse(request);


            SendResponse(client.GetStream(), response);
        }

        public void SendResponse(NetworkStream stream, Response Response)
        {

            StringBuilder sbHeader = new StringBuilder();

            sbHeader.AppendLine(HttpServer.Version + " " + Response.status);
            // CONTENT-LENGTH            
            sbHeader.AppendLine("Content-Length: " + Response.data.Length);

            // Append one more line breaks to seperate header and content.
            sbHeader.AppendLine();
            string deadlie = sbHeader.ToString();
            List<byte> response = new List<byte>();
            // response.AddRange(bHeadersString);
            response.AddRange(Encoding.ASCII.GetBytes(sbHeader.ToString()));

            response.AddRange(Response.data);
            byte[] responseByte = response.ToArray();
            stream.Write(responseByte, 0, responseByte.Length);
        }



    }
}
