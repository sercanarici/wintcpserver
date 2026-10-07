using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using SK50_Checker;
using System.Xml;
using System.Xml.Schema;
using System.Data;
using System.Diagnostics;
using System.Linq;

namespace winTCPServer
{
    public delegate void NetEvent(object sender, NetEventArgs e);

    public class NetEventArgs : EventArgs
    {
        private Socket _client;
        private object _data;

        public Socket Client
        {
            get
            {
                return _client;
            }
        }

        public object Data
        {
            get
            {
                return _data;
            }
        }

        public NetEventArgs(Socket client)
        {
            if (null == client)
            {
                throw (new System.ArgumentNullException());
            }
            _client = client;
        }

        public NetEventArgs(Socket client, object data)
        {
            if (null == client && null == data)
            {
                throw (new System.ArgumentNullException());
            }
            _client = client;
            _data = data;
        }
    }

    class SocketSvr
    {
        public const int DefaultMaxClient = 100;
        public const int DefaultBufferSize = 1024 * 64;
        private Encoding _coder;
        private int _port;
        private volatile bool _isRun;
        private Socket _socket;
        // Her client kendi alım buffer'ına sahip; tek ortak buffer eşzamanlı okumalarda veriyi karıştırıyordu.
        // Anahtar Socket nesnesinin kendisi: Handle değeri kapanan soketten sonra yeni bir sokete verilebiliyor.
        private readonly Dictionary<Socket, ClientSession> _sessionTable = new Dictionary<Socket, ClientSession>();
        private ushort _MaxClient;

        private const string MessageStart = "<Property>";
        private const string MessageEnd = "</Property>";
        // Tamamlanmamış mesaj için üst sınır; aşılırsa bekleyen veri atılır.
        private const int MaxPendingLength = 16 * 1024;

        private class ClientSession
        {
            public readonly Socket Socket;
            public readonly byte[] Buffer = new byte[DefaultBufferSize];
            public readonly Decoder Decoder;
            // Henüz tamamlanmamış mesaj parçaları.
            public readonly StringBuilder Pending = new StringBuilder();

            public ClientSession(Socket socket, Encoding encoding)
            {
                Socket = socket;
                Decoder = encoding.GetDecoder();
            }
        }

        // TCP akışında mesaj sınırı yoktur: bir mesaj birden fazla parça halinde gelebilir, SOEPS başlığıyla
        // ya da başka bir mesajla aynı pakete düşebilir. Biriken veriden tamamlanan her mesaj ayrı çıkarılır.
        private static List<string> ExtractMessages(StringBuilder pending)
        {
            List<string> messages = new List<string>();
            while (true)
            {
                string text = pending.ToString();
                int start = text.IndexOf(MessageStart, StringComparison.Ordinal);
                if (start < 0)
                {
                    // Mesaj başlangıcı yok (ör. SOEPS başlığı); sadece yarım gelmiş olabilecek "<Property" kısmı saklanır.
                    int keep = Math.Min(text.Length, MessageStart.Length - 1);
                    pending.Remove(0, text.Length - keep);
                    break;
                }

                int end = text.IndexOf(MessageEnd, start, StringComparison.Ordinal);
                if (end < 0)
                {
                    // Mesajın devamı bekleniyor.
                    pending.Remove(0, start);
                    break;
                }

                end += MessageEnd.Length;
                messages.Add(text.Substring(start, end - start));
                pending.Remove(0, end);
            }
            return messages;
        }

        public bool IsRunning
        {
            get
            {
                return _isRun;
            }
        }

        public int SessionCount
        {
            get
            {
                lock (_sessionTable)
                {
                    return _sessionTable.Count;
                }
            }
        }

        public event NetEvent ClientConn;
        public event NetEvent ClientClose;
        public event NetEvent ServerFull;
        public event NetEvent ReceivedData;

        public SocketSvr()
        {
            _coder = Encoding.UTF8;
            _MaxClient = DefaultMaxClient;
        }

        public virtual void Start(int port)
        {
            if (_isRun)
                throw new InvalidOperationException("Server is running!");
            _port = port;
            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                IPEndPoint iep = new IPEndPoint(IPAddress.Any, port);
                listener.Bind(iep);
                listener.Listen(5);

                // AcceptConn _isRun'a baktığı için BeginAccept'ten önce set edilmeli.
                _socket = listener;
                _isRun = true;
                listener.BeginAccept(new AsyncCallback(AcceptConn), listener);
            }
            catch
            {
                _isRun = false;
                listener.Close();
                throw;
            }
        }

        // Entity/karakter referansı başlatmayan çıplak '&' karakterleri.
        private static readonly Regex _bareAmpersand = new Regex(@"&(?!(#[0-9]+|#x[0-9a-fA-F]+|amp|lt|gt|quot|apos);)", RegexOptions.Compiled);

        // Ürün adındaki '&' ve '<' gibi karakterler XML'i bozuyordu ("H&M" gibi).
        // View'ların bilinçli eklediği '&#13;' (cihazda satır atlatır) gibi referanslar olduğu gibi korunur.
        private static string XmlEscape(string value, bool attribute = false)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }
            string escaped = _bareAmpersand.Replace(value, "&amp;").Replace("<", "&lt;");
            return attribute ? escaped.Replace("\"", "&quot;") : escaped;
        }

        public virtual void SendProduct(Socket Client, Product _Product)
        {
            try
            {
                string root = string.Format("<root><barcode>{0}</barcode><title>{1}</title><desp>{2}</desp><price1>{3}</price1><price2>{4}</price2></root>", XmlEscape(_Product.Barcode), XmlEscape(_Product.Title), XmlEscape(_Product.Description), XmlEscape(_Product.Price), XmlEscape(_Product.Price2));

                // Not: Length karakter sayısı olarak gönderiliyor. UTF-8'de bayt sayısı farklı olsa da (€, Türkçe karakterler)
                // cihazlar bu haliyle çalışıyor; cihazda test edilmeden bayt sayısına çevrilmemeli.
                int len = root.Length;


                string txtData = string.Format(@"<Property><ValuePairs><Item Key=""Message-Profile-Id"" Value=""SK4050"" /><Item Key=""Code-Content"" Value=""{0}"" /></ValuePairs><PayLoads><Item Type=""XML"" Length=""{1}"" /></PayLoads></Property>{2}", XmlEscape(_Product.Barcode, true), len, root);

                byte[] _soeps = SoepsOlustur(_Product);

                byte[] _data = _coder.GetBytes(txtData);

                Client.BeginSend(_soeps, 0, _soeps.Length, SocketFlags.None, new AsyncCallback(SendDataEnd), Client);

                Client.BeginSend(_data, 0, _data.Length, SocketFlags.None, new AsyncCallback(SendDataEnd), Client);
            }
            catch (ObjectDisposedException)
            {
                // Client bu arada kapatılmış.
            }
            catch (Exception ex)
            {
                Logger.Write("SendProduct: " + ex);
                CloseClient(Client, "SendError");
            }
        }

        public virtual void Stop()
        {
            if (!_isRun)
            {
                return;
            }

            _isRun = false;
            try
            {
                _socket.Close();
            }
            catch (Exception ex)
            {
                Logger.Write("Stop: " + ex);
            }

            List<Socket> clients;
            lock (_sessionTable)
            {
                clients = new List<Socket>(_sessionTable.Keys);
            }
            foreach (Socket client in clients)
            {
                CloseClient(client, "StopServer");
            }
        }

        // Soket callback'leri IOCP thread'inde çalışır; buradan kaçan her exception process'i sonlandırır.
        // Bu yüzden callback'ler hiçbir exception'ı dışarı sızdırmamalı.
        protected virtual void AcceptConn(IAsyncResult iar)
        {
            if (!_isRun)
            {
                return;
            }
            Socket listener = (Socket)iar.AsyncState;
            Socket client = null;
            try
            {
                client = listener.EndAccept(iar);
            }
            catch (ObjectDisposedException)
            {
                // Server durduruldu.
                return;
            }
            catch (Exception ex)
            {
                Logger.Write("AcceptConn: " + ex);
            }

            if (client != null)
            {
                try
                {
                    AddClient(client);
                }
                catch (Exception ex)
                {
                    Logger.Write("AcceptConn: " + ex);
                    CloseClient(client, "AcceptError");
                }
            }

            // Ne olursa olsun yeni bağlantıları dinlemeye devam et.
            if (_isRun)
            {
                try
                {
                    listener.BeginAccept(new AsyncCallback(AcceptConn), listener);
                }
                catch (ObjectDisposedException)
                {
                }
                catch (Exception ex)
                {
                    Logger.Write("BeginAccept: " + ex);
                }
            }
        }

        private void AddClient(Socket client)
        {
            if (SessionCount >= _MaxClient)
            {
                Logger.Write("Maksimum client sayısına ulaşıldı, bağlantı reddedildi.");
                RaiseEvent(ServerFull, new NetEventArgs(client));
                client.Close();
                return;
            }

            ClientSession session = new ClientSession(client, _coder);
            lock (_sessionTable)
            {
                _sessionTable[client] = session;
            }
            // ClientClose'dan önce gelmesi için ClientConn, okuma başlamadan tetiklenir.
            RaiseEvent(ClientConn, new NetEventArgs(client));
            client.BeginReceive(session.Buffer, 0, session.Buffer.Length, SocketFlags.None,
                new AsyncCallback(RecvData), session);
        }

        protected virtual void RecvData(IAsyncResult iar)
        {
            ClientSession session = (ClientSession)iar.AsyncState;
            Socket client = session.Socket;
            try
            {
                int recv = client.EndReceive(iar);
                if (recv == 0)
                {
                    CloseClient(client, "NormalExit");
                    return;
                }
                // Decoder parçalar arasında bölünen çok baytlı UTF-8 karakterleri doğru birleştirir.
                char[] chars = new char[_coder.GetMaxCharCount(recv)];
                int charCount = session.Decoder.GetChars(session.Buffer, 0, recv, chars, 0);
                string receivedData = new string(chars, 0, charCount);
                Debug.WriteLine(receivedData);

                session.Pending.Append(receivedData);
                foreach (string message in ExtractMessages(session.Pending))
                {
                    XmlValidate(message);

                    string _barcode = GetBarcodeFromReceivedData(message);

                    Product _Product = Products.GetProductInfo(_barcode);
                    if (_Product != null)
                    {
                        SendProduct(client, _Product);
                    }
                }

                if (session.Pending.Length > MaxPendingLength)
                {
                    Logger.Write("RecvData: tamamlanmayan mesaj üst sınırı aştı, bekleyen veri atıldı.");
                    session.Pending.Clear();
                }

                RaiseEvent(ReceivedData, new NetEventArgs(client, receivedData));

                client.BeginReceive(session.Buffer, 0, session.Buffer.Length, SocketFlags.None,
                new AsyncCallback(RecvData), session);
            }
            catch (ObjectDisposedException)
            {
                // Soket başka bir yerde (Stop / gönderim hatası) kapatılmış.
                CloseClient(client, "Disposed");
            }
            catch (SocketException ex)
            {
                Logger.Write(ex.ToString());
                // Önceden sadece 10054'te kapatılıyordu; 10053/10060'ta soket açık ve tabloda kalıp sızıyordu.
                CloseClient(client, "ExceptionExit");
            }
            catch (Exception ex)
            {
                // Bozuk/parçalı XML vb. Önceden bu durum process'i çökertiyordu.
                Logger.Write("RecvData: " + ex);
                CloseClient(client, "ProcessError");
            }
        }

        protected virtual void CloseClient(Socket client, string exitMsg)
        {
            if (client == null)
            {
                return;
            }

            bool removed;
            lock (_sessionTable)
            {
                removed = _sessionTable.Remove(client);
            }

            // Aynı client birden fazla callback'ten kapatılabilir; event'i sadece bir kez tetikle.
            if (removed)
            {
                RaiseEvent(ClientClose, new NetEventArgs(client));
            }

            try
            {
                client.Shutdown(SocketShutdown.Both);
            }
            catch
            {
                // Bağlantı zaten kopmuş olabilir.
            }

            try
            {
                client.Close();
            }
            catch
            {
            }
        }

        protected virtual void SendDataEnd(IAsyncResult iar)
        {
            Socket remote = (Socket)iar.AsyncState;
            try
            {
                remote.EndSend(iar);
            }
            catch (ObjectDisposedException)
            {
                // Client bu arada kapatılmış.
            }
            catch (Exception ex)
            {
                // Cihaz yanıtı beklemeden bağlantıyı kopardıysa buraya düşer (logdaki 10053 sonrası çökmeler).
                Logger.Write("SendDataEnd: " + ex);
                CloseClient(remote, "SendError");
            }
        }

        private void RaiseEvent(NetEvent handler, NetEventArgs e)
        {
            if (handler == null)
            {
                return;
            }
            try
            {
                handler(this, e);
            }
            catch (Exception ex)
            {
                Logger.Write("Event handler: " + ex);
            }
        }

        private void XmlValidate(string xmlstr)
        {
            string schemapath = Directory.GetCurrentDirectory() + "\\Scheme.xsd";
            using (XmlTextReader schemaReader = new XmlTextReader(schemapath))
            {
                XmlSchema sema = XmlSchema.Read(schemaReader, ValidationCallBack);

                XmlReaderSettings settings = new XmlReaderSettings();
                settings.Schemas.Add(sema);
                settings.ValidationType = ValidationType.Schema;

                XmlReader reader = XmlReader.Create(new StringReader(xmlstr), settings);
            }
        }

        private void ValidationCallBack(object sender, ValidationEventArgs e)
        {
            switch (e.Severity)
            {
                case XmlSeverityType.Error:
                    throw new Exception(string.Format("Error: {0}", e.Message));
                case XmlSeverityType.Warning:
                    throw new Exception(string.Format("Warning: {0}", e.Message));
            }
        }

        private string GetBarcodeFromReceivedData(string receivedData)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(receivedData);

            XmlNode root = doc.DocumentElement;
            XmlNode node = root.ChildNodes[0].ChildNodes[1];

            return node.Attributes["Value"].Value;

        }

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        private byte[] SoepsOlustur(Product _Product)
        {

            byte[] _soeps = new byte[12];


            if (_Product.Barcode.Length == 22)
            {
                _soeps = StringToByteArray("534F4550536B7E6ACD000000");
            }

            else if (_Product.Barcode.Length == 21)
            {
                _soeps = StringToByteArray("534F4550536B7E6ACC000000");
            }
            else if (_Product.Barcode.Length == 20)
            {
                _soeps = StringToByteArray("534F4550536B7E6ACB000000");
            }

            else if (_Product.Barcode.Length == 19)
            {
                _soeps = StringToByteArray("534F4550536B7E6ACA000000");
            }
            else if (_Product.Barcode.Length == 18)
            {
                _soeps = StringToByteArray("534F4550536B7E6AC9000000");
            }
            else if (_Product.Barcode.Length == 17)
            {
                _soeps = StringToByteArray("534F4550536B7E6AC8000000");
            }
            else if (_Product.Barcode.Length == 16)
            {
                _soeps = StringToByteArray("534F4550536B7E6AC7000000");
            }
            else if (_Product.Barcode.Length == 15)
            {
                _soeps = StringToByteArray("534F4550536B7E6AC6000000");

            }
            else if (_Product.Barcode.Length == 14)
            {
                _soeps = StringToByteArray("534F4550536B7E6AC5000000");

            }
            else if (_Product.Barcode.Length == 13)
            {
                _soeps = StringToByteArray("534F4550536B7E6AC4000000");

            }
            else if (_Product.Barcode.Length == 12)
            {
                _soeps = StringToByteArray("534F4550536B7E6AC3000000");

            }
            else if (_Product.Barcode.Length == 11)
            {
                _soeps = StringToByteArray("534F4550536B7E6AC2000000");
            }
            else if (_Product.Barcode.Length == 10)
            {
                _soeps = StringToByteArray("534F4550536B7E6AC1000000");
            }
            else if (_Product.Barcode.Length == 9)
            {
                _soeps = StringToByteArray("534F4550536B7E6AC0000000");
            }
            else if (_Product.Barcode.Length == 8)
            {
                _soeps = StringToByteArray("534F4550536B7E6ABF000000");

            }
            else if (_Product.Barcode.Length == 7)
            {
                _soeps = StringToByteArray("534F4550536B7E6ABE000000");
            }
            else if (_Product.Barcode.Length == 6)
            {
                _soeps = StringToByteArray("534F4550536B7E6ABD000000");
            }
            else if (_Product.Barcode.Length == 5)
            {
                _soeps = StringToByteArray("534F4550536B7E6ABC000000");
            }
            else if (_Product.Barcode.Length == 4)
            {
                _soeps = StringToByteArray("534F4550536B7E6ABB000000");
            }
            else if (_Product.Barcode.Length == 3)
            {
                _soeps = StringToByteArray("534F4550536B7E6ABA000000");
            }
            else if (_Product.Barcode.Length == 2)
            {
                _soeps = StringToByteArray("534F4550536B7E6AB9000000");
            }
            else if (_Product.Barcode.Length == 1)
            {
                _soeps = StringToByteArray("534F4550536B7E6AB8000000");
            }
            return _soeps;
        }

    }

}
