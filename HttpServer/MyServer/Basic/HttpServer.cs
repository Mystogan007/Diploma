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
using HttpServer.MyServer;

namespace HttpServer
{
    public class HttpServer
    {

        
        

        private bool isRunning = false;

        private TcpListener listener;

        public HttpServer(int port)
        {
            listener = new TcpListener(IPAddress.Any, port);           
            ThreadPool.SetMaxThreads(Environment.ProcessorCount * 4, Environment.ProcessorCount * 4);
            ThreadPool.SetMinThreads(2, 2);
        }

        public void Start()
        {
            isRunning = true;
            listener.Start();

            while (isRunning)
            {                
                if (listener.Pending())
                {
                    #region Через создание новых тридов

                    //Thread t = new Thread(HandleRequest);
                    //t.IsBackground = true;
                    //t.Start(listener.AcceptSocket());
                    //   Console.WriteLine("Client is connected!");  
                    #endregion
                    #region Через пул потоков
                    ThreadPool.QueueUserWorkItem(HandleRequest, listener.AcceptSocket());

                    #endregion
                }
                Thread.Sleep(1000);
            }        
            listener.Stop(); 
        }
               


        private void HandleRequest(object arg)
        {
            Socket myClient = (Socket)arg;
            byte[] requestArray = ReadToEnd(myClient);

           Console.WriteLine(Encoding.ASCII.GetString(requestArray));  //тестовая строка для просмотра запросов            

            if (requestArray.Length != 0)
            {
                Request request = new Request(requestArray);
                request.LoadRequest();
                request = request.HasError == true ? null : request;
                Response response = new Response(request);
                response.MakeResponse(response.HandleRequest());
                myClient.Send(response.responseArray, response.responseArray.Length, SocketFlags.None);
            }
            myClient.Dispose();
            myClient.Close();
        }



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
