using Microsoft.Win32;
using System;
using System.Drawing;
using System.Net.Sockets;
using System.Windows.Forms;

namespace winTCPServer
{
    public partial class frmMain : Form
    {
        // Ekrandaki log kutusunun üst sınırı; aşılınca eski kayıtlar silinir.
        // Sınırsız büyüyen metin her eklemede UI'yi yavaşlatıp cihazların zaman aşımına düşmesine yol açıyordu.
        private const int MaxLogTextLength = 200000;

        SocketSvr _Server;

        private class ClientItem
        {
            public readonly Socket Socket;
            private readonly string _text;

            public ClientItem(Socket socket, string text)
            {
                Socket = socket;
                _text = text;
            }

            public override string ToString()
            {
                return _text;
            }
        }

        public frmMain()
        {
            InitializeComponent();

            //IPHostEntry IpEntry = Dns.GetHostEntry(Dns.GetHostName());
            //string myip = IpEntry.AddressList[2].ToString();

            //txtIP.Text = myip;
            txtPort.Text = "9000";
            txtPort.Enabled = true;
            txtGetMsg.MaxLength = 0;
            //panel1.Enabled = false;

        }

        private void UpdateGetMsgTextBox(string member,  string message, Color color)
        {
            string appendText = member + ":           " + System.DateTime.Now.ToString()
                    + Environment.NewLine                    + message + Environment.NewLine;
            Logger.Write(appendText);
            PostToUI(() => AppendLogText(appendText));
        }

        // Soket thread'leri UI'yi beklememeli (Invoke yerine BeginInvoke).
        private void PostToUI(Action action)
        {
            try
            {
                if (IsHandleCreated && !IsDisposed)
                {
                    BeginInvoke(action);
                }
            }
            catch (InvalidOperationException)
            {
                // Form kapanıyor.
            }
        }

        private void AppendLogText(string appendText)
        {
            if (txtGetMsg.TextLength > MaxLogTextLength)
            {
                string text = txtGetMsg.Text;
                int start = text.IndexOf('\n', text.Length - MaxLogTextLength / 2);
                txtGetMsg.Text = start >= 0 ? text.Substring(start + 1) : string.Empty;
            }
            txtGetMsg.AppendText(appendText);
        }

        private static string GetEndPoint(Socket socket)
        {
            try
            {
                return socket.RemoteEndPoint.ToString();
            }
            catch
            {
                return "?";
            }
        }

        private void SocketRevice(object sender,NetEventArgs e)
        {
            try
            {
                UpdateGetMsgTextBox("From " + GetEndPoint(e.Client), (string)e.Data, Color.Red);
            }
            catch (Exception ex)
            {
                Logger.Write(ex.ToString());
            }
        }

        private void ClientConn(object sender, NetEventArgs e)
        {
            Socket socket = e.Client;
            string endPoint = GetEndPoint(socket);
            Logger.Write(string.Format("ClientConn:{0}--{1}", endPoint, socket.Handle.ToString()));
            PostToUI(() => AddClientItem(socket, endPoint));
        }

        private void ClientClose(object sender, NetEventArgs e)
        {
            Socket socket = e.Client;
            Logger.Write(string.Format("ClientClose:{0}--{1}", GetEndPoint(socket), socket.Handle.ToString()));
            PostToUI(() => RemoveClientItem(socket));
        }

        // Listede Socket yerine sabit metin tutulur; kapanmış soketin RemoteEndPoint'ini okumak exception fırlatır.
        private void AddClientItem(Socket socket, string text)
        {
            panel1.Enabled = true;
            listBox1.Items.Add(new ClientItem(socket, text));
            listBox1.SelectedIndex = listBox1.Items.Count - 1;
        }

        private void RemoveClientItem(Socket socket)
        {
            for (int i = listBox1.Items.Count - 1; i >= 0; i--)
            {
                if (((ClientItem)listBox1.Items[i]).Socket == socket)
                {
                    listBox1.Items.RemoveAt(i);
                    break;
                }
            }
            listBox1.SelectedIndex = listBox1.Items.Count - 1;
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            int port = 0;
            try
            {
                string ip = txtIP.Text;
                port = Convert.ToInt32(txtPort.Text);

                if (_Server == null)
                {
                    _Server = new SocketSvr();
                    _Server.ReceivedData += new NetEvent(SocketRevice);
                    _Server.ClientConn += new NetEvent(ClientConn);
                    _Server.ClientClose += new NetEvent(ClientClose);
                    this.Text += " - [ Address = " + ip + ":" + port + " ]";
                    panel2.Visible = true;
                }

                if (_Server.IsRunning)
                {
                    UpdateGetMsgTextBox("System", "TCP Server zaten çalışıyor.", Color.Red);
                    return;
                }

                _Server.Start(port);
                UpdateGetMsgTextBox("System", "TCP Server is running. Barkod eşleşme: "
                    + (Globals.BarcodeMatch == BarcodeMatchMode.Exact ? "Equals (birebir)" : "Contains (içerir)"), Color.Red);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                Logger.Write(ex.ToString());
                ShowStartError(port + " numaralı port başka bir uygulama tarafından kullanılıyor."
                    + Environment.NewLine + Environment.NewLine
                    + "Programın başka bir kopyası açık olabilir veya önceki kopyası düzgün kapanmamış olabilir. "
                    + "Görev Yöneticisi'nde FiyatGorTCPServer.exe'yi kontrol edip kapatın, ardından 'Start' butonuna tekrar basın.");
            }
            catch (Exception ex)
            {
                Logger.Write(ex.ToString());
                ShowStartError("TCP Server başlatılamadı: " + ex.Message);
            }
        }

        private void ShowStartError(string message)
        {
            UpdateGetMsgTextBox("System", "HATA: TCP Server çalışmıyor! " + message, Color.Red);
            MessageBox.Show(this,
                message + Environment.NewLine + Environment.NewLine + "Sunucu şu an ÇALIŞMIYOR, fiyatgör cihazları fiyat alamaz.",
                "Fiyatgör TCP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                if (_Server != null)
                    _Server.Stop();
            }
            catch (Exception ex)
            {
                Logger.Write(ex.ToString());
            }
        }

        private void frmMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\PC88-FiyatGor"))
                {
                    key.SetValue("ip", txtIP.Text);
                    key.SetValue("port", txtPort.Text);
                }
            }
            catch (Exception ex)
            {
                Logger.Write(ex.ToString());
            }
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\PC88-FiyatGor"))
                {
                    if (key != null)
                    {
                        txtIP.Text = Convert.ToString(key.GetValue("ip", txtIP.Text));
                        txtPort.Text = Convert.ToString(key.GetValue("port", txtPort.Text));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write(ex.ToString());
            }

            // Kayıtlı ayar olmasa da (ilk kurulum / farklı kullanıcı) varsayılan port ile başlat.
            btnConnect_Click(null, null);
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            //Show();
            //this.WindowState = FormWindowState.Normal;
            //notifyIcon1.Visible = false;

            ShowInTaskbar = true;
            notifyIcon1.Visible = false;
            WindowState = FormWindowState.Normal;
        }

        private void frmMain_Resize(object sender, EventArgs e)
        {
            //if the form is minimized
            //hide it from the task bar
            //and show the system tray icon (represented by the NotifyIcon control)
            if (this.WindowState == FormWindowState.Minimized)
            {
                //Hide();
                //notifyIcon1.Visible = true;
                ShowInTaskbar = false;
                notifyIcon1.Visible = true;
                notifyIcon1.ShowBalloonTip(1000);
            }
        }
    }
}
