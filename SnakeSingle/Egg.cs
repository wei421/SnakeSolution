using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SnakeSingle
{
    internal class Egg
    {
        private Color eggColor = Color.Goldenrod;
        private Panel panel;
        private Random rand = new Random();
        private Label eggLabel;
        private int labelWidth = 10;

        public Point EggData { get { return eggLabel.Location; } }
        public Egg(Panel panel)
        {
            this.panel = panel;
            eggLabel = new Label();
            // 设置位置（相对于容器，如 panel 的左上角）

            eggLabel.Size = new Size(labelWidth, labelWidth);
            // 设置背景颜色
            eggLabel.BackColor = eggColor;
            // 可选：设置边框
            eggLabel.BorderStyle = BorderStyle.FixedSingle;
            // 可选：设置文本（如果不想显示文字可以留空）
            eggLabel.Text = "";
            eggLabel.BorderStyle = BorderStyle.None;
            // 将它添加到容器中，例如 panel1
            panel.Controls.Add(eggLabel);
        }
        public Point RandomPos() {
            return  new Point(rand.Next(0, 50)*10, rand.Next(0, 50)*10);
        }

        public void Update(Point p)
        {

            eggLabel.Location = p;
        }
    }
}
