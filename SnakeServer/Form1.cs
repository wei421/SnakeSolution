using Newtonsoft.Json;
using SnakeCommon;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace SnakeServer
{
    public partial class Form1 : Form
    {
        private TcpListener listener;
        private Dictionary<string, TcpClient> clients;
        private Dictionary<string, Snake> snakes;
        private Dictionary<string, DIRECTION> directions;
        private Dictionary<string, bool> eaten;
        private Egg egg;
        private volatile bool isGameOver = true; // 游戏状态标记
        private volatile bool matching = false;

        public Form1()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false; // 简化跨线程访问
        }

        

        private void StartListening()
        {
            listener = new TcpListener(IPAddress.Any, 7788);
            listener.Start();
            Thread thread = new Thread(AcceptClientsLoop);
            thread.IsBackground = true;
            thread.Start();
        }

        

        private void AcceptClientsLoop()
        {
            while (true)
            {
                // 每次开始时初始化参数，清空旧状态
                isGameOver = false;
                matching = true;
                AcceptClients();
                if (!matching)
                    continue;
                else{
                    matching = false;
                }
                // 游戏结束后，等待游戏结束标志
                while (!isGameOver)
                {
                    Thread.Sleep(100);
                }

                lstLog.AppendText("游戏结束，重新等待玩家连接...\n");

                // 重新开始等待玩家连接
            }
        }

        private void AcceptClients()
        {
            // 初始化参数
            clients = new Dictionary<string, TcpClient>();
            snakes = new Dictionary<string, Snake>();
            directions = new Dictionary<string, DIRECTION>();
            eaten = new Dictionary<string, bool>();


            //while (clients.Count < 2)
            //{
            //    TcpClient client = listener.AcceptTcpClient();
            //    string key = client.Client.RemoteEndPoint.ToString();
            //    clients[key] = client;
            //    directions[key] = DIRECTION.RIGHT;
            //    snakes[key] = new Snake(new Point(5 + clients.Count * 10, 5));
            //    eaten[key] = false;

            //    lstLog.AppendText($"客户端 {key} 已连接。\n");

            //    Thread t = new Thread(() => HandleClient(client, key));
            //    t.IsBackground = true;
            //    t.Start();

            //    if (clients.Count == 2)
            //    {
            //        StartGame();  // 开启对局
            //        lstLog.AppendText($"开启对局\n");
            //    }
            //}
            
            while (clients.Count < 2 && matching)
            {
                // 非阻塞检查是否有连接请求
                if (listener.Pending())
                {
                    TcpClient client = listener.AcceptTcpClient();

                    string uuid = Guid.NewGuid().ToString();
                    string key = client.Client.RemoteEndPoint.ToString();
                    clients[uuid] = client;
                    directions[uuid] = DIRECTION.RIGHT;
                    snakes[uuid] = new Snake(new Point(5 + clients.Count * 10, 5));
                    eaten[uuid] = false;

                    lstLog.AppendText($"客户端 {key} - {uuid} 已连接。\n");

                    var initState = new GameState
                    {
                        MyId = uuid
                    };
                    SendToClient(client, initState);

                    Thread t = new Thread(() => HandleClient(client, uuid));
                    t.IsBackground = true;
                    t.Start();

                    if (clients.Count == 2)
                    {
                        StartGame();
                        lstLog.AppendText($"开启对局\n");
                    }
                }
                else
                {
                    Thread.Sleep(100); // 没有连接，休息一下
                }
            }
       


        }
        void SendToClient(TcpClient client, GameState state)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                string json = JsonConvert.SerializeObject(state);
                byte[] data = Encoding.UTF8.GetBytes(json + "\n");
                stream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                lstLog.AppendText("发送 GameState 失败：" + ex.Message + "\n");
            }
        }


        private void HandleClient(TcpClient client, string key)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];
            try
            {
                while (true)
                {
                    int len = stream.Read(buffer, 0, buffer.Length);
                    if (len <= 0) break;

                    string json = Encoding.UTF8.GetString(buffer, 0, len);
                    dynamic msg = JsonConvert.DeserializeObject(json);
                    if (msg.Type == "DIRECTION")
                    {
                        string dirStr = msg.Direction.ToString();
                        if (Enum.TryParse<DIRECTION>(dirStr, out var parsedDir))
                        {
                            directions[key] = parsedDir;
                        }
                        else
                        {
                            lstLog.AppendText($"收到非法方向: {dirStr}\n");
                        }
                    }
                    if (msg.Type == "EXIT")
                    {
                        //lstLog.AppendText($"{key} 主动退出。\n");
                        throw new Exception($"{key} 主动退出");
                    }

                }
            }
            catch (Exception ex)
            {
                lstLog.AppendText($"客户端 {key} 异常断开：{ex.Message}\n");
                if (matching) {
                    matching = false;
                }
                // 判断游戏是否仍在进行中
               
                timer1.Stop();
                string winner = GetOtherKey(key);
                Broadcast(new GameState
                {
                    IsGameOver = true,
                    WinnerId = winner
                });
                lstLog.AppendText($"玩家 {key} 掉线，{winner} 获胜。\n");
                isGameOver = true;
            }
        }

        private void StartGame()
        {


            // 初始化蛋
            
            
            egg = new Egg(snakes);
            timer1.Interval = 300;
            // 初始化两条蛇（避免位置重叠）
            var rnd = new Random();
            var colors = new[] { Color.Blue, Color.Green, Color.Orange, Color.Purple };
            int row1 = rnd.Next(5, 20);
            int row2 = row1 + 5;
            var keys = clients.Keys.ToList();
            var a1 = new Snake(new Point(10, row1), DIRECTION.RIGHT, colors[0]);
            snakes[keys[0]] = a1;
            directions[keys[0]] = DIRECTION.RIGHT;
            var a2 = new Snake(new Point(10, row2), DIRECTION.RIGHT, colors[1]);
            snakes[keys[1]] = a2;
            directions[keys[1]] = DIRECTION.RIGHT;
            Dictionary<string, Point> np = new Dictionary<string, Point>();
            np[keys[0]] = new Point(10, row1);
            np[keys[1]] = new Point(10, row2);
            // 初次新增label蛇身
            eaten[keys[0]] = true;
            eaten[keys[1]] = true;
            var s = new GameState
            {
                Egg = egg.Position,
                SnakesEat = eaten,
                Colors = snakes.ToDictionary(k => k.Key, v => ColorTranslator.ToHtml(v.Value.Color)),
                Directions = directions,
                NextPostitions = np,
                IsGameOver = false
            };
            Broadcast(s);



            this.BeginInvoke((MethodInvoker)(() => timer1.Start()));

            lstLog.AppendText("游戏开始！\n");
        }



        private void Broadcast(GameState state)
        {
            string json = JsonConvert.SerializeObject(state);
            byte[] data = Encoding.UTF8.GetBytes(json);
            
            foreach (var client in clients.Values)
            {
                try
                {
                    client.GetStream().Write(data, 0, data.Length);
                }
                catch (Exception ex)
                { 
                
                }
                
            }
        }

        private string GetOtherKey(string key)
        {

            var otherKey = clients.Keys.FirstOrDefault(k => k != key);
            return otherKey ?? ""; // 或者 return null，根据你希望的默认值
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            timer1.Stop();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            Dictionary<string, Point> nextPositions = new Dictionary<string, Point>();
            Dictionary<string, Point> oldHeads = new Dictionary<string, Point>();
            //MessageBox.Show("呵呵");
            foreach (var kvp in snakes)
            {
                kvp.Value.Direction = directions[kvp.Key];
                nextPositions[kvp.Key] = kvp.Value.GetNextPosition();
                oldHeads[kvp.Key] = kvp.Value.Head;
            }

            var keys = nextPositions.Keys.ToArray();
            if (keys.Length == 2)
            {
                var head1 = nextPositions[keys[0]];
                var head2 = nextPositions[keys[1]];
                var old1 = oldHeads[keys[0]];
                var old2 = oldHeads[keys[1]];

                // 1. 两蛇头移动到同一位置
                if (head1 == head2)
                {
                    Broadcast(new GameState { IsGameOver = true });
                    timer1.Stop();
                    lstLog.AppendText("平局！双方头部相撞。\n");
                    isGameOver = true;
                    return;
                }

                // 2. 头互换位置（对冲而过）
                if (head1 == old2 && head2 == old1)
                {
                    Broadcast(new GameState { IsGameOver = true });
                    timer1.Stop();
                    lstLog.AppendText("平局！双方头部互换位置对冲。\n");
                    isGameOver = true;
                    return;
                }
            }

            // 先清理游戏状态标志
          
            string winnerId = null;
            List<string> deadPlayers = new List<string>();

            foreach (var kvp in snakes)
            {
                var playerId = kvp.Key;
                var snake = kvp.Value;

                // 吃蛋判断
                if (nextPositions[playerId] == egg.Position)
                {
                    
                    snake.Eat();
                    eaten[playerId] = true;
                    egg.GenerateNew(snakes);
                    timer1.Interval = Math.Max(50, timer1.Interval - 5);
                }
                else
                {
                    eaten[playerId] = false;
                    snake.Move();
                }

                // 判断是否长度超50，直接胜利
                if (snake.Segments.Count >= 50)
                {
                    timer1.Stop();
                    lstLog.AppendText($"玩家 {playerId} 蛇长达到50，获胜！\n");
                    Broadcast(new GameState { IsGameOver = true, WinnerId = playerId });
                    isGameOver = true;
                    return;
                }
            }

            // 所有玩家动作结束后，再判断死亡状态
            foreach (var kvp in snakes)
            {
                var playerId = kvp.Key;
                var snake = kvp.Value;

                // 撞到自己
                if (snake.CollidesWithSelf())
                {
                    deadPlayers.Add(playerId);
                    continue;
                }

                // 撞到其他人
                foreach (var other in snakes)
                {
                    if (other.Key == playerId) continue;

                    if (other.Value.Segments.Skip(1).Any(p => p == snake.Head))
                    {
                        deadPlayers.Add(playerId);
                        break;
                    }
                }

                // 越界判断
                var head = snake.Head;
                if (head.X < 0 || head.X >= 50 || head.Y < 0 || head.Y >= 50)
                {
                    deadPlayers.Add(playerId);
                }
            }

            // 如果所有玩家都死了，平局
            if (deadPlayers.Count == snakes.Count)
            {
                timer1.Stop();
                lstLog.AppendText("所有玩家都死亡，游戏平局。\n");
                Broadcast(new GameState { IsGameOver = true, WinnerId = null });
                isGameOver = true;
                return;
            }

            // 如果部分玩家死亡，剩下玩家胜利
            if (deadPlayers.Count > 0)
            {
                timer1.Stop();
                foreach (var deadPlayer in deadPlayers)
                {
                    lstLog.AppendText($"玩家 {deadPlayer} 失败。\n");
                }

                // 假设只剩一个赢家（多赢家规则可根据需求改）
                var alivePlayers = snakes.Keys.Except(deadPlayers).ToList();
                if (alivePlayers.Count == 1)
                {
                    winnerId = alivePlayers[0];
                    lstLog.AppendText($"玩家 {winnerId} 获胜！\n");
                }
                //else
                //{
                //    // 多赢家或者都死情况，winnerId置空为平局
                //    winnerId = null;
                //    lstLog.AppendText("游戏结束，无明确赢家。\n");
                //}

                Broadcast(new GameState { IsGameOver = true, WinnerId = winnerId });
                isGameOver = true;
                return;
            }

            // 没有结束游戏



            var state = new GameState
            {
                Egg = egg.Position,
                SnakesEat = eaten,
                IsGameOver = false,
                Colors = snakes.ToDictionary(k => k.Key, v => ColorTranslator.ToHtml(v.Value.Color)),
                NextPostitions = nextPositions,
                Directions = directions
            };


            Broadcast(state);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            lstLog.AppendText("服务端启动...\n");
            StartListening();
            button1.Enabled = false;
        }
    }
}
