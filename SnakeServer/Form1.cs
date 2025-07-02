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
        private Dictionary<string, GameRoom> activeRooms = new Dictionary<string, GameRoom>();
        private List<TcpClient> waitingClients = new List<TcpClient>();
        private Dictionary<TcpClient, Thread> waitingMonitors = new Dictionary<TcpClient, Thread>();

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

            lstLog.AppendText("监听启动成功，等待客户端连接...\n");
        }


        private List<TcpClient> pendingClients = new List<TcpClient>();
        private object lockObj = new object();

        private void AcceptClientsLoop()
        {
            while (true)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    string remote = client.Client.RemoteEndPoint.ToString();

                    lock (lockObj)
                    {
                        waitingClients.Add(client);
                        lstLog.AppendText($"新客户端 {remote} 加入等待队列。\n");

                        if (waitingClients.Count >= 2)
                        {
                            var player1 = waitingClients[0];
                            var player2 = waitingClients[1];
                            waitingClients.RemoveRange(0, 2);

                            string p1Id = Guid.NewGuid().ToString();
                            string p2Id = Guid.NewGuid().ToString();

                            var room = new GameRoom(lstLog, OnRoomGameOver);
                            room.AddPlayer(p1Id, player1);
                            room.AddPlayer(p2Id, player2);

                            activeRooms[room.RoomId] = room;

                            SendInitState(player1, p1Id);
                            SendInitState(player2, p2Id);

                            StartHandleClient(player1, p1Id, room);
                            StartHandleClient(player2, p2Id, room);

                            room.Start();

                            lstLog.AppendText($"[房间 {room.RoomId}] 玩家 {p1Id} 和 {p2Id} 成功配对，开始游戏。\n");
                        }
                    }
                }
                catch (Exception ex)
                {
                    lstLog.AppendText("接受客户端异常：" + ex.Message + "\n");
                }
            }
        }





        private void OnRoomGameOver(string roomId)
        {
            if (activeRooms.ContainsKey(roomId))
            {
                activeRooms.Remove(roomId);
                lstLog.AppendText($"房间 {roomId} 对局结束，释放资源。\n");
            }
        }

        private void StartHandleClient(TcpClient client, string playerId, GameRoom room)
        {
            new Thread(() =>
            {
                NetworkStream stream = null;
                try
                {
                    stream = client.GetStream(); // 这里可能抛出“非连接套接字”异常
                }
                catch (Exception ex)
                {
                    lstLog.AppendText($"[房间 {room.RoomId}] 玩家 {playerId} 获取流失败：{ex.Message}");
                    room.HandleMessage(playerId, "{\"Type\": \"EXIT\"}");
                    return;
                }

                byte[] buffer = new byte[1024];

                while (true)
                {
                    try
                    {
                        int len = stream.Read(buffer, 0, buffer.Length);
                        if (len <= 0) break;

                        string json = Encoding.UTF8.GetString(buffer, 0, len);
                        room.HandleMessage(playerId, json);
                    }
                    catch
                    {
                        room.HandleMessage(playerId, "{\"Type\": \"EXIT\"}");
                        break;
                    }
                }
            })
            { IsBackground = true }.Start();
        }


        private void SendInitState(TcpClient client, string uuid)
        {
            var init = new GameState { MyId = uuid };
            string json = JsonConvert.SerializeObject(init);
            byte[] data = Encoding.UTF8.GetBytes(json + "\n");
            client.GetStream().Write(data, 0, data.Length);
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
