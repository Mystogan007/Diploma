using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace HttpServer
{
    static public class Parser
    {        
        static public Request ParseAndMakeRequest(string incomingData)
        {
            if (string.IsNullOrEmpty(incomingData))
                return null;


            Regex myReg = new Regex(@"(?<method>.+)\s+\/(?<nameOfFile>.+)\s+(?<protocol>.+)\/(?<version>[\d\.\d]+)", RegexOptions.Multiline);
            Match httpInfo = myReg.Match(incomingData);
            if (httpInfo.Groups["nameOfFile"].Value == "")
            {
                myReg = new Regex(@"(?<method>.+)\s+\/\s+(?<protocol>.+)\/(?<version>[\d\.\d]+)", RegexOptions.Multiline);
                httpInfo = myReg.Match(incomingData);
            }


            string method = httpInfo.Groups["method"].Value;
            string nameOfFile = httpInfo.Groups["nameOfFile"].Value;
            string protocol = httpInfo.Groups["protocol"].Value;
            string version = httpInfo.Groups["version"].Value;

            string headers = incomingData.Replace($"{httpInfo}", "");
            headers = headers.Replace($"\nHost", "Host");

            if (method != "GET" && method != "POST")
                return null;
            if (protocol != "HTTP")
                return null;
            if (version != "1.1" && method != "1.0")
                return null;

            Request request = new Request(method, protocol, version, nameOfFile);



            myReg = new Regex(@"^(?<key>[^\x3A]+)\:\s{1}(?<value>.+)$", RegexOptions.Multiline);
            MatchCollection mc = myReg.Matches(headers);
            foreach (Match mm in mc)
            {
                string key = mm.Groups["key"].Value;
                string value = mm.Groups["value"].Value;
                if (key.Trim().ToLower() == "host")
                {
                    request.Host = value;
                    continue;
                }
                request.Items.Add(key, value);
            }
            return request;

            
        }

        static public Response ParseAndMakeResponse(Request request)
        {
            if (request == null)
                return MakeNullRequest();

            if (request.Method == "GET")
            {
                if (request.Host == "127.0.0.1:8080")
                {
                    String filePath = Environment.CurrentDirectory + HttpServer.MsgPath + @"\";
                    if (request.NameOfFile != "")
                    {
                        DirectoryInfo dInfo = new DirectoryInfo(filePath);
                        FileInfo[] files = dInfo.GetFiles();
                        foreach (var file in files)
                        {

                            if (file.Name == request.NameOfFile)
                            {
                                filePath = filePath + file.Name;
                                return MakeRequestFromFile(filePath);
                            }

                        }
                        return MakeRequestPageNotFound();
                    }
                    filePath = filePath + @"index.html";
                    FileInfo fInfo = new FileInfo(filePath);
                    return MakeRequestFromFile(fInfo);
                }



            }





            else if (request.Method == "POST")
            {
                // в разработке
            }



            return MakeRequestPageNotFound();


        }

        private static Response MakeRequestFromFile(FileInfo fInfo)
        {
            FileStream fS = fInfo.OpenRead();
            BinaryReader reader = new BinaryReader(fS);
            Byte[] data = new byte[fS.Length];
            reader.Read(data, 0, data.Length);
            fS.Close();
            return new Response("200 OK", "multipart/form-data", data);
        }

        private static Response MakeRequestFromFile(string filepath)
        {
            Byte[] data = File.ReadAllBytes(filepath);

            return new Response("200 OK", "text/html", data);
        }

        private static Response MakeNullRequest()
        {
            String filePath = Environment.CurrentDirectory + HttpServer.MsgPath + @"\" + "400.html";
            FileInfo fI = new FileInfo(filePath);
            FileStream fS = fI.OpenRead();
            BinaryReader reader = new BinaryReader(fS);
            Byte[] data = new byte[fS.Length];
            reader.Read(data, 0, data.Length);
            fS.Close();
            return new Response("400 Bad Request", "text/html", data);
        }

        private static Response MakeRequestNotAllowedMethod()
        {
            String filePath = Environment.CurrentDirectory + HttpServer.MsgPath + @"\" + "405.html";
            FileInfo fI = new FileInfo(filePath);
            FileStream fS = fI.OpenRead();
            BinaryReader reader = new BinaryReader(fS);
            Byte[] data = new byte[fS.Length];
            reader.Read(data, 0, data.Length);
            fS.Close();
            return new Response("405 Method Not Allowed", "text/html", data);
        }

        private static Response MakeRequestPageNotFound()
        {
            String filePath = Environment.CurrentDirectory + HttpServer.MsgPath + @"\404.html";
            // filePath = filePath.Replace(@"\\", @"\");

            FileInfo fI = new FileInfo(filePath);
            FileStream fS = fI.OpenRead();
            BinaryReader reader = new BinaryReader(fS);
            Byte[] data = new byte[fS.Length];
            reader.Read(data, 0, data.Length);
            return new Response("404 Page not found", "text/html", data);
        }

    }
}
