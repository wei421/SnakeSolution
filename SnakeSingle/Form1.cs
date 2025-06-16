using SnakeSingle.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SnakeSingle
{
    public partial class Form1 : Form
    {
        private Snake snake;
        private Egg egg;
        private DIRECTION rcvDir = DIRECTION.RIGHT;
        public Form1(Point startPosition)
        {
            InitializeComponent();
            this.StartPosition = FormStartPosition.Manual;
            this.Location = startPosition;
            this.FormClosing += Form1_FormClosing;
           
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                Debug.WriteLine("窗体正在关闭");

                timer1.Stop();
                timer1.Dispose();
                panel1.Controls.Clear();
                
            }
            catch (Exception ex)
            {
                //MessageBox.Show("关闭连接出错：" + ex.Message);
            }
        }


        private void Form1_Load(object sender, EventArgs e)
        {
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.ClientSize = new Size(500, 500);
            this.snake = new Snake(panel1);
            this.egg = new Egg(panel1);
            RefreshEgg();

        }

        private void RefreshEgg() {
            Point p = egg.RandomPos();
            while (snake.detectCollision(p)) {
                p = egg.RandomPos();
            }
            egg.Update(p);
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down)
            {
                // 下箭头按下了
                Console.WriteLine("按下了↓键");
                // 执行你的逻辑，如改变蛇的方向
                rcvDir = DIRECTION.DOWN;
            }
            else if (e.KeyCode == Keys.Up) {
                rcvDir = DIRECTION.UP;
            }
            else if (e.KeyCode == Keys.Left)
            {
                rcvDir = DIRECTION.LEFT;
            }
            else if (e.KeyCode == Keys.Right)
            {
                rcvDir = DIRECTION.RIGHT;
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            snake.Direction = rcvDir;
            Point p1 = snake.getHeadNext();
            Point p2 = egg.EggData;
            if (p1.X == p2.X && p1.Y == p2.Y) {
                snake.eat();
                RefreshEgg();
                return;
            }
            snake.move();
            if (snake.Status == STATUS.DEAD) {
                timer1.Stop();
                timer1.Dispose();
                MessageBox.Show("你的蛇挂了。");
                //resetConfig();
                //snake.Clear();
                //RefreshEgg();
                //panel1.Controls.Clear();
                this.BeginInvoke(new Action(() =>
                {
                    this.Close();
                }));
            }
        }

        public void resetConfig() {
            rcvDir = DIRECTION.RIGHT;
            timer1.Start();
        }
    }
}
