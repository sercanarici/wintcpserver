using System;
using System.Drawing;
using System.Net.Sockets;
using System.Windows.Forms;
using SK50_Checker;
using System.Net;
using Microsoft.Win32;
using System.IO; 

namespace winTCPServer
{
    public partial class frmMain : Form
    {
        SocketSvr _Server;
        private delegate void UpdateTxtdele(string appendText, Color color, string message);
        
        public frmMain()
        {
            InitializeComponent();

            //IPHostEntry IpEntry = Dns.GetHostEntry(Dns.GetHostName());
            //string myip = IpEntry.AddressList[2].ToString();           

            //txtIP.Text = myip;
            txtPort.Text = "9000";
            txtPort.Enabled = true;
            //panel1.Enabled = false;            

        }

        private void UpdateGetMsgTextBox(string member,  string message, Color color)
        {
            string appendText = member + ":           " + System.DateTime.Now.ToString()
                    + Environment.NewLine                    + message + Environment.NewLine;
            this.Invoke(new UpdateTxtdele(UpdataTextControl), new object[] { appendText, color, message });

            using (StreamWriter w = File.AppendText("log.txt"))
            {
                Log(appendText, w);
            }

        }

        private void UpdataTextControl(string appendText, Color color,string message)
        {
            txtGetMsg.Text += appendText;
            txtGetMsg.ScrollToCaret();
        }
       
        private void SocketRevice(object sender,NetEventArgs e)
        {
            try
            {
                UpdateGetMsgTextBox("From " + ((Socket)e.Client).RemoteEndPoint.ToString(), (string)e.Data, Color.Red);

            }
            catch (Exception ex)
            {
                using (StreamWriter w = File.AppendText("log.txt"))
                {
                    Log(ex.ToString(), w);
                }
            }
        }

        private delegate void UpdateListbox(Socket _socket,bool add);
        private void ClientConn(object sender, NetEventArgs e)
        {
            UpdateListbox _UpdateListbox = new UpdateListbox(UpdateListboxItem);
            this.Invoke(_UpdateListbox, new object[] { e.Client,true });

            using (StreamWriter w = File.AppendText("log.txt"))
            {
                Log(string.Format("ClientConn:{0}--{1}",e.Client.RemoteEndPoint.ToString(), e.Client.Handle.ToString()), w);
            }

        }
        private void ClientClose(object sender, NetEventArgs e)
        {
            UpdateListbox _UpdateListbox = new UpdateListbox(UpdateListboxItem);
            this.Invoke(_UpdateListbox, new object[] { e.Client, false });

            using (StreamWriter w = File.AppendText("log.txt"))
            {
                Log(string.Format("ClientClose:{0}--{1}", e.Client.RemoteEndPoint.ToString(), e.Client.Handle.ToString()), w);
            }
        }

        private void UpdateListboxItem(Socket _socket,bool add)
        {
            if (listBox1.Items.Count == 0)
            {
                panel1.Enabled = true;
                listBox1.DisplayMember = "RemoteEndPoint";
                listBox1.ValueMember = "Handle";
            }
            if (add)
            {
                listBox1.Items.Add(_socket);
                listBox1.SelectedIndex = listBox1.Items.Count - 1;
            }
            else
            {
                listBox1.Items.Remove(_socket);
                listBox1.SelectedIndex = listBox1.Items.Count - 1;
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            
            try
            {
                string ip = txtIP.Text;
                int port = Convert.ToInt32(txtPort.Text);

                if (_Server == null)
                {
                    _Server = new SocketSvr();
                    _Server.ReceivedData += new NetEvent(SocketRevice);
                    _Server.ClientConn += new NetEvent(ClientConn);
                    _Server.ClientClose += new NetEvent(ClientClose);
                    this.Text += " - [ Address = " + ip + ":" + port + " ]";
                    panel2.Visible = true;
                }
                _Server.Start(port);
                UpdateGetMsgTextBox("System", "TCP Server is running.", Color.Red);
            }
            catch (Exception ex)
            {
                using (StreamWriter w = File.AppendText("log.txt"))
                {
                    Log(ex.ToString(), w);
                }
            }
           
        }       

        private void MainForm1_FormClosing()
        {
            if (_Server != null)
                _Server.Stop();
        }

        private void frmMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\PC88-FiyatGor");
                key.SetValue("ip", txtIP.Text);
                key.SetValue("port", txtPort.Text);
            }
            catch (Exception ex)
            {
                using (StreamWriter w = File.AppendText("log.txt"))
                {
                    Log(ex.ToString(), w);
                }
            }
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\PC88-FiyatGor");
                txtIP.Text = key.GetValue("ip").ToString();
                txtPort.Text = key.GetValue("port").ToString();
                btnConnect_Click(null, null);
            }
            catch (Exception ex)
            {
                using (StreamWriter w = File.AppendText("log.txt"))
                {
                    Log(ex.ToString(), w);
                }
            }
        }
        public static void Log(string logMessage, TextWriter w)
        {
            w.Write("\r\nLog Entry : ");
            w.WriteLine($"{DateTime.Now.ToLongTimeString()} {DateTime.Now.ToLongDateString()}");
            w.WriteLine($"  :{logMessage}");
            w.WriteLine("-------------------------------");
        }
    }
}
