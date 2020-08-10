using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
        private bool _isRun;
        private Socket _socket;
        private Hashtable _sessionTable;
        private byte[] _recvDataBuffer;
        private ushort _MaxClient;

        public Hashtable SessionTable
        {
            get
            {
                return _sessionTable;
            }
        }

        public int SessionCount
        {
            get
            {
                return _sessionTable.Count;
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
                throw new Exception("Server is running!");
            _port = port;
            _sessionTable = new Hashtable();
            _recvDataBuffer = new byte[DefaultBufferSize];
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            IPEndPoint iep = new IPEndPoint(IPAddress.Any, port);
            _socket.Bind(iep);
            _socket.Listen(5);

            _socket.BeginAccept(new AsyncCallback(AcceptConn), _socket);
            _isRun = true;
        }

        public virtual void SendProduct(Socket Client, Product _Product)
        {
            string root = string.Format("<root><barcode>{0}</barcode><title>{1}</title><desp>{2}</desp><price1>{3}</price1><price2>{4}</price2></root>", _Product.Barcode, _Product.Title, _Product.Description, _Product.Price, _Product.Price2);

            int len = root.Length;


            string txtData = string.Format(@"<Property><ValuePairs><Item Key=""Message-Profile-Id"" Value=""SK4050"" /><Item Key=""Code-Content"" Value=""{0}"" /></ValuePairs><PayLoads><Item Type=""XML"" Length=""{1}"" /></PayLoads></Property>{2}", _Product.Barcode, len, root);

            byte[] _soeps = SoepsOlustur(_Product);

            byte[] _data = _coder.GetBytes(txtData);

            Client.BeginSend(_soeps, 0, _soeps.Length, SocketFlags.None, new AsyncCallback(SendDataEnd), Client);

            Client.BeginSend(_data, 0, _data.Length, SocketFlags.None, new AsyncCallback(SendDataEnd), Client);
        }

        public virtual void Stop()
        {
            if (!_isRun)
            {
                throw (new ApplicationException("Server is Stop"));
            }

            _isRun = false;
            if (_socket.Connected)
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
            while (SessionTable.Count > 0)
            {
                object[] keys = new object[SessionTable.Count];
                SessionTable.Keys.CopyTo(keys, 0);
                Socket _client = (Socket)SessionTable[keys[0]];
                CloseClient(_client, "StopServer");
            }
            _socket.Close();

            _sessionTable = null;
        }
        protected virtual void AcceptConn(IAsyncResult iar)
        {
            if (!_isRun)
            {
                return;
            }
            Socket oldserver = (Socket)iar.AsyncState;
            Socket client = oldserver.EndAccept(iar);
            if (SessionCount == _MaxClient)
            {
                if (ServerFull != null)
                    ServerFull(this, new NetEventArgs(client));
            }
            else
            {
                _sessionTable.Add(client.Handle, client);
                client.BeginReceive(_recvDataBuffer, 0, _recvDataBuffer.Length, SocketFlags.None,
                new AsyncCallback(RecvData), client);
                if (ClientConn != null)
                {
                    ClientConn(this, new NetEventArgs(client));
                }
            }
            _socket.BeginAccept(new AsyncCallback(AcceptConn), _socket);
        }

        protected virtual void RecvData(IAsyncResult iar)
        {
            Socket client = (Socket)iar.AsyncState;
            try
            {
                int recv = client.EndReceive(iar);
                if (recv == 0)
                {
                    CloseClient(client, "NormalExit");
                    return;
                }
                string receivedData = _coder.GetString(_recvDataBuffer, 0, recv);
                Debug.WriteLine(receivedData);


                if (receivedData.StartsWith("<Property>"))
                {
                    XmlValidate(receivedData);

                    string _barcode = GetBarcodeFromReceivedData(receivedData);

                    Product _Product = Products.GetProductInfo(_barcode);
                    if (_Product != null)
                    {
                        SendProduct(client, _Product);
                    }
                }

                if (client == null) return;
                ReceivedData(this, new NetEventArgs(client, receivedData));

                client.BeginReceive(_recvDataBuffer, 0, _recvDataBuffer.Length, SocketFlags.None,
                new AsyncCallback(RecvData), client);
            }
            catch (SocketException ex)
            {
                using (StreamWriter w = File.AppendText("log.txt"))
                {
                    frmMain.Log(ex.ToString(), w);
                }

                //System.Windows.Forms.MessageBox.Show(ex.Message.ToString());
                if (10054 == ex.ErrorCode)
                {
                    CloseClient(client, "ExceptionExit");
                }
            }
            catch (ObjectDisposedException ex)
            {
                if (ex != null) ex = null;
            }
        }

        protected virtual void CloseClient(Socket client, string exitMsg)
        {
            try
            {
                if (client != null)
                {
                    SessionTable.Remove(client.Handle);
                    if (ClientClose != null)
                        ClientClose(this, new NetEventArgs(client));

                    client.Shutdown(SocketShutdown.Both);

                    client.Close();
                }
            }
            catch
            {
                throw (new ApplicationException("Client is null"));
            }
        }

        protected virtual void SendDataEnd(IAsyncResult iar)
        {
            Socket remote = (Socket)iar.AsyncState;
            int sent = remote.EndSend(iar);
        }

        private void XmlValidate(string xmlstr)
        {
            try
            {
                string schemapath = Directory.GetCurrentDirectory() + "\\Scheme.xsd";
                XmlTextReader schemaReader = new XmlTextReader(schemapath);
                XmlSchema sema = XmlSchema.Read(schemaReader, ValidationCallBack);

                XmlReaderSettings settings = new XmlReaderSettings();
                settings.Schemas.Add(sema);
                settings.ValidationType = ValidationType.Schema;
                ValidationEventHandler eventHandler = new ValidationEventHandler(ValidationCallBack);

                XmlReader reader = XmlReader.Create(new StringReader(xmlstr), settings);

            }
            catch (Exception ex)
            {

                throw ex;
            }


        }

        private void ValidationCallBack(object sender, ValidationEventArgs e)
        {
            switch (e.Severity)
            {
                case XmlSeverityType.Error:
                    throw new Exception(string.Format("Error: {}", e.Message));
                case XmlSeverityType.Warning:
                    throw new Exception(string.Format("Warning: {}", e.Message));
            }
        }

        private string GetBarcodeFromReceivedData(string receivedData)
        {
            string sonuc = "";
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(receivedData);

                XmlNode node;
                XmlNode root = doc.DocumentElement;

                node = root.ChildNodes[0].ChildNodes[1];

                sonuc = node.Attributes["Value"].Value;
            }
            catch (Exception ex)
            {

                throw ex;
            }

            return sonuc;

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
