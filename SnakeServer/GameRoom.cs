using Newtonsoft.Json;
using SnakeCommon;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;

namespace SnakeServer
{
    public class GameRoom
    {
        public string RoomId { get; private set; } = Guid.NewGuid().ToString();
        public Dictionary<string, TcpClient> Clients { get; private set; } = new Dictionary<string, TcpClient>();
        public Dictionary<string, Snake> Snakes { get; private set; } = new Dictionary<string, Snake>();
        public Dictionary<string, DIRECTION> Directions { get; private set; } = new Dictionary<string, DIRECTION>();
        public Dictionary<string, bool> Eaten { get; private set; } = new Dictionary<string, bool>();
        public Egg Egg { get; private set; }
        public int Total_len { get; private set; }


        private static readonly HttpClient httpClient = new HttpClient();
        private string ledServerBaseUrl = "http://192.168.1.74:5000"; // 替换为你的树莓派 IP
        private System.Timers.Timer Timer;
        private RichTextBox LogBox;
        private Action<string> OnGameOverCallback;

        private volatile bool isGameOver = false;

        public GameRoom(RichTextBox logBox, Action<string> onGameOver)
        {
            this.LogBox = logBox;
            this.OnGameOverCallback = onGameOver;
            //this.RoomId = Guid.NewGuid().ToString();

            Timer = new System.Timers.Timer();
            Timer.Interval = 300;
            Timer.Elapsed += Timer_Tick;
            Total_len = 2;
        }

        private void FireAndForgetGet(string url)
        {
            Task.Run(async () =>
            {
                try
                {
                    await httpClient.GetAsync(url);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("HTTP 调用失败: " + ex.Message);
                }
            });
        }


        public void StartGameLights()
        {
            string url = $"{ledServerBaseUrl}/start";
            FireAndForgetGet(url);
        }


        public void EatEggLights(string player, int delayMs, int length)
        {
            string url = $"{ledServerBaseUrl}/eat?player={player}&delay={delayMs}&length={length}";
            FireAndForgetGet(url);
        }


        public void EndGameLights(string winner)
        {
            string url = $"{ledServerBaseUrl}/end?winner={winner}";
            FireAndForgetGet(url);
        }

        public void AddPlayer(string id, TcpClient client)
        {
            Clients[id] = client;
            Directions[id] = DIRECTION.RIGHT;
            Snakes[id] = new Snake(new Point(10 + Clients.Count * 10, 5));
            Eaten[id] = false;
        }

        public bool IsFull => Clients.Count == 2;

        public void Start()
        {
            Egg = new Egg(Snakes);

            var rnd = new Random();
            int row1 = rnd.Next(5, 20);
            int row2 = row1 + 5;
            var keys = Clients.Keys.ToList();

            Snakes[keys[0]] = new Snake(new Point(10, row1), DIRECTION.RIGHT, Color.Blue);
            Snakes[keys[1]] = new Snake(new Point(10, row2), DIRECTION.RIGHT, Color.Green);

            var np = new Dictionary<string, Point>
            {
                [keys[0]] = new Point(10, row1),
                [keys[1]] = new Point(10, row2)
            };
            Eaten[keys[0]] = Eaten[keys[1]] = true;

            var state = new GameState
            {
                Egg = Egg.Position,
                SnakesEat = Eaten,
                Colors = Snakes.ToDictionary(k => k.Key, v => ColorTranslator.ToHtml(v.Value.Color)),
                Directions = Directions,
                NextPostitions = np,
                IsGameOver = false
            };

            Broadcast(state);
            Timer.Start();
            LogBox.AppendText($"房间 {RoomId} 开始对局\n");
            StartGameLights();
        }

        private void Timer_Tick(object sender, ElapsedEventArgs e)
        {
            Dictionary<string, Point> nextPositions = new Dictionary<string, Point>();
            Dictionary<string, Point> oldHeads = new Dictionary<string, Point>();

            foreach (var kvp in Snakes)
            {
                kvp.Value.Direction = Directions[kvp.Key];
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

                // 双头相撞
                if (head1 == head2)
                {
                    Broadcast(new GameState { IsGameOver = true });
                    Timer.Stop();
                    LogBox.AppendText($"[房间 {RoomId}] 平局：双方头部相撞。\n");
                    isGameOver = true;
                    OnGameOverCallback?.Invoke(RoomId);
                    EndGameLights("None");
                    return;
                }

                // 对冲而过
                if (head1 == old2 && head2 == old1)
                {
                    Broadcast(new GameState { IsGameOver = true });
                    Timer.Stop();
                    LogBox.AppendText($"[房间 {RoomId}] 平局：双方对冲。\n");
                    isGameOver = true;
                    OnGameOverCallback?.Invoke(RoomId);
                    EndGameLights("None");
                    return;
                }
            }

            string winnerId = null;
            List<string> deadPlayers = new List<string>();

           
            foreach (var kvp in Snakes)
            {
                var playerId = kvp.Key;
                var snake = kvp.Value;

                if (nextPositions[playerId] == Egg.Position)
                {
                    snake.Eat();
                    Total_len += 1;
                    Eaten[playerId] = true;
                    Egg.GenerateNew(Snakes);
                    Timer.Interval = Math.Max(50, Timer.Interval - 20);
                    EatEggLights(snake.Color == Color.Blue ? "blue" : "green", (int) Timer.Interval, Total_len);
                }
                else
                {
                    Eaten[playerId] = false;
                    snake.Move();
                }

                if (snake.Segments.Count >= 50)
                {
                    Timer.Stop();
                    LogBox.AppendText($"[房间 {RoomId}] 玩家 {playerId} 长度达到50，胜利！\n");
                    Broadcast(new GameState { IsGameOver = true, WinnerId = playerId });
                    isGameOver = true;
                    OnGameOverCallback?.Invoke(RoomId);
                    EndGameLights(snake.Color == Color.Blue ? "blue" : "green");
                    return;
                }
               
            }

            // 死亡检测
            foreach (var kvp in Snakes)
            {
                var playerId = kvp.Key;
                var snake = kvp.Value;

                if (snake.CollidesWithSelf())
                {
                    deadPlayers.Add(playerId);
                    continue;
                }

                foreach (var other in Snakes)
                {
                    if (other.Key == playerId) continue;
                    if (other.Value.Segments.Skip(1).Any(p => p == snake.Head))
                    {
                        deadPlayers.Add(playerId);
                        break;
                    }
                }

                var head = snake.Head;
                if (head.X < 0 || head.X >= 50 || head.Y < 0 || head.Y >= 50)
                {
                    deadPlayers.Add(playerId);
                }
            }

            if (deadPlayers.Count == Snakes.Count)
            {
                Timer.Stop();
                LogBox.AppendText($"[房间 {RoomId}] 所有玩家死亡，平局。\n");
                Broadcast(new GameState { IsGameOver = true, WinnerId = null });
                isGameOver = true;
                OnGameOverCallback?.Invoke(RoomId);
                EndGameLights("None");
                return;
            }

            if (deadPlayers.Count > 0)
            {
                Timer.Stop();
                foreach (var dead in deadPlayers)
                {
                    LogBox.AppendText($"[房间 {RoomId}] 玩家 {dead} 死亡。\n");
                }

                var survivors = Snakes.Keys.Except(deadPlayers).ToList();
                if (survivors.Count == 1)
                {
                    winnerId = survivors[0];
                    LogBox.AppendText($"[房间 {RoomId}] 玩家 {winnerId} 获胜！\n");
                    EndGameLights(Snakes[survivors[0]].Color == Color.Blue ? "blue" : "green");
                }

                Broadcast(new GameState { IsGameOver = true, WinnerId = winnerId });
                isGameOver = true;
                OnGameOverCallback?.Invoke(RoomId);
                return;
            }

            // 游戏继续
            var state = new GameState
            {
                Egg = Egg.Position,
                SnakesEat = Eaten,
                IsGameOver = false,
                Colors = Snakes.ToDictionary(k => k.Key, v => ColorTranslator.ToHtml(v.Value.Color)),
                NextPostitions = nextPositions,
                Directions = Directions
            };

            Broadcast(state);
        }

        private void Broadcast(GameState state)
        {
            string json = JsonConvert.SerializeObject(state);
            byte[] data = Encoding.UTF8.GetBytes(json);
            foreach (var client in Clients.Values)
            {
                try { client.GetStream().Write(data, 0, data.Length); } catch { }
            }
        }

        public void HandleMessage(string playerId, string json)
        {
            dynamic msg = JsonConvert.DeserializeObject(json);
            if (msg.Type == "DIRECTION")
            {
                DIRECTION parsedDir;
                if (Enum.TryParse(msg.Direction.ToString(), out parsedDir))
                {
                    Directions[playerId] = parsedDir;
                }
            }
            else if (msg.Type == "EXIT")
            {
                Broadcast(new GameState { IsGameOver = true, WinnerId = GetOtherKey(playerId) });
                Timer.Stop();
                isGameOver = true;
                EndGameLights(Snakes[GetOtherKey(playerId)].Color == Color.Blue ? "blue" : "green");
                OnGameOverCallback(RoomId);
            }
        }

        private string GetOtherKey(string key) => Clients.Keys.FirstOrDefault(k => k != key);
    }
}
