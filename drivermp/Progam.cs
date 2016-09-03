using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Drawing;
using System.Windows.Forms;
using System.Reflection;

namespace drivermp
{
    class Form1 : Form
    {
        public const string TITLE = "Driver Multiplayer Client 1.21 Alpha";
        public const string GAME_EXE = "Game.exe";
        const string FILE_IP = "drivermp.txt";
        const int PORT = 7777;
        //Client recieve
        const int BUFFER_SIZE = BUFFER_LEN + BUFFER_LEN2 + 1;
        const int BUFFER_LEN = 0x40;
        const int BUFFER_LEN2 = 0x04;
        //Client send
        const int THD_SLEEP = 40; //25 FPS
        //Player, network car
        uint PLAYER_ADDR;
        uint NET_ADDR;
        //Traffic
        uint TRAFFIC_ADDR;
        //Mission - Take a ride
        uint MISSION_ADDR;
        static byte[] ASM_MOV = { 0xB9, 0x00, 0x00, 0x00, 0x00, 0x90 }; //MOV ECX, 0
        static byte[] ASCII_DIR =
        {
	        0x4D, 0x50, 0x5C, 0x4D, 0x69, 0x73, 0x73, 0x69, 0x6F, 0x6E, 0x25, 0x64,
	        0x2E, 0x64, 0x6D, 0x73, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
	        0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };
        //Offset
        const uint CAR_OFFSET = 0x148;
        const uint CAR_DMG_OFFS = 0x104;
        //Version Differences
        const int OFFS_US12 = -0x20;
        const int OFFS_EU63 = -0x7F2E0;
        //
        TextBox tb_ip;
        Button bt_connect;
        //
        StreamReader sr;
        StreamWriter sw;
        //
        TcpClient client;
        Socket sock;
        Thread thd, thd2;
        MemoryEdit.Memory mem;
        Process game;

        public Form1()
        {
            Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            Text = TITLE;
            ClientSize = new Size(320, 48);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            tb_ip = new TextBox();
            tb_ip.Bounds = new Rectangle(12, 12, 128, 24);
            tb_ip.MaxLength = 15;
            if (File.Exists(FILE_IP))
            {
                sr = new StreamReader(FILE_IP);
                tb_ip.Text = sr.ReadLine();
                sr.Close();
            }
            Controls.Add(tb_ip);
            bt_connect = new Button();
            bt_connect.Text = "Connect";
            bt_connect.Bounds = new Rectangle(ClientRectangle.Right - 140, 12, 128, 24);
            bt_connect.Click += bt_connect_Click;
            Controls.Add(bt_connect);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (game != null && !game.HasExited)
                game.Kill();
            Environment.Exit(0);
            base.OnClosed(e);
        }

        void bt_connect_Click(object sender, EventArgs e)
        {
            bt_connect.Enabled = false;
            tb_ip.Enabled = false;
            try
            {
                client = new TcpClient(tb_ip.Text, PORT);
                sw = new StreamWriter(FILE_IP, false, Encoding.Default);
                sw.Write(tb_ip.Text);
                sw.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Source + " - " + ex.Message, Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                bt_connect.Enabled = true;
                tb_ip.Enabled = true;
                return;
            }
            game = Process.Start(GAME_EXE);
            mem = new MemoryEdit.Memory(game, 0x001F0FFF);
            //V1.3 US
            PLAYER_ADDR = 0x00D86AB8;
            NET_ADDR = 0x00D86C00;
            TRAFFIC_ADDR = 0x00495EA9;
            MISSION_ADDR = 0x00570FF4;
            if (mem.ReadByte2(0x00571004) == 0x53)
            {
                //V1.2 US
                PLAYER_ADDR = (uint)(PLAYER_ADDR + OFFS_US12);
                NET_ADDR = (uint)(NET_ADDR + OFFS_US12);
                TRAFFIC_ADDR = 0x00495DB9;
                MISSION_ADDR = 0x00571004;
            }
            else if (mem.ReadByte2(0x00570FF4) != 0x53)
            {
                //V6.3w EU
                PLAYER_ADDR = (uint)(PLAYER_ADDR + OFFS_EU63);
                NET_ADDR = (uint)(NET_ADDR + OFFS_EU63);
                TRAFFIC_ADDR = 0x004951A9;
                MISSION_ADDR = 0x0056ED04;
            }
            //Disable Traffic
            mem.WriteByte(TRAFFIC_ADDR, ASM_MOV, ASM_MOV.Length);
            //Change mission script
            mem.WriteByte(MISSION_ADDR, ASCII_DIR, ASCII_DIR.Length);
            sock = client.Client;
            thd = new Thread(new ThreadStart(NetRec));
            thd.Start();
            thd2 = new Thread(new ThreadStart(NetSend));
            thd2.Start();
        }

        void NetRec()
        {
            try
            {
                byte[] buffer = new byte[BUFFER_SIZE];
                byte[] data = new byte[BUFFER_LEN];
                byte[] data2 = new byte[BUFFER_LEN2];
                uint tmp;
                while (true)
                {
                    sock.Receive(buffer);
                    Array.Copy(buffer, 1, data, 0, BUFFER_LEN);
                    Array.Copy(buffer, BUFFER_LEN + 1, data2, 0, BUFFER_LEN2);
                    tmp = NET_ADDR + CAR_OFFSET * buffer[0];
                    mem.WriteByte(tmp, data, BUFFER_LEN); //Movement
                    mem.WriteByte(tmp + CAR_DMG_OFFS, data2, BUFFER_LEN2); //Smoke
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Source + " - " + e.Message, Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (game != null && !game.HasExited)
                    game.Kill();
                Environment.Exit(0);
            }
        }

        void NetSend()
        {
            try
            {
                byte[] buffer = new byte[BUFFER_LEN + BUFFER_LEN2];
                while (true)
                {
                    Thread.Sleep(THD_SLEEP);
                    Array.Copy(mem.ReadByte(PLAYER_ADDR, BUFFER_LEN), buffer, BUFFER_LEN); //Movement
                    Array.Copy(mem.ReadByte(PLAYER_ADDR + CAR_DMG_OFFS, BUFFER_LEN2),
                        0, buffer, BUFFER_LEN, BUFFER_LEN2); //Smoke
                    sock.Send(buffer);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Source + " - " + e.Message, Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (game != null && !game.HasExited)
                    game.Kill();
                Environment.Exit(0);
            }
        }
    }

    class Progam
    {
        [STAThread]
        static void Main()
        {
            if (!File.Exists(Form1.GAME_EXE))
            {
                MessageBox.Show("Game not found! (" + Form1.GAME_EXE + ")",
                    Form1.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}