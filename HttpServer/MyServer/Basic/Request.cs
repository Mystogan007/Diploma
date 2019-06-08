using HttpServer.MyServer.Support;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;


namespace HttpServer
{
    /// <summary>
    /// Представляет класс, предназначеннный для загрузки запроса.
    /// </summary>
    public sealed class Request
    {
        private const int InitialLineSize = 1000;

        #region Поля (закрытые)        
        private int _linePosition;

        private string TempPath = @"\root\temp";

        private byte[] _lineBuffer = new byte[InitialLineSize];

        private int Position;

        private Dictionary<string, string> _headers =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, byte[]> _boundaries =
    new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        private byte[] _requestArray;

        #endregion

        #region Свойства

        public Dictionary<string, string> Parameters  { get; private set; }


        public bool HasError { get; private set; }

        #endregion


        #region Основные данные

                             /// <summary>
        /// Возвращает HTTP-метод, используемый при запросе.
        /// </summary>
        public HttpMethod Method { get; private set; }


        /// <summary>
        /// Возвращает версию HTTP-протокола, используемую в ответе.
        /// </summary>
        public Version ProtocolVersion { get; set; }

        #endregion

        #region HTTP-заголовки
               
        /// <summary>
        /// Возвращает длину тела сообщения.
        /// </summary>
        /// <value>Длина тела сообщения, если соответствующий заголок задан, иначе -1.</value>
        public int ContentLength { get; set; }

        /// <summary>
        /// Возвращает тип содержимого ответа.
        /// </summary>
        /// <value>Тип содержимого ответа, если соответствующий заголок задан, иначе пустая строка.</value>
        public string ContentType { get; set; }





        #endregion

                                   

        public Request(byte[] requestBytes)
        {
            _requestArray = requestBytes;
            ContentLength = -1;
            ContentType = string.Empty;
            Position = 0;
            Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        }


        #region Методы (открытые)
       

        #endregion
        // Загружает запрос
        public void LoadRequest()
        {
            HasError = false;

            _headers.Clear();

            ReceiveStartingLine();
            if (HasError)
                return;

            ReceiveHeaders();
            if (HasError)
                return;

            ContentLength = GetContentLength();
            ContentType = GetContentType();

            if (ContentLength != -1 && Method == HttpMethod.POST)
            {
                ParseBody();
            }
        }

        #region Методы (закрытые)

        private void WriteToFile(string path, Tuple<int, int> specifiedPart)
        {
            try
            {
                using (var fileStream = new FileStream(path, FileMode.Create))
                {
                    byte[] source = ReceivePartOfBodyBytes(specifiedPart);

                    fileStream.Write(source, 0, source.Length);
                }
            }
            catch
            {
                HasError = true;
            }
        }

        private string ReadLine()
        {
            _linePosition = 0;

            while (true)
            {
                byte b;

                b = _requestArray[Position++];
                _lineBuffer[_linePosition++] = b;

                // Если считан символ '\n'.
                if (b == 10)
                {
                    break;
                }

                // Если достигнут максимальный предел размера буфера линии.
                if (_linePosition == _lineBuffer.Length)
                {
                    // Увеличиваем размер буфера линии в два раза.
                    byte[] newLineBuffer = new byte[_lineBuffer.Length * 2];

                    _lineBuffer.CopyTo(newLineBuffer, 0);
                    _lineBuffer = newLineBuffer;
                }
            }


            return Encoding.ASCII.GetString(_lineBuffer, 0, _linePosition);
        }

        #region Загрузка начальных данных

        private void ReceiveStartingLine()
        {
            string startingLine;

            while (true)
            {
                startingLine = ReadLine();

                if (startingLine.Length == 0)
                {
                    HasError = true;
                    break;
                }
                else if (startingLine == Http.NewLine)
                {
                    continue;
                }
                else
                {
                    break;
                }
            }

            Match httpInfo = ParseStartLine(startingLine);
            //   string method = startingLine.Substring(" ", " / ");
            //  string version = startingLine.Substring("HTTP/", " ");
            string method = httpInfo.Groups["method"].Value;
            string nameOfFile = httpInfo.Groups["nameOfFile"].Value;
            string version = httpInfo.Groups["version"].Value;

            Method = (HttpMethod)Enum.Parse(typeof(HttpMethod), method);
            ProtocolVersion = Version.Parse(version);
            if (nameOfFile != null && Method == HttpMethod.GET)
                Parameters["nameOfFileInStartLine"] = nameOfFile;


            if (version.Length == 0 || method.Length == 0)
            {
                HasError = true;
            }


        }
        private void ReceiveHeaders()
        {
            while (true)
            {
                string header = ReadLine();



                // Если достигнут конец заголовков.
                if (header == Http.NewLine)
                    return;

                // Ищем позицию между именем и значением заголовка.
                int separatorPos = header.IndexOf(':');

                if (separatorPos == -1)
                {
                    HasError = true;
                    return;
                }

                string headerName = header.Substring(0, separatorPos);
                string headerValue = header.Substring(separatorPos + 1).Trim(' ', '\t', '\r', '\n');

                _headers[headerName] = headerValue;

            }
        }

        private Match ParseStartLine(string startingLine)
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

        #region Загрузка тела сообщения

        private void ParseBody()
        {
            if (_boundaries.ContainsKey("boundary"))
            {
                List<Tuple<int, int>> parts = GetNumberAndIndexPartsOfBody();
                foreach (var part in parts)
                {
                    try
                    {
                        Tuple<int, int> specifiedPart = CheckBodyHeaders(part);
                        if (Parameters.ContainsKey("nameOfFileInBody"))
                        {
                            Parameters["nameOfModel"] = Pinvoke.GetEntryPointToLoadModel(ReceivePartOfBodyBytes(specifiedPart));
                        }
                        else
                            GetParameters(specifiedPart);
                    }
                    catch
                    {
                        HasError = true;
                        return;
                    }
                }
                if(Parameters.ContainsKey("subject") && Parameters.ContainsKey("model"))
                {
                    Parameters["nameOfProcess"] = Pinvoke.GetEntryPointToStartModeling(Parameters);
                }

            }
        }

        private void GetParameters(Tuple<int, int> specifiedPart)
        {
            byte[] source = ReceivePartOfBodyBytes(specifiedPart);
            string line = Encoding.ASCII.GetString(source);
            if (Parameters.ContainsKey("action"))
            {
                Parameters["action"] = line;
                return;
            }
               
            if (Parameters.ContainsKey("subject"))
            {
                Parameters["subject"] = line;
                return;
            }
                
            if (Parameters.ContainsKey("model"))
                Parameters["model"] = line;
        }

        private byte[] ReceivePartOfBodyBytes(Tuple<int, int> specifiedPart)
        {
            int startIndex = specifiedPart.Item1;
            int finishIndex = specifiedPart.Item2;
            int length = finishIndex - startIndex;
            byte[] temp = new byte[length];
            Array.Copy(_requestArray, startIndex, temp, 0, length);
            return temp;
        }

        private Tuple<int, int> CheckBodyHeaders(Tuple<int, int> part)
        {
            Position = part.Item1 + _boundaries["boundary"].Length;

            while (Position != part.Item2)
            {

                string line = ReadLine();

                // Если достигнут конец заголовков тела.
                if (line == Http.NewLine)
                {
                    Tuple<int, int> specifiedPart = new Tuple<int, int>(Position, part.Item2);
                    return specifiedPart;
                }
                // Ищем имя файла в заголовках тела.
                if (line.Contains("filename"))
                {
                    Regex myReg = new Regex($@"filename=""(?<nameOfFile>.+)""", RegexOptions.Multiline);
                    Match name = myReg.Match(line);
                    Parameters["nameOfFileInBody"] = name.Groups["nameOfFile"].Value;
                }
                // Ищем имя параметра в заголовках тела.
                else if (line.Contains("name"))
                {
                    Regex myReg = new Regex($@"name=""(?<name>.+)""", RegexOptions.Multiline);
                    Match name = myReg.Match(line);
                    Parameters[$"{name.Groups["name"].Value}"] = "";
                }
                
            }
            return null;
        }

        private List<Tuple<int, int>> GetNumberAndIndexPartsOfBody()
        {
            int start = 0;
            int finish = 0;
            int flag = 0;
            List<int> temp = new List<int>();
            List<Tuple<int, int>> parts = new List<Tuple<int, int>>();

            foreach (var position in _requestArray.Locate(_boundaries["boundary"]))
            {
                temp.Add(position);
            }
            foreach (var position in _requestArray.Locate(_boundaries["finishBoundary"]))
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

        #endregion

        #region Получение значения HTTP-заголовков


        private int GetContentLength()
        {
            if (_headers.ContainsKey("Content-Length"))
            {
                int contentLength;
                int.TryParse(_headers["Content-Length"], out contentLength);
                return contentLength;
            }

            return -1;
        }

        private string GetContentType()
        {
            if (_headers.ContainsKey("Content-Type"))
            {
                string contentType = _headers["Content-Type"];

                if (contentType.Contains("boundary"))
                {
                    Regex myReg = new Regex($@"boundary=(?<boundary>.+)", RegexOptions.Multiline);
                    Match name = myReg.Match(contentType);
                    _boundaries["boundary"] = Encoding.ASCII.GetBytes("--" + name.Groups["boundary"].Value + "\r\n");
                    _boundaries["finishBoundary"] = Encoding.ASCII.GetBytes("--" + name.Groups["boundary"].Value + "--\r\n");

                }

                // Ищем позицию, где заканчивается описание типа контента и начинается описание его параметров.
                int endTypePos = contentType.IndexOf(';');
                if (endTypePos != -1)
                    contentType = contentType.Substring(0, endTypePos);

                return contentType;
            }

            return string.Empty;
        }



        #endregion


        #endregion








    }
}
