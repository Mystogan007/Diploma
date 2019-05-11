using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Drawing;
using HttpServer.MyServer.Support;

namespace HttpServer
{
    public class HttpServer
    {
        public const string MsgPath = @"\root\msg";
        public const string WebPath = @"\root\msg";
        public const string TempPath = @"\root\temp";
        public const string Version = "HTTP/1.1";
        public const string Name = "Tutorial server v0.1";

        private bool isRunning = false;

        private TcpListener listener;

        public HttpServer(string IP, int port)
        {
            if (IP == "*")
            {
                IPAddress ip = Dns.GetHostByName(Dns.GetHostName()).AddressList[0];
                listener = new TcpListener(ip, port);
            }
            else
            listener = new TcpListener(IPAddress.Any, port);
            // listener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
        }

        public void Start()
        {
            isRunning = true;
            listener.Start();

            while (isRunning)
            {
                //  Console.WriteLine("Waiting for connection");
                if (listener.Pending())
                {
                    Thread t = new Thread(Parserequest);
                    t.IsBackground = true;
                    t.Start(listener.AcceptTcpClient());
                    Console.WriteLine("Client is connected!");                    
                }
                Thread.Sleep(1000); 

            }

            isRunning = false;

            listener.Stop();

            


        }

        private void Parserequest(object arg)
        {
            Request request = new Request((TcpClient)arg);
            request.LoadRequest();
            //if(request.HasErrorParseStartLine || request.HasErrorParseHeaders)
            //{

            //}
            if(request.Method == HttpMethod.POST)
            {

                String filePath = Environment.CurrentDirectory + TempPath + @"\1.jpg";
                request.ToFile(filePath);
                request.CloseClient();
                isRunning = false;

            }
            else if (request.Method == HttpMethod.GET && request.NameOfFile == null)
            {


            }
            else if (request.Method == HttpMethod.GET && request.NameOfFile != null)
            {

            }

        }



        private void HandleRequest(object arg)
        {


            

            #region На сокетах
            //Socket myClient = (Socket)arg;
            //if (myClient.Connected)
            //{
            //    byte[] httpRequest = ReadToEnd(myClient);
            //    Request request = Parser.ParseRequest(httpRequest);
            //    Response response = Parser.ParseAndMakeResponse(request);
            //    //      SendResponse(client.GetStream(), response);
            //}
            #endregion

        }

        //public void SendResponse(NetworkStream stream, Response Response)
        //{

        //    StringBuilder sbHeader = new StringBuilder();

        //    sbHeader.AppendLine(Version + " " + Response.status);
        //    // CONTENT-LENGTH            
        //    sbHeader.AppendLine("Content-Length: " + Response.data.Length);

        //    // Append one more line breaks to seperate header and content.
        //    sbHeader.AppendLine();
 
        //    List<byte> response = new List<byte>();
        //    // response.AddRange(bHeadersString);
        //    response.AddRange(Encoding.ASCII.GetBytes(sbHeader.ToString()));

        //    response.AddRange(Response.data);
        //    byte[] responseByte = response.ToArray();
        //    stream.Write(responseByte, 0, responseByte.Length);
        //}

        public static byte[] ReadToEnd(Socket mySocket)
        {
            byte[] b = new byte[mySocket.ReceiveBufferSize];
            int len = 0;
            using (MemoryStream m = new MemoryStream())
            {
                while (mySocket.Poll(1000000, SelectMode.SelectRead) && (len = mySocket.Receive(b, mySocket.ReceiveBufferSize, SocketFlags.None)) > 0)
                {
                    m.Write(b, 0, len);
                }
                return m.ToArray();
            }
        }



    }
}
