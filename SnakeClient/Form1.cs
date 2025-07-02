using Newtonsoft.Json;
using SnakeClient.Properties;
using SnakeCommon;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Media;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;
using static System.Windows.Forms.AxHost;

namespace SnakeClient
{
    public partial class Form1 : Form
    {
        // 多人游戏
        private TcpClient client;
        private NetworkStream stream;
        private string myKey = null;
        private const int gridSize = 10;
        private DIRECTION currentDir = DIRECTION.RIGHT;

        private Dictionary<string, List<Label>> snakesLabels;
        private Dictionary<string, bool> eaten;
        private Dictionary<string, Brush> snakeBrushes;
        private Dictionary<string, Point> nextPositions;
        private Point eggPosition;
        private PictureBox eggLabel;
        private string serverIp;
        private bool isGaming = false;
        

        public Form1(string ip="127.0.0.1")
        {
            this.KeyPreview = true;
            this.serverIp = ip;
            InitializeComponent();
            this.FormClosing += Form1_FormClosing;
            join();


        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                
                var msg = new { Type = "EXIT" };
                string json = JsonConvert.SerializeObject(msg);
                byte[] data = Encoding.UTF8.GetBytes(json);
                stream?.Write(data, 0, data.Length);
                stream?.Flush();

                stream?.Close();
                client?.Close();
            }
            catch (Exception ex)
            {
                //MessageBox.Show("关闭连接出错：" + ex.Message);
            }
        }

        private void join()
        {
            try
            {
                client = new TcpClient(this.serverIp, 7788);
                stream = client.GetStream();
                //myKey = client.Client.LocalEndPoint.ToString();
                new Thread(ReceiveLoop) { IsBackground = true }.Start();

                panelGame.Focus();
                snakesLabels = new Dictionary<string, List<Label>>();
                eaten = new Dictionary<string, bool>();
                snakeBrushes = new Dictionary<string, Brush>();
                nextPositions = new Dictionary<string, Point>();

                
            }
            catch (Exception ex)
            {
                MessageBox.Show("连接服务器失败: " + ex.Message);
                Close();
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.ClientSize = new Size(500, 500);
            this.panelGame.Size = new Size(500, 500);


        }




        private void ReceiveLoop()
        {

            byte[] buffer = new byte[2048];
            try
            {
                while (true)
                {
                    int len = stream.Read(buffer, 0, buffer.Length);
                    if (len <= 0) break;
                    string json = Encoding.UTF8.GetString(buffer, 0, len);
                    var state = JsonConvert.DeserializeObject<GameState>(json);
                    this.Invoke((MethodInvoker)(() => UpdateGameState(state)));
                }
            }
            catch (Exception ex)
            {
                // 可选：记录日志、提示用户
                Console.WriteLine("接收线程异常：" + ex.Message);
            }
            finally
            {
                // 资源清理
                stream.Close();
                client.Close();

                //清理屏幕
       
                //join();
            }

        }

        // 更新游戏状态，初始化或刷新画面
        private void UpdateGameState(GameState state)
        {
            if (myKey == null && state.MyId!=null)
            { 
                myKey = state.MyId;
                return;
            }

            if (!isGaming)
            {
                isGaming = true;
                pictureBox1.Visible = false;
                SoundPlayer player = new SoundPlayer(Properties.Resources.begin); // 如果你用了资源文件
                player.Play(); // 异步播放
            }
            
            // 游戏结束提示
            if (state.IsGameOver)
            {
                string msg = state.WinnerId == null ? "平局！" :
                    (state.WinnerId == myKey ? "你赢了！" : "你输了。");
                if (state.WinnerId == myKey) {
                    SoundPlayer player = new SoundPlayer(Properties.Resources.win); // 如果你用了资源文件
                    player.Play(); // 异步播放
                } else if (state.WinnerId != null) {
                    SoundPlayer player = new SoundPlayer(Properties.Resources.lose); // 如果你用了资源文件
                    player.Play(); // 异步播放
                }
                    MessageBox.Show(msg);
                client.Close();
                this.Close();

            }



            if (state.Directions != null && state.Directions.ContainsKey(myKey))
            {
                // 正常数据报
                
                currentDir = state.Directions[myKey];
            }
            
            //panelGame.Controls.Clear();

            eggPosition = state.Egg;
            nextPositions = state.NextPostitions;

            // 蛋绘制
            DrawEgg(eggPosition);

            eaten = state.SnakesEat;
            //snakeBrushes.Clear();

            // 根据服务端颜色信息创建画刷
            if (state.Colors != null)
            {
                foreach (var kvp in state.Colors)
                {
                    try
                    {
                        Color c = ColorTranslator.FromHtml(kvp.Value);
                        snakeBrushes[kvp.Key] = new SolidBrush(c);
                    }
                    catch
                    {
                        snakeBrushes[kvp.Key] = Brushes.Green;
                    }
                }
            }

            // 绘制所有蛇
            DrawSnake(state);
            

            
        }

        private void DrawSnake(GameState state)
        {
            foreach (var kvp in state.NextPostitions)
            {
                Brush brush = Brushes.Green;
                if (snakeBrushes.ContainsKey(kvp.Key))
                    brush = snakeBrushes[kvp.Key];

                if (eaten[kvp.Key])
                {
                    
                    Label label = new Label();
                    label.BackColor = Color.FromArgb(brush is SolidBrush sb ? sb.Color.ToArgb() : Color.Black.ToArgb());
                    label.Size = new Size(gridSize, gridSize);
                    label.Location = new Point(nextPositions[kvp.Key].X * gridSize, nextPositions[kvp.Key].Y * gridSize);
                    panelGame.Controls.Add(label);
                    if (!snakesLabels.ContainsKey(kvp.Key))
                    {
                        snakesLabels[kvp.Key] = new List<Label>();
                    }
                    else {
                        if (kvp.Key == myKey) {
                            SoundPlayer player = new SoundPlayer(Properties.Resources.eat); // 如果你用了资源文件
                            player.Play(); // 异步播放
                        }
                    }
                        snakesLabels[kvp.Key].Add(label); //尾部增加
                }
                else
                {
                    Label label = snakesLabels[kvp.Key][0];
                    snakesLabels[kvp.Key].RemoveAt(0);
                    label.Location = new Point(nextPositions[kvp.Key].X * gridSize, nextPositions[kvp.Key].Y * gridSize);
                    snakesLabels[kvp.Key].Add(label); //尾部增加

                }
            }


            
        }

        private void DrawEgg(Point p)
        {
            if (eggLabel == null)
            {
                //首次绘
                eggLabel = new PictureBox();
                eggLabel.Size = new Size(gridSize, gridSize);
                eggLabel.SizeMode = PictureBoxSizeMode.StretchImage;

                // 加载苹果图标（你可以用自己的图片路径或资源）
                eggLabel.Image = Properties.Resources.ap;

                eggLabel.Location = new Point(p.X * gridSize, p.Y * gridSize);
                panelGame.Controls.Add(eggLabel);
            }
            else {
                // 只改位置
                eggLabel.Location = new Point(p.X * gridSize, p.Y * gridSize);
            }
            
            
            
        }


        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            DIRECTION newDir = currentDir;
            if (e.KeyCode == Keys.Down && (currentDir != DIRECTION.UP || snakesLabels[myKey].Count == 1))
                newDir = DIRECTION.DOWN;
            else if (e.KeyCode == Keys.Up && (currentDir != DIRECTION.DOWN || snakesLabels[myKey].Count == 1))
                newDir = DIRECTION.UP;
            else if (e.KeyCode == Keys.Left && (currentDir != DIRECTION.RIGHT || snakesLabels[myKey].Count == 1))
                newDir = DIRECTION.LEFT;
            else if (e.KeyCode == Keys.Right && (currentDir != DIRECTION.LEFT || snakesLabels[myKey].Count == 1))
                newDir = DIRECTION.RIGHT;

            if (newDir != currentDir)
            {
   
                var msg = new { Type = "DIRECTION", Direction = newDir.ToString() };
                string json = JsonConvert.SerializeObject(msg);
                byte[] data = Encoding.UTF8.GetBytes(json);
                stream.Write(data, 0, data.Length);
       
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {

        }




        private void timer1_Tick(object sender, EventArgs e)
        {

        }
      




    }
}
