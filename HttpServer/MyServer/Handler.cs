﻿using HttpServer.MyServer.Support;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace HttpServer.MyServer
{
    static class Handler
    {
        [DllImport(@"C:\Users\Asus\Desktop\V\HttpServer\bin\Debug\netcoreapp2.1\control.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint ControlSystemEntryPoint(
    uint ID,
    IntPtr in_params,
    uint in_byte_count,
  out IntPtr out_params,
   out uint out_byte_count);

        [DllImport(@"C:\Users\Asus\Desktop\V\HttpServer\bin\Debug\netcoreapp2.1\control.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ControlSystemGetErrorDescription(
        uint code,
        out uint pSize);



        protected class Request
        {
            public Version ProtocolVersion;

            public HttpMethod Method;

            public Dictionary<string, string> Headers =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, string> Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, byte[]> Boundaries =
   new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);


            public bool isBadRequest;
        }


        protected class ByteReader
        {
            public readonly string NewLine = "\r\n";

            public int InitialLineSize = 1000;

            public byte[] lineBuffer;

            public int position;

            public byte[] requestArray;


            public ByteReader(byte[] requestArray)
            {
                this.requestArray = requestArray;
                lineBuffer = new byte[InitialLineSize];
                position = 0;
            }
        }

        static protected class BytesChecker
        {
            static readonly int[] Empty = new int[0];

            public static int[] Locate(byte[] self, byte[] candidate)
            {
                if (IsEmptyLocate(self, candidate))
                    return Empty;

                var list = new List<int>();

                for (int i = 0; i < self.Length; i++)
                {
                    if (!IsMatch(self, i, candidate))
                        continue;

                    list.Add(i);
                }

                return list.Count == 0 ? Empty : list.ToArray();
            }

            static bool IsMatch(byte[] array, int position, byte[] candidate)
            {
                if (candidate.Length > (array.Length - position))
                    return false;

                for (int i = 0; i < candidate.Length; i++)
                    if (array[position + i] != candidate[i])
                        return false;

                return true;
            }

            static bool IsEmptyLocate(byte[] array, byte[] candidate)
            {
                return array == null
                    || candidate == null
                    || array.Length == 0
                    || candidate.Length == 0
                    || candidate.Length > array.Length;
            }
        }
     

        public static byte[] GetRequestFromArrayAndReturnResponse(byte[] requestArray)
        {
            Request request = GetRequest(requestArray);
            return MakeResponse(HandleRequest(request), request);
        }

        #region Handle request

        private static Request GetRequest(byte[] requestArray)
        {
            ByteReader ByteReader = new ByteReader(requestArray);
            Request request = new Request();
            request.isBadRequest = false;

            ReceiveStartingLine(ByteReader, request);
            if (request.isBadRequest)
                return request;

            ReceiveHeaders(ByteReader, request);
            if (request.isBadRequest)
                return request;

            if (request.Headers.ContainsKey("Content-Type"))
            {
                GetBoundaries(request);
            }

            if (request.Method == HttpMethod.POST)
            {
                ParseBody(ByteReader, request);
            }
            return request;
        }

        private static void ParseBody(ByteReader byteReader, Request request)
        {
            if (request.Boundaries.ContainsKey("boundary"))
            {
                List<Tuple<int, int>> parts = GetNumberAndIndexPartsOfBody(byteReader, request);
                foreach (var part in parts)
                {
                    try
                    {
                        Tuple<int, int> specifiedPart = CheckBodyHeaders(part, byteReader, request);
                        if (request.Parameters.ContainsKey("nameOfFileInBody"))
                        {
                            request.Parameters["nameOfModel"] = GetEntryPointToLoadModel(ReceivePartOfBodyBytes(specifiedPart, byteReader));
                        }
                        else
                            GetParameters(specifiedPart, request, byteReader);
                    }
                    catch (Exception e)
                    {
                        request.Parameters["error"] = e.ToString();
                        return;
                    }
                }
                if (request.Parameters.ContainsKey("subject") && request.Parameters.ContainsKey("model"))
                {
                    try
                    {
                        request.Parameters["nameOfProcess"] = GetEntryPointToStartModeling(request.Parameters);
                    }
                    catch (Exception e)
                    {
                        request.Parameters["error"] = e.ToString();
                        return;
                    }
                }
                if (request.Parameters.ContainsKey("process"))
                {
                    try
                    {
                        request.Parameters["process"] = GetEntryPointToCheckStatus(request);
                    }
                    catch (Exception e)
                    {
                        request.Parameters["error"] = e.ToString();
                        return;
                    }
                }

            }
        }

        static private List<Tuple<int, int>> GetNumberAndIndexPartsOfBody(ByteReader ByteReader, Request request)
        {
            int start = 0;
            int finish = 0;
            int flag = 0;
            List<int> temp = new List<int>();
            List<Tuple<int, int>> parts = new List<Tuple<int, int>>();

            foreach (var position in BytesChecker.Locate(ByteReader.requestArray, request.Boundaries["boundary"]))
            {
                temp.Add(position);
            }
            foreach (var position in BytesChecker.Locate(ByteReader.requestArray, request.Boundaries["finishBoundary"]))
            {
                temp.Add(position);
            }



            foreach (int position in temp)
            {
                switch (flag)
                {
                    case 0:
                        start = position;
                        flag++;
                        break;
                    case 1:
                        finish = position - 2;
                        parts.Add(new Tuple<int, int>(start, finish));
                        flag++;
                        break;
                    default:
                        start = finish + 2;
                        finish = position - 2;
                        parts.Add(new Tuple<int, int>(start, finish));

                        break;
                }

            }

            return parts;
        }

        static private Tuple<int, int> CheckBodyHeaders(Tuple<int, int> part, ByteReader ByteReader, Request request)
        {
            ByteReader.position = part.Item1 + request.Boundaries["boundary"].Length;

            while (ByteReader.position != part.Item2)
            {

                string line = ReadLine(ByteReader);

                // Если достигнут конец заголовков тела.
                if (line == ByteReader.NewLine)
                {
                    Tuple<int, int> specifiedPart = new Tuple<int, int>(ByteReader.position, part.Item2);
                    return specifiedPart;
                }
                // Ищем имя файла в заголовках тела.
                if (line.Contains("filename"))
                {
                    Regex myReg = new Regex($@"filename=""(?<nameOfFile>.+)""", RegexOptions.Multiline);
                    Match name = myReg.Match(line);
                    request.Parameters["nameOfFileInBody"] = name.Groups["nameOfFile"].Value;
                }
                // Ищем имя параметра в заголовках тела.
                else if (line.Contains("name"))
                {
                    Regex myReg = new Regex($@"name=""(?<name>.+)""", RegexOptions.Multiline);
                    Match name = myReg.Match(line);
                    request.Parameters[$"{name.Groups["name"].Value}"] = "";
                }

            }
            return null;
        }

        static private void GetParameters(Tuple<int, int> specifiedPart, Request request, ByteReader ByteReader)
        {
            byte[] source = ReceivePartOfBodyBytes(specifiedPart, ByteReader);
            string line = Encoding.UTF8.GetString(source);
            if (request.Parameters.ContainsKey("action"))
            {
                request.Parameters["action"] = line;
                return;
            }

            if (request.Parameters.ContainsKey("subject") && request.Parameters["subject"] == string.Empty)
            {
                request.Parameters["subject"] = line;
                return;
            }

            if (request.Parameters.ContainsKey("model") && request.Parameters["model"] == string.Empty)
            {
                request.Parameters["model"] = line;
                return;
            }


            if (request.Parameters.ContainsKey("process"))
                request.Parameters["process"] = line;
        }

        static private byte[] ReceivePartOfBodyBytes(Tuple<int, int> specifiedPart, ByteReader ByteReader)
        {
            int startIndex = specifiedPart.Item1;
            int finishIndex = specifiedPart.Item2;
            int length = finishIndex - startIndex;
            byte[] temp = new byte[length];
            Array.Copy(ByteReader.requestArray, startIndex, temp, 0, length);
            return temp;
        }

        private static void GetBoundaries(Request request)
        {
            Regex myReg = new Regex($@"boundary=(?<boundary>.+)", RegexOptions.Multiline);
            Match name = myReg.Match(request.Headers["Content-Type"]);
            request.Boundaries["boundary"] = Encoding.ASCII.GetBytes("--" + name.Groups["boundary"].Value + "\r\n");
            request.Boundaries["finishBoundary"] = Encoding.ASCII.GetBytes("--" + name.Groups["boundary"].Value + "--\r\n");
        }

        private static void ReceiveHeaders(ByteReader ByteReader, Request request)
        {
            while (true)
            {
                string header = ReadLine(ByteReader);

                // Если достигнут конец заголовков.
                if (header == ByteReader.NewLine)
                    return;

                // Ищем позицию между именем и значением заголовка.
                int separatorPos = header.IndexOf(':');

                if (separatorPos == -1)
                {
                    request.isBadRequest = true;
                    return;
                }

                string headerName = header.Substring(0, separatorPos);
                string headerValue = header.Substring(separatorPos + 1).Trim(' ', '\t', '\r', '\n');

                request.Headers[headerName] = headerValue;

            }
        }

        static private string ReadLine(ByteReader ByteReader)
        {
            int linePosition = 0;

            while (true)
            {
                byte b;

                b = ByteReader.requestArray[ByteReader.position++];
                ByteReader.lineBuffer[linePosition++] = b;

                // Если считан символ '\n'.
                if (b == 10)
                {
                    break;
                }

                // Если достигнут максимальный предел размера буфера линии.
                if (linePosition == ByteReader.lineBuffer.Length)
                {
                    // Увеличиваем размер буфера линии в два раза.
                    byte[] newLineBuffer = new byte[ByteReader.lineBuffer.Length * 2];

                    ByteReader.lineBuffer.CopyTo(newLineBuffer, 0);
                    ByteReader.lineBuffer = newLineBuffer;
                }
            }


            return Encoding.ASCII.GetString(ByteReader.lineBuffer, 0, linePosition);
        }

        static private void ReceiveStartingLine(ByteReader ByteReader, Request request)
        {
            string startingLine;

            while (true)
            {
                startingLine = ReadLine(ByteReader);

                if (startingLine.Length == 0)
                {
                    request.isBadRequest = true;
                    break;
                }
                else if (startingLine == ByteReader.NewLine)
                {
                    continue;
                }
                else
                {
                    break;
                }
            }

            Match httpInfo = ParseStartLine(startingLine);
            string method = httpInfo.Groups["method"].Value;
            string nameOfFile = httpInfo.Groups["nameOfFile"].Value;
            string version = httpInfo.Groups["version"].Value;

            request.Method = (HttpMethod)Enum.Parse(typeof(HttpMethod), method);
            request.ProtocolVersion = Version.Parse(version);
            if (nameOfFile != null && request.Method == HttpMethod.GET)
                request.Parameters["nameOfFileInStartLine"] = nameOfFile;


            if (version.Length == 0 || method.Length == 0)
            {
                request.isBadRequest = true;
            }


        }

        static private Match ParseStartLine(string startingLine)
        {
            Regex myReg = new Regex(@"(?<method>.+)\s+\/(?<nameOfFile>.+)\s+(?<protocol>.+)\/(?<version>[\d\.\d]+)", RegexOptions.Multiline);
            Match httpInfo = myReg.Match(startingLine);
            if (httpInfo.Groups["nameOfFile"].Value == "")
            {
                myReg = new Regex(@"(?<method>.+)\s+\/\s+(?<protocol>.+)\/(?<version>[\d\.\d]+)", RegexOptions.Multiline);
                httpInfo = myReg.Match(startingLine);
            }
            return httpInfo;
        }
        #endregion

        #region Get response
        static private string HandleRequest(Request request)
        {
            if (request == null)
                return "Send bad request";

          else if (request.Parameters.ContainsKey("error"))
                return "Send error message";

            else if (request.Method == HttpMethod.GET)
                return HandleGetRequest(request);


            else if (request.Method == HttpMethod.POST)
                return HandlePostRequest(request);

            else
                return "Send bad request";
        }

        static private string HandleGetRequest(Request request)
        {

            if (request.Parameters["nameOfFileInStartLine"] == string.Empty)
                return "Send start page";

            else
                return "Send file";
        }

        static private string HandlePostRequest(Request request)
        {
            if (request.Parameters.ContainsKey("action"))
                switch (request.Parameters["action"])
                {
                    case "choose":
                        return "Send start modeling page";
                    case "upload":
                        return "Send upload page";
                    case "check":
                        return "Send Check model status page";
                }

            else if (request.Parameters.ContainsKey("nameOfModel"))
                return "Send name of the Model";

            else if (request.Parameters.ContainsKey("resultSimulation"))
                return "Send result of simulation";

            else if (request.Parameters.ContainsKey("nameOfProcess"))
                return "Send name of process";


            else if (request.Parameters.ContainsKey("process"))
                return "Send status of simulation";

            return string.Empty;
        }

        static private byte[] MakeResponse(string handleResult, Request request)
        {
            string serverName = "Modeling server v0.1";
            string version = "HTTP/1.1";
            StringBuilder sbHeader = new StringBuilder();
            byte[] responseArray;

            switch (handleResult)
            {
                case "Send start page":
                    {
                        string filePath = Environment.CurrentDirectory;
                        int index = filePath.IndexOf("bin");
                        filePath = filePath.Remove(index) + "web\\" + "Start page.html";

                        Byte[] bodyArray = File.ReadAllBytes(filePath);
                        sbHeader.AppendLine(
                        version + " " + HttpStatusCode.OK + "\r\n" +
                        "Server: " + serverName + "\r\n" +
                        "Content-Type: " + "text/html" + "\r\n" +
                        "Content-Length: " + bodyArray.Length + "\r\n" +
                           "\r\n");
                        return responseArray = MakeArray(sbHeader, bodyArray);

                    }

                case "Send file":
                    {
                        string filePath = Environment.CurrentDirectory;
                        int index = filePath.IndexOf("bin"); 
                        if(request.Parameters["nameOfFileInStartLine"] == "CheckModelStatusPage.html")
                        filePath = filePath.Remove(index) + "web\\" + "Check model status page.html";
                        else
                        filePath = filePath.Remove(index) + "storage\\" + $"{request.Parameters["nameOfFileInStartLine"]}";

                        if (File.Exists(filePath) && request.Parameters["nameOfFileInStartLine"] == "CheckModelStatusPage.html")
                        {
                            Byte[] bodyArray = File.ReadAllBytes(filePath);
                            sbHeader.AppendLine(
                            version + " " + HttpStatusCode.OK + "\r\n" +
                            "Server: " + serverName + "\r\n" +
                            "Content-Type: " + "text/html" + "\r\n" +
                            "Content-Length: " + bodyArray.Length + 
                               "\r\n");
                            return responseArray = MakeArray(sbHeader, bodyArray);
                        }
                        else if (File.Exists(filePath))
                        {
                            Byte[] bodyArray = File.ReadAllBytes(filePath);
                            sbHeader.AppendLine(
                            version + " " + HttpStatusCode.OK + "\r\n" +
                            "Server: " + serverName + "\r\n" +
                            "Content-Type: " + "multipart/form-data" + "\r\n" +
                            "Content-Length: " + bodyArray.Length +
                               "\r\n");
                            return responseArray = MakeArray(sbHeader, bodyArray);
                        }
                        else
                        {
                            filePath = Environment.CurrentDirectory;
                             index = filePath.IndexOf("bin");
                            filePath = filePath.Remove(index) + "web\\" + @"\" + @"404.html";
                            Byte[] bodyArray = File.ReadAllBytes(filePath);
                            sbHeader.AppendLine(
                            version + " " + HttpStatusCode.NotFound + "\r\n" +
                            "Server: " + serverName + "\r\n" +
                            "Content-Type: " + "text/html" + "\r\n" +
                            "Content-Length: " + bodyArray.Length +
                               "\r\n");

                            return responseArray = MakeArray(sbHeader, bodyArray);
                        }


                    }


                case "Send bad request":
                    {

                        Byte[] bodyArray = Encoding.ASCII.GetBytes($"BAD REQUEST");
                        sbHeader.AppendLine(
                        version + " " + HttpStatusCode.BadRequest + "\r\n" +
                        "Server: " + serverName + "\r\n" +
                        "Content-Type: " + "text/html" + "\r\n" +
                        "Content-Length: " + bodyArray.Length +
                           "\r\n");

                        return responseArray = MakeArray(sbHeader, bodyArray);

                    }

                case "Send start modeling page":
                    {
                        string filePath = Environment.CurrentDirectory;
                        int index = filePath.IndexOf("bin");
                        filePath = filePath.Remove(index) + "web\\" + "Start modeling page.html";

                        Byte[] bodyArray = File.ReadAllBytes(filePath);
                        sbHeader.AppendLine(
                        version + " " + HttpStatusCode.OK + "\r\n" +
                        "Server: " + serverName + "\r\n" +
                        "Content-Type: " + "text/html" + "\r\n" +
                        "Content-Length: " + bodyArray.Length +
                           "\r\n");

                        return responseArray = MakeArray(sbHeader, bodyArray);

                    }

                case "Send upload page":
                    {
                        string filePath = Environment.CurrentDirectory;
                        int index = filePath.IndexOf("bin");
                        filePath = filePath.Remove(index) + "web\\" + "Upload page.html";

                        Byte[] bodyArray = File.ReadAllBytes(filePath);
                        sbHeader.AppendLine(
                        version + " " + HttpStatusCode.OK + "\r\n" +
                        "Server: " + serverName + "\r\n" +
                        "Content-Type: " + "text/html" + "\r\n" +
                        "Content-Length: " + bodyArray.Length +
                           "\r\n");

                        return responseArray = MakeArray(sbHeader, bodyArray);

                    }

                case "Send Check model status page":
                    {
                        string filePath = Environment.CurrentDirectory;
                        int index = filePath.IndexOf("bin");
                        filePath = filePath.Remove(index) + "web\\" + "Check model status page.html";

                        Byte[] bodyArray = File.ReadAllBytes(filePath);
                        sbHeader.AppendLine(
                        version + " " + HttpStatusCode.OK + "\r\n" +
                        "Server: " + serverName + "\r\n" +
                        "Content-Type: " + "text/html" + "\r\n" +
                        "Content-Length: " + bodyArray.Length +
                           "\r\n");

                        return responseArray = MakeArray(sbHeader, bodyArray);

                    }


                case "Send name of the Model":
                    {
                        byte[] bodyArray = Encoding.UTF8.GetBytes(request.Parameters["nameOfModel"]);
                        sbHeader.AppendLine(
                        version + " " + HttpStatusCode.OK + "\r\n" +
                        "Server: " + serverName + "\r\n" +
                        "Content-Type: " + "text/html" + "\r\n" +
                        "Content-Length: " + bodyArray.Length +
                           "\r\n");
                        return responseArray = MakeArray(sbHeader, bodyArray);

                    }

                case "Send result of simulation":
                    {
                        byte[] bodyArray = Encoding.UTF8.GetBytes(request.Parameters["resultSimulation"]);
                        sbHeader.AppendLine(
                        version + " " + HttpStatusCode.OK + "\r\n" +
                        "Server: " + serverName + "\r\n" +
                        "Content-Type: " + "text/html" + "\r\n" +
                        "Content-Length: " + bodyArray.Length +
                           "\r\n");
                        return responseArray = MakeArray(sbHeader, bodyArray);

                    }

                case "Send status of simulation":
                    {
                        byte[] bodyArray = Encoding.UTF8.GetBytes(request.Parameters["process"]);
                        sbHeader.AppendLine(
                        version + " " + HttpStatusCode.OK + "\r\n" +
                        "Server: " + serverName + "\r\n" +
                        "Content-Type: " + "text/html" + "\r\n" +
                        "Content-Length: " + bodyArray.Length +
                           "\r\n");
                        return responseArray = MakeArray(sbHeader, bodyArray);
                    }

                case "Send name of process":
                    {
                        byte[] bodyArray = Encoding.UTF8.GetBytes("Modeling started. Name of process is " + request.Parameters["nameOfProcess"]);
                        sbHeader.AppendLine(
                        version + " " + HttpStatusCode.OK + "\r\n" +
                        "Server: " + serverName + "\r\n" +
                        "Content-Type: " + "text/html" + "\r\n" +
                        "Content-Length: " + bodyArray.Length +
                           "\r\n");
                        return responseArray = MakeArray(sbHeader, bodyArray);

                    }

                case "Send error message":
                    {
                        byte[] bodyArray = Encoding.UTF8.GetBytes(request.Parameters["error"]);
                        sbHeader.AppendLine(
                        version + " " + HttpStatusCode.OK + "\r\n" +
                        "Server: " + serverName + "\r\n" +
                        "Content-Type: " + "text/html" + "\r\n" +
                        "Content-Length: " + bodyArray.Length +
                           "\r\n");
                        return responseArray = MakeArray(sbHeader, bodyArray);
                    }


                default:
                    {
                        string filePath = Environment.CurrentDirectory;
                        int index = filePath.IndexOf("bin");
                        filePath = filePath.Remove(index) + "web\\" + "404.html";

                        Byte[] bodyArray = File.ReadAllBytes(filePath);
                        sbHeader.AppendLine(
                       version + " " + HttpStatusCode.NotFound + "\r\n" +
                        "Server: " + serverName + "\r\n" +
                        "Content-Type: " + "text/html" + "\r\n" +
                        "Content-Length: " + bodyArray.Length +
                           "\r\n");

                        return responseArray = MakeArray(sbHeader, bodyArray);
                    }
            }


        }


        static public byte[] MakeArray(StringBuilder sbHeader, Byte[] bodyArray)
        {

            List<byte> response = new List<byte>();
            response.AddRange(Encoding.ASCII.GetBytes(sbHeader.ToString()));
            response.AddRange(bodyArray);
            return response.ToArray();

        }
        #endregion

        #region Control.dll
        public static string GetEntryPointToLoadModel(byte[] arrayModel)
        {
            IntPtr in_params = Marshal.AllocHGlobal(arrayModel.Length);
            Marshal.Copy(arrayModel, 0, in_params, arrayModel.Length);
            uint out_byte_count;
            IntPtr out_params;
            uint a = ControlSystemEntryPoint(2, in_params, (uint)arrayModel.Length, out out_params, out out_byte_count);
            if (a == 0)
            {
                Marshal.FreeHGlobal(in_params);
                byte[] temp = new byte[out_byte_count];
                Marshal.Copy(out_params, temp, 0, (int)out_byte_count);
                ////   Marshal.FreeHGlobal(out_params);

                return Encoding.UTF8.GetString(temp);
            }
            else
            {
                uint pSize;
                IntPtr error = ControlSystemGetErrorDescription(a, out pSize);
                var result = Marshal.PtrToStringUTF8(error);
                return result;
            }
        }

        public static string GetEntryPointToStartModeling(Dictionary<string, string> options)
        {
            byte[] subjectArray = Encoding.UTF8.GetBytes(options["subject"]);
            byte[] nameOfModel = Encoding.UTF8.GetBytes(options["model"]);

            byte[] requestLine = new byte[subjectArray.Length + 4 + 4 + nameOfModel.Length];

            requestLine[0] = subjectArray.Length == 7 ? (byte)7 : (byte)8;
            requestLine[1] = 0;
            requestLine[2] = 0;
            requestLine[3] = 0;
            int k = 4;
            foreach (byte i in subjectArray)
            {
                requestLine[k] = i;
                k++;
            }
            UInt32 val = (UInt32)nameOfModel.Length;
            requestLine[k++] = (byte)(val & 0xff);
            requestLine[k++] = (byte)((val >> 8) & 0xff);
            requestLine[k++] = (byte)((val >> 16) & 0xff);
            requestLine[k++] = (byte)((val >> 24) & 0xff);
            foreach (byte i in nameOfModel)
            {
                requestLine[k] = i;
                k++;
            }

            IntPtr in_params = Marshal.AllocHGlobal(requestLine.Length);
            Marshal.Copy(requestLine, 0, in_params, requestLine.Length);


            uint out_byte_count;
            IntPtr out_params;
            uint a = ControlSystemEntryPoint(3, in_params, (uint)requestLine.Length, out out_params, out out_byte_count);


            if (a == 0)
            {
                Marshal.FreeHGlobal(in_params);
                byte[] temp = new byte[out_byte_count];
                Marshal.Copy(out_params, temp, 0, (int)out_byte_count);
                string process = Encoding.UTF8.GetString(temp);
                ProcessKeeper.WriteNewProcess(new Tuple<string, string>(process, options["subject"]));
                return process;
            }
            else
            {
                uint pSize;
                IntPtr error = ControlSystemGetErrorDescription(a, out pSize);
                var result = Marshal.PtrToStringUTF8(error);
                return result;
            }

        }

        private static string GetEntryPointToCheckStatus(Request request)
        {
            byte[] nameOfProcess = Encoding.UTF8.GetBytes(request.Parameters["process"]);
            byte[] nameOfProcessLength = BitConverter.GetBytes(nameOfProcess.Length);
            byte[] requestLine = new byte[nameOfProcessLength.Length + nameOfProcess.Length];

            int k = 0;
            foreach (byte i in nameOfProcessLength)
            {
                requestLine[k] = i;
                k++;
            }
            foreach (byte i in nameOfProcess)
            {
                requestLine[k] = i;
                k++;
            }

            IntPtr in_params = Marshal.AllocHGlobal(requestLine.Length);
            Marshal.Copy(requestLine, 0, in_params, requestLine.Length);
            uint out_byte_count;
            IntPtr out_params;
            uint a = ControlSystemEntryPoint(4, in_params, (uint)requestLine.Length, out out_params, out out_byte_count);

            if (a == 0)
            {
                Marshal.FreeHGlobal(in_params);
                byte[] temp = new byte[out_byte_count];
                Marshal.Copy(out_params, temp, 0, (int)out_byte_count);

                Console.WriteLine(Encoding.Default.GetString(temp));
                if (temp[0].ToString() == "0")
                    return "Процесс с заданным именем не существует";
                else if (temp[0].ToString() == "1")
                    return $"{DateTime.Now} - процесс выполняется";
                else if (temp[0].ToString() == "2")
                {
                    Tuple<byte[], string> result = GetEntryPointToGetResult(request);
                    if (result.Item1 != null)
                    {
                        string status = WriteToFile(result.Item1, request);

                        if (status == "Failed to write to file")
                            return status;

                        if (ProcessKeeper.GetSubjectAreaOfProcess(request.Parameters["process"]) == "radio_hf")
                        {
                            GetImage(request.Parameters["process"]);
                            return request.Headers["Host"] + "/" + $"{request.Parameters["process"]}.resultsbmp";
                        }
                        else
                            return request.Headers["Host"] + "/" + $"{request.Parameters["process"]}.results";
                    }
                    else
                        return result.Item2;
                }

                else
                    return "Unknown error";
            }
            else
            {
                uint pSize;
                IntPtr error = ControlSystemGetErrorDescription(a, out pSize);
                var result = Marshal.PtrToStringUTF8(error);
                return result;
            }

        }


        private static Tuple<byte[], string> GetEntryPointToGetResult(Request request)
        {
            byte[] nameOfProcess = Encoding.UTF8.GetBytes(request.Parameters["process"]);
            byte[] nameOfProcessLength = BitConverter.GetBytes(nameOfProcess.Length);
            byte[] requestLine = new byte[nameOfProcessLength.Length + nameOfProcess.Length];

            int k = 0;
            foreach (byte i in nameOfProcessLength)
            {
                requestLine[k] = i;
                k++;
            }
            foreach (byte i in nameOfProcess)
            {
                requestLine[k] = i;
                k++;
            }

            IntPtr in_params = Marshal.AllocHGlobal(requestLine.Length);
            Marshal.Copy(requestLine, 0, in_params, requestLine.Length);
            uint out_byte_count;
            IntPtr out_params;
            uint a = ControlSystemEntryPoint(5, in_params, (uint)requestLine.Length, out out_params, out out_byte_count);

            if (a == 0)
            {
                Marshal.FreeHGlobal(in_params);
                byte[] result = new byte[out_byte_count];
                Marshal.Copy(out_params, result, 0, (int)out_byte_count);
                GetEntryPointToCloseProcess(request);
                return new Tuple<byte[], string>(result, null);
            }
            else
            {
                uint pSize;
                IntPtr error = ControlSystemGetErrorDescription(a, out pSize);
                var result = Marshal.PtrToStringUTF8(error);
                return new Tuple<byte[], string>(null, result);
            }

        }

        private static void GetEntryPointToCloseProcess(Request request)
        {
            byte[] nameOfProcess = Encoding.UTF8.GetBytes(request.Parameters["process"]);
            byte[] nameOfProcessLength = BitConverter.GetBytes(nameOfProcess.Length);
            byte[] requestLine = new byte[nameOfProcessLength.Length + nameOfProcess.Length];

            int k = 0;
            foreach (byte i in nameOfProcessLength)
            {
                requestLine[k] = i;
                k++;
            }
            foreach (byte i in nameOfProcess)
            {
                requestLine[k] = i;
                k++;
            }

            IntPtr in_params = Marshal.AllocHGlobal(requestLine.Length);
            Marshal.Copy(requestLine, 0, in_params, requestLine.Length);
            uint out_byte_count;
            IntPtr out_params;
            uint a = ControlSystemEntryPoint(7, in_params, (uint)requestLine.Length, out out_params, out out_byte_count);
        }

        #endregion

        #region Запись в файл
        static private string WriteToFile(byte[] result, Request request)
        {

            string path = Environment.CurrentDirectory;
            int just = path.IndexOf("bin");
            path = path.Remove(just) + "storage\\" + $"{request.Parameters["process"]}.results";
            try
            {
                using (var fileStream = new FileStream(path, FileMode.Create))
                {
                    fileStream.Write(result, 0, result.Length);
                }
                return $"{request.Parameters["process"]}.results";
            }
            catch
            {
                return "Failed to write to file";
            }
        }
        #endregion


        #region visualisation
        private static void GetImage(string resultName)
        {
            resultName = resultName + ".results";
            ProcessStartInfo psi = new ProcessStartInfo();
            string pathToExe = Environment.CurrentDirectory;
            int index = pathToExe.IndexOf("bin");
            pathToExe = pathToExe.Remove(index) + "visualisationComponents\\" + "radio_hf_vis.exe";
            string pathToResult = Environment.CurrentDirectory;
            pathToResult = pathToResult.Remove(index) + "storage\\" + $"{resultName}";

            //Имя запускаемого приложения
            psi.FileName = "cmd";
            //команда, которую надо выполнить
            psi.Arguments = $@"/c start """" ""{pathToExe}"" {pathToResult}";
            //  /c - после выполнения команды консоль закроется
            //  /к - не закрывать консоль после выполнения команды
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            Process.Start(psi);
            Thread.Sleep(3000);

            if (File.Exists(pathToResult + ".bmp"))
            {
                Bitmap img = new Bitmap(pathToResult + ".bmp");
                img.Save(pathToResult + ".jpg");
            }

        }
        #endregion
    }
}
