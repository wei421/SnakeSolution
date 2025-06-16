using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;

using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SnakeSingle.Properties
{
    enum DIRECTION { 
        UP,DOWN,LEFT,RIGHT
    }

    enum STATUS
    {
        ALIVE,DEAD
    }


    internal class Snake
    {
        private int cubeSideLen;
        private DIRECTION direction;
        private Color snakeColor = Color.Green;
        private int snakeInitLen = 5;
        private int labelWidth = 10;
        private List<Label> snakeData = new List<Label>();
        private Panel panel;
        private STATUS status;
        // private Label snakeLabel;
       
        public DIRECTION Direction
        {
            get { return direction; }
            set {
                                
                if (direction == DIRECTION.UP || direction == DIRECTION.DOWN)
                {
                    if (value == DIRECTION.RIGHT || value == DIRECTION.LEFT)
                    {
                        direction = value;  // 只有非行进方向才能改变，行进方向上不能突变
                    }
                }
                else {
                    
                    if (value == DIRECTION.UP || value == DIRECTION.DOWN)
                    {
                        direction = value;
                    }
                }
                
            }
        }

        public STATUS Status
        {
            get { return status; }
        
        }



        public void Clear() {
            status = STATUS.ALIVE;
            direction = DIRECTION.RIGHT;
            for (int i = 0; i < snakeData.Count; i++) {
                panel.Controls.Remove(snakeData[i]);
            }
            snakeData = new List<Label>();
            for (int i = 0; i < snakeInitLen; i++)
            {
                Label snakeLabel = new Label();
                // 设置位置（相对于容器，如 panel 的左上角）
                snakeLabel.Location = new Point(i * 10, 240);  // X=100, Y=150
                snakeLabel.Size = new Size(labelWidth, labelWidth);
                // 设置背景颜色
                snakeLabel.BackColor = snakeColor;
                // 可选：设置边框
                snakeLabel.BorderStyle = BorderStyle.FixedSingle;
                // 可选：设置文本（如果不想显示文字可以留空）
                snakeLabel.Text = "";
                snakeLabel.BorderStyle = BorderStyle.None;
                // 将它添加到容器中，例如 panel1
                panel.Controls.Add(snakeLabel);
                snakeData.Add(snakeLabel);
            }
        }
        public Snake(Panel panel)
        {
            this.panel = panel;
            Clear();
     
        }

        public Point getHeadNext() {
            // 头下一步的位置
            Label head = snakeData[snakeData.Count - 1];  // 队尾（新加入的“蛇头”）
            Point newP; ;
            if (direction == DIRECTION.UP)
            {
                newP = new Point(head.Location.X, head.Location.Y - labelWidth);

            }
            else if (direction == DIRECTION.DOWN)
            {
                newP = new Point(head.Location.X, head.Location.Y + labelWidth);
            }
            else if (direction == DIRECTION.LEFT)
            {
                newP = new Point(head.Location.X - labelWidth, head.Location.Y);

            }
            else
            {
                newP = new Point(head.Location.X + labelWidth, head.Location.Y);
            }
            return newP;
        }
        public void move() {

            Point newP = getHeadNext();
            checkCollision(newP);
            if (status == STATUS.DEAD) {
                return;
            }
            Label tmp = snakeData[0];
            snakeData.RemoveAt(0);  // 出队
            tmp.Location = newP;
            snakeData.Add(tmp);

        }

        public void eat()
        {
            
            Point newP = getHeadNext();
            Label snakeLabel = new Label();
            // 设置位置（相对于容器，如 panel 的左上角）
            snakeLabel.Location = newP;  
            snakeLabel.Size = new Size(labelWidth, labelWidth);
            // 设置背景颜色
            snakeLabel.BackColor = snakeColor;
            // 可选：设置边框
            snakeLabel.BorderStyle = BorderStyle.FixedSingle;
            // 可选：设置文本（如果不想显示文字可以留空）
            snakeLabel.Text = "";
            snakeLabel.BorderStyle = BorderStyle.None;
            // 将它添加到容器中，例如 panel1
            panel.Controls.Add(snakeLabel);
            snakeData.Add(snakeLabel);

        }




        private void checkCollision(Point newP)
        {
            if (newP.X < 0 || newP.X > 490 || newP.Y < 0 || newP.Y > 490)
                status = STATUS.DEAD;  // 撞边

            for (int i = 1; i < snakeData.Count; i++) {
                if (snakeData[i].Location.X == newP.X && snakeData[i].Location.Y == newP.Y) {

                    status = STATUS.DEAD;  // 撞自己
                }
            }
        }


        public bool detectCollision(Point newP)
        {
            // 生成egg不要和蛇身碰撞
            for (int i = 0; i < snakeData.Count; i++)
            {
                if (snakeData[i].Location.X == newP.X && snakeData[i].Location.Y == newP.Y)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
