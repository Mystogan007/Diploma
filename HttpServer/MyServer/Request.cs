using HttpServer.MyServer.Support;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;


namespace HttpServer
{
    /// <summary>
    /// Представляет класс, предназначеннный для загрузки запроса.
    /// </summary>
    public sealed class Request
    {
        #region Классы (закрытые)

        // Обёртка для массива байтов.
        // Указывает реальное количество байтов содержащихся в массиве.
        private sealed class BytesWraper
        {
            public int Length { get; set; }

            public byte[] Value { get; set; }
        }

        // Данный класс используется для загрузки начальных данных.
        // Но он также используется и для загрузки тела сообщения, точнее, из него просто выгружается остаток данных, полученный при загрузки начальных данных.
        private sealed class ReceiverHelper
        {
            private const int InitialLineSize = 1000;


            #region Поля (закрытые)

            private Stream _stream;

            private byte[] _buffer;
            private int _bufferSize;

            private int _linePosition;
            private byte[] _lineBuffer = new byte[InitialLineSize];

            #endregion


            #region Свойства (открытые)

            public bool HasData
            {
                get
                {
                    return (Length - Position) != 0;
                }
            }

            public int Length { get; private set; }

            public int Position { get; private set; }

            #endregion


            public ReceiverHelper(int bufferSize)
            {
                _bufferSize = bufferSize;
                _buffer = new byte[_bufferSize];
            }


            #region Методы (открытые)

            public void Init(Stream stream)
            {
                _stream = stream;
                _linePosition = 0;

                Length = 0;
                Position = 0;
            }

            public string ReadLine()
            {
                _linePosition = 0;

                while (true)
                {
                    if (Position == Length)
                    {
                        Position = 0;
                        Length = _stream.Read(_buffer, 0, _bufferSize);

                        if (Length == 0)
                        {
                            break;
                        }
                    }

                    byte b = _buffer[Position++];

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

            public int Read(byte[] buffer, int index, int length)
            {
                int curLength = Length - Position;

                if (curLength > length)
                {
                    curLength = length;
                }

                Array.Copy(_buffer, Position, buffer, index, curLength);

                Position += curLength;

                return curLength;
            }

            #endregion
        }

        // Данный класс используется при загрузки сжатых данных.
        // Он позволяет определить точное количество считаных байт (сжатых данных).
        // Это нужно, так как потоки для считывания сжатых данных сообщают количество байт уже преобразованных данных.
        private sealed class ZipWraperStream : Stream
        {
            #region Поля (закрытые)

            private Stream _baseStream;
            private ReceiverHelper _receiverHelper;

            #endregion


            #region Свойства (открытые)

            public int BytesRead { get; private set; }

            public int TotalBytesRead { get; set; }

            public int LimitBytesRead { get; set; }

            #region Переопределённые

            public override bool CanRead
            {
                get
                {
                    return _baseStream.CanRead;
                }
            }

            public override bool CanSeek
            {
                get
                {
                    return _baseStream.CanSeek;
                }
            }

            public override bool CanTimeout
            {
                get
                {
                    return _baseStream.CanTimeout;
                }
            }

            public override bool CanWrite
            {
                get
                {
                    return _baseStream.CanWrite;
                }
            }

            public override long Length
            {
                get
                {
                    return _baseStream.Length;
                }
            }

            public override long Position
            {
                get
                {
                    return _baseStream.Position;
                }
                set
                {
                    _baseStream.Position = value;
                }
            }

            #endregion

            #endregion


            public ZipWraperStream(Stream baseStream, ReceiverHelper receiverHelper)
            {
                _baseStream = baseStream;
                _receiverHelper = receiverHelper;
            }


            #region Методы (открытые)

            public override void Flush()
            {
                _baseStream.Flush();
            }

            public override void SetLength(long value)
            {
                _baseStream.SetLength(value);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return _baseStream.Seek(offset, origin);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                // Если установлен лимит на количество считанных байт.
                if (LimitBytesRead != 0)
                {
                    int length = LimitBytesRead - TotalBytesRead;

                    // Если лимит достигнут.
                    if (length == 0)
                    {
                        return 0;
                    }

                    if (length > buffer.Length)
                    {
                        length = buffer.Length;
                    }

                    if (_receiverHelper.HasData)
                    {
                        BytesRead = _receiverHelper.Read(buffer, offset, length);
                    }
                    else
                    {
                        BytesRead = _baseStream.Read(buffer, offset, length);
                    }
                }
                else
                {
                    if (_receiverHelper.HasData)
                    {
                        BytesRead = _receiverHelper.Read(buffer, offset, count);
                    }
                    else
                    {
                        BytesRead = _baseStream.Read(buffer, offset, count);
                    }
                }

                TotalBytesRead += BytesRead;

                return BytesRead;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _baseStream.Write(buffer, offset, count);
            }

            #endregion
        }

        private struct BodyContentIndex
        {
            int startPosition;
            int finishPosition;
        }

        #endregion

        #region Поля (закрытые)


        private ReceiverHelper _receiverHelper;

        private Dictionary<string, string> _headers =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private TcpClient _client;


        #endregion


        #region Свойства (открытые)

        /// <summary>
        /// Возвращает значение, указывающие, произошла ли ошибка во время обработки первой строки запроса.
        /// </summary>
        public bool HasErrorParseStartLine { get; private set; }

        /// <summary>
        /// Возвращает значение, указывающие, произошла ли ошибка во время заголовков запроса.
        /// </summary>
        public bool HasErrorParseHeaders { get; private set; }

        /// <summary>
        /// Возвращает значение, указывающие, произошла ли ошибка во время загрузки тела запроса.
        /// </summary>

        public bool HasErrorLoadBody { get; private set; }




        /// <summary>
        /// Возвращает значение, указывающие, загружено ли тело сообщения.
        /// </summary>
        public bool MessageBodyLoaded { get; private set; }


        #endregion

        #region Основные данные

        /// <summary>
        /// Возвращает HTTP-метод, используемый при запросе.
        /// </summary>
        public HttpMethod Method { get; private set; }

        /// <summary>
        /// Возвращает имя файла, который запрашивается Get запросом.
        /// </summary>

        public string NameOfFile { get; private set; }

        /// <summary>
        /// Возвращает версию HTTP-протокола, используемую в ответе.
        /// </summary>
        public Version ProtocolVersion { get; set; }

        #endregion

        #region HTTP-заголовки

        /// <summary>
        /// Возвращает кодировку тела сообщения.
        /// </summary>
        /// <value>Кодировка тела сообщения, если соответствующий заголок задан, иначе значение заданное в <see cref="xNet.Net.HttpRequest"/>. Если и оно не задано, то значение <see cref="System.Text.Encoding.Default"/>.</value>
        public Encoding CharacterSet { get; set; }

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


        #region Индексаторы (открытые)

        /// <summary>
        /// Возвращает значение HTTP-заголовка.
        /// </summary>
        /// <param name="headerName">Название HTTP-заголовка.</param>
        /// <value>Значение HTTP-заголовка, если он задан, иначе пустая строка.</value>

        public string this[string headerName]
        {
            get
            {
                #region Проверка параметра

                //if (headerName == null)
                //{
                //    throw new ArgumentNullException("headerName");
                //}

                //if (headerName.Length == 0)
                //{
                //    throw ExceptionHelper.EmptyString("headerName");
                //}

                #endregion

                string value;

                if (!_headers.TryGetValue(headerName, out value))
                {
                    value = string.Empty;
                }

                return value;
            }
        }

        /// <summary>
        /// Возвращает значение HTTP-заголовка.
        /// </summary>
        /// <param name="header">HTTP-заголовок.</param>
        /// <value>Значение HTTP-заголовка, если он задан, иначе пустая строка.</value>
        public string this[HttpHeader header]
        {
            get
            {
                return this[Http.Headers[header]];
            }
        }

        #endregion


        public Request(TcpClient client)
        {
            _client = client;

            ContentLength = -1;
            ContentType = string.Empty;
        }


        #region Методы (открытые)

        /// <summary>
        /// Загружает тело сообщения и сохраняет его в новый файл по указанному пути. Если файл уже существует, то он будет перезаписан.
        /// </summary>
        public void ToFile(string path)
        {
            if (MessageBodyLoaded)
            {
                return;
            }

            try
            {
                using (var fileStream = new FileStream(path, FileMode.Create))
                {
                    IEnumerable<BytesWraper> source = GetMessageBodySource();

                    foreach (var bytes in source)
                    {
                        fileStream.Write(bytes.Value, 0, bytes.Length);
                    }
                }
                MessageBodyLoaded = true;
            }
            catch
            {
               HasErrorLoadBody = true;

            }

        }

        #endregion

        #region Работа с заголовками

        /// <summary>
        /// Возвращает перечисляемую коллекцию HTTP-заголовков.
        /// </summary>
        /// <returns>Коллекция HTTP-заголовков.</returns>
        public Dictionary<string, string>.Enumerator EnumerateHeaders()
        {
            return _headers.GetEnumerator();
        }

        #endregion




        // Загружает запрос
        internal void LoadRequest()
        {
            HasErrorParseStartLine = false;
            HasErrorParseHeaders = false;
            HasErrorLoadBody = false;
            MessageBodyLoaded = false;

            _headers.Clear();

            if (_receiverHelper == null)
            {
                _receiverHelper = new ReceiverHelper(
                    _client.ReceiveBufferSize);
            }

            _receiverHelper.Init(_client.GetStream());

            ReceiveStartingLine();
            if (HasErrorParseStartLine)
                return;

      
                ReceiveHeaders();

            if (HasErrorParseHeaders)
                return;

                ContentLength = GetContentLength();
                ContentType = GetContentType();

            

            

            // Если пришёл запрос без тела сообщения.
            if (ContentLength == -1 )
            {
                MessageBodyLoaded = true;
            }
            else if (Method == HttpMethod.POST)
            {
                ReceiveNameOfFile();

            }

            //long responseSize = _receiverHelper.Position;

            //if (ContentLength > 0)
            //{
            //    responseSize += ContentLength;
            //}
        }

        internal void CloseClient()
        {
            _client.GetStream().Dispose();
       
            _client.Dispose();
            _client.Close();

        }


        #region Методы (закрытые)

        #region Загрузка начальных данных

        private void ReceiveStartingLine()
        {
            string startingLine;

            while (true)
            {
                startingLine = _receiverHelper.ReadLine();
                if (startingLine.Length == 0)
                {
                    HasErrorParseStartLine = true;
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
                NameOfFile = nameOfFile;


            if (version.Length == 0 || method.Length == 0)
            {
                HasErrorParseStartLine = true;
            }


        }
        private void ReceiveHeaders()
        {
            while (true)
            {
                string header = _receiverHelper.ReadLine();

                // Если достигнут конец заголовков.
                if (header == Http.NewLine)
                    return;

                // Ищем позицию между именем и значением заголовка.
                int separatorPos = header.IndexOf(':');

                if (separatorPos == -1)
                {
                    HasErrorParseHeaders = true;
                    return;
                }

                string headerName = header.Substring(0, separatorPos);
                string headerValue = header.Substring(separatorPos + 1).Trim(' ', '\t', '\r', '\n');

                _headers[headerName] = headerValue;

            }
        }

        private void ReceiveNameOfFile()
        {
            while (true)
            {
                string line = _receiverHelper.ReadLine();

                // Если достигнут конец заголовков тела.
                if (line == Http.NewLine)
                    return;

                // Ищем имя файла в заголовках тела.
                Regex myReg = new Regex($@"filename=""(?<nameOfFile>.+)"")", RegexOptions.Multiline);
                Match name = myReg.Match(line);
                if(name.Length > 0)
                    NameOfFile = name.Groups["nameOfFile"].Value;




            }
        }

        private Match ParseStartLine (string startingLine)
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

        private IEnumerable<BytesWraper> GetMessageBodySource()
        {
            if (_headers.ContainsKey("Content-Encoding"))
            {
                return GetMessageBodySourceZip();
            }

            return GetMessageBodySourceStd();
        }

        // Загрузка обычных данных.
        private IEnumerable<BytesWraper> GetMessageBodySourceStd()
        {
            if (_headers.ContainsKey("Transfer-Encoding"))
            {
                return ReceiveMessageBodyChunked();
            }

            if (ContentLength != -1)
            {
              //  return ReceiveMessageBody(ContentLength);
                return ReceiveMessageBodyTest(ContentLength);
            }

            return ReceiveMessageBody(_client.GetStream());
        }

        // Загрузка сжатых данных.
        private IEnumerable<BytesWraper> GetMessageBodySourceZip()
        {
            if (_headers.ContainsKey("Transfer-Encoding"))
            {
                return ReceiveMessageBodyChunkedZip();
            }

            if (ContentLength != -1)
            {
                return ReceiveMessageBodyZip(ContentLength);
            }

            var streamWrapper = new ZipWraperStream(
                _client.GetStream(), _receiverHelper);

            return ReceiveMessageBody(GetZipStream(streamWrapper));
        }


        // Загрузка тела сообщения неизвестной длины.
        private IEnumerable<BytesWraper> ReceiveMessageBody(Stream stream)
        {
            var bytesWraper = new BytesWraper();

            int bufferSize = _client.ReceiveBufferSize;
            byte[] buffer = new byte[bufferSize];

            bytesWraper.Value = buffer;

            int begBytesRead = 0;

            // Считываем начальные данные из тела сообщения.
            if (stream is GZipStream || stream is DeflateStream)
            {
                begBytesRead = stream.Read(buffer, 0, bufferSize);
            }
            else
            {
                if (_receiverHelper.HasData)
                {
                    begBytesRead = _receiverHelper.Read(buffer, 0, bufferSize);
                }

                if (begBytesRead < bufferSize)
                {
                    begBytesRead += stream.Read(buffer, begBytesRead, bufferSize - begBytesRead);
                }
            }

            // Возвращаем начальные данные.
            bytesWraper.Length = begBytesRead;
            yield return bytesWraper;



            while (true)
            {
                int bytesRead = stream.Read(buffer, 0, bufferSize);


                 if (bytesRead == 0)
                {
                    yield break;
                }

                bytesWraper.Length = bytesRead;
                yield return bytesWraper;
            }
        }

        // Загрузка тела сообщения известной длины.
        private IEnumerable<BytesWraper> ReceiveMessageBody(int contentLength)
        {
            Stream stream = _client.GetStream();
            var bytesWraper = new BytesWraper();

            int bufferSize = _client.ReceiveBufferSize;
            byte[] buffer = new byte[bufferSize];

            bytesWraper.Value = buffer;

            int totalBytesRead = 0;

            while (totalBytesRead != contentLength)
            {
                int bytesRead;

                if (_receiverHelper.HasData)
                {
                    bytesRead = _receiverHelper.Read(buffer, 0, bufferSize);
                }
                else
                {
                    bytesRead = stream.Read(buffer, 0, bufferSize);
                }

                if (bytesRead == 0)
                {
                    WaitData();
                }
                else
                {
                    totalBytesRead += bytesRead;

                    bytesWraper.Length = bytesRead;
                    yield return bytesWraper;
                }
            }
        }

        // Загрузка тела сообщения только с файлом.
        private IEnumerable<BytesWraper> ReceiveMessageBodyTest(int contentLength)
        {
            bool endOfFile = false;
            Stream stream = _client.GetStream();
            var bytesWraper = new BytesWraper();

            int bufferSize = _client.ReceiveBufferSize;
            byte[] buffer = new byte[bufferSize];

            bytesWraper.Value = buffer;

            int totalBytesRead = 0;

            while (!endOfFile)
            {
                
                int bytesRead;

                if (_receiverHelper.HasData)
                {
                    bytesRead = _receiverHelper.Read(buffer, 0, bufferSize);
                }
                else
                {
                    bytesRead = stream.Read(buffer, 0, bufferSize);
                }

                if (bytesRead == 0)
                {
                    WaitData();
                }
                else
                {
                    totalBytesRead += bytesRead;

                    bytesWraper.Length = bytesRead;
                    yield return bytesWraper;
                }
            }
        }


        // Загрузка тела сообщения частями.
        private IEnumerable<BytesWraper> ReceiveMessageBodyChunked()
        {
            Stream stream = _client.GetStream();
            var bytesWraper = new BytesWraper();

            int bufferSize = _client.ReceiveBufferSize;
            byte[] buffer = new byte[bufferSize];

            bytesWraper.Value = buffer;

            while (true)
            {
                string line = _receiverHelper.ReadLine();

                // Если достигнут конец блока.
                if (line == Http.NewLine)
                    continue;

                line = line.Trim(' ', '\r', '\n');

                // Если достигнут конец тела сообщения.
                if (line == string.Empty)
                    yield break;

                int blockLength;
                int totalBytesRead = 0;

                #region Задаём длину блока

                try
                {
                    blockLength = Convert.ToInt32(line, 16);
                }
                catch 
                {
                    throw;
                }

                #endregion

                // Если достигнут конец тела сообщения.
                if (blockLength == 0)
                    yield break;

                while (totalBytesRead != blockLength)
                {
                    int length = blockLength - totalBytesRead;

                    if (length > bufferSize)
                    {
                        length = bufferSize;
                    }

                    int bytesRead;

                    if (_receiverHelper.HasData)
                    {
                        bytesRead = _receiverHelper.Read(buffer, 0, length);
                    }
                    else
                    {
                        bytesRead = stream.Read(buffer, 0, length);
                    }

                    if (bytesRead == 0)
                    {
                        WaitData();
                    }
                    else
                    {
                        totalBytesRead += bytesRead;

                        bytesWraper.Length = bytesRead;
                        yield return bytesWraper;
                    }
                }
            }
        }

        private IEnumerable<BytesWraper> ReceiveMessageBodyZip(int contentLength)
        {
            var bytesWraper = new BytesWraper();
            var streamWrapper = new ZipWraperStream(
                _client.GetStream(), _receiverHelper);

            using (Stream stream = GetZipStream(streamWrapper))
            {
                int bufferSize = _client.ReceiveBufferSize;
                byte[] buffer = new byte[bufferSize];

                bytesWraper.Value = buffer;

                while (true)
                {
                    int bytesRead = stream.Read(buffer, 0, bufferSize);

                    if (bytesRead == 0)
                    {
                        if (streamWrapper.TotalBytesRead == contentLength)
                        {
                            yield break;
                        }
                        else
                        {
                            WaitData();

                            continue;
                        }
                    }

                    bytesWraper.Length = bytesRead;
                    yield return bytesWraper;
                }
            }
        }

        private IEnumerable<BytesWraper> ReceiveMessageBodyChunkedZip()
        {
            var bytesWraper = new BytesWraper();
            var streamWrapper = new ZipWraperStream
                (_client.GetStream(), _receiverHelper);

            using (Stream stream = GetZipStream(streamWrapper))
            {
                int bufferSize = _client.ReceiveBufferSize;
                byte[] buffer = new byte[bufferSize];

                bytesWraper.Value = buffer;

                while (true)
                {
                    string line = _receiverHelper.ReadLine();

                    // Если достигнут конец блока.
                    if (line == Http.NewLine)
                        continue;

                    line = line.Trim(' ', '\r', '\n');

                    // Если достигнут конец тела сообщения.
                    if (line == string.Empty)
                        yield break;

                    int blockLength;

                    #region Задаём длину блока

                    try
                    {
                        blockLength = Convert.ToInt32(line, 16);
                    }
                    catch 
                    {                    
                        throw;
                    }

                    #endregion

                    // Если достигнут конец тела сообщения.
                    if (blockLength == 0)
                        yield break;

                    streamWrapper.TotalBytesRead = 0;
                    streamWrapper.LimitBytesRead = blockLength;

                    while (true)
                    {
                        int bytesRead = stream.Read(buffer, 0, bufferSize);

                        if (bytesRead == 0)
                        {
                            if (streamWrapper.TotalBytesRead == blockLength)
                            {
                                break;
                            }
                            else
                            {
                                WaitData();

                                continue;
                            }
                        }

                        bytesWraper.Length = bytesRead;
                        yield return bytesWraper;
                    }
                }
            }
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

                // Ищем позицию, где заканчивается описание типа контента и начинается описание его параметров.
                int endTypePos = contentType.IndexOf(';');
                if (endTypePos != -1)
                    contentType = contentType.Substring(0, endTypePos);

                return contentType;
            }

            return string.Empty;
        }

        #endregion

        private void WaitData()
        {
            int sleepTime = 0;
            int delay = (_client.ReceiveTimeout < 10) ?
                10 : _client.ReceiveTimeout;

            while (!_client.GetStream().DataAvailable)
            {
                if (sleepTime >= delay)
                {
                    break;
                }

                sleepTime += 10;
                Thread.Sleep(10);
            }
        }

        private Stream GetZipStream(Stream stream)
        {
            string contentEncoding = _headers["Content-Encoding"].ToLower();

            switch (contentEncoding)
            {
                case "gzip":
                    return new GZipStream(stream, CompressionMode.Decompress, true);

                case "deflate":
                    return new DeflateStream(stream, CompressionMode.Decompress, true);

                default:
                    throw new InvalidOperationException(string.Format(
                        Resources.InvalidOperationException_NotSupportedEncodingFormat, contentEncoding));
            }
        }
        #endregion



    }
}
