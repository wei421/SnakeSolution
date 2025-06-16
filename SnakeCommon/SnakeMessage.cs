using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace SnakeCommon
{
    public enum DIRECTION { UP, DOWN, LEFT, RIGHT }

    public class Snake
    {
        public List<Point> Segments = new List<Point>();
        public DIRECTION Direction = DIRECTION.RIGHT;
        public bool Dead = false;
        public Color Color { get; set; }
        public Point Head => Segments.Count > 0 ? Segments[0] : Point.Empty;

        public Snake(Point start)
        {
            Segments.Add(start);
        }

        public Snake(Point start, DIRECTION d, Color c)
        {
            Segments.Add(start);
            Direction = d;
            Color = c;

        }

        public void Move()
        {
            if (Dead) return;
            Point head = GetNextPosition();
            Segments.Insert(0, head);
            Segments.RemoveAt(Segments.Count - 1);
        }

        public void Eat()
        {
            Segments.Insert(0, GetNextPosition());
        }

        public Point GetNextPosition()
        {
            Point head = Segments[0];
            switch (Direction)
            {
                case DIRECTION.UP: return new Point(head.X, head.Y - 1);
                case DIRECTION.DOWN: return new Point(head.X, head.Y + 1);
                case DIRECTION.LEFT: return new Point(head.X - 1, head.Y);
                case DIRECTION.RIGHT: return new Point(head.X + 1, head.Y);
            }
            return head;
        }

        public bool CollidesWithSelf()
        {
            return Segments.Skip(1).Any(p => p == Segments[0]);
        }
    }

    public class Egg
    {
        public Point Position;
        private static Random rand = new Random();

        public Egg(Dictionary<string, Snake> snakes)
        {
            GenerateNew(snakes);
        }

        public void GenerateNew(Dictionary<string, Snake> snakes, int maxX = 25, int maxY = 25)
        {
            while (true)
            {
                Point p = new Point(rand.Next(0, maxX), rand.Next(0, maxY));
                bool overlap = snakes.Values.Any(s => s.Segments.Contains(p));
                if (!overlap)
                {
                    Position = p;
                    break;
                }
            }
        }
    }

    public class GameState
    {
        public Point Egg;
        public Dictionary<string, bool> SnakesEat { get; set; } = new Dictionary<string, bool>();
        public Dictionary<string, string> Colors { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, DIRECTION> Directions { get; set; } = new Dictionary<string, DIRECTION>();
        public Dictionary<string, Point> NextPostitions { get; set; } = new Dictionary<string, Point>();
        public string MyId { get; set; }

        public bool IsGameOver;
        public string WinnerId;

    }




}

