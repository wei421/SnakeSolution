using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace SnakeClient
{
    public partial class FormMenu : Form
    {
        public FormMenu()
        {
            InitializeComponent();
        }

        private void FormMenu_Load(object sender, EventArgs e)
        {
            this.ClientSize = new Size(500, 500);
            //pictureBox1.Image = Image.FromFile("C:/Users/45277/Desktop/images.jpg");
       
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Point currentLocation = this.Location;
            
            // 隐藏当前窗体
            this.Hide();

            // 打开 FormB
            var formB = new SnakeSingle.Form1(currentLocation);

            // 显示为模态窗口，等待关闭
            formB.ShowDialog();

            // B 关闭后重新显示 A
            this.Show();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                IPAddress ip;
                if (!IPAddress.TryParse(ServerIp.Text.Trim(), out ip))
                {
                    Invoke(new Action(() =>
                    {
                        MessageBox.Show("请输入有效的 IP 地址。");

                    }));
                    return;
                }
                Form1 multiplayerForm = new Form1(ServerIp.Text.Trim());
                multiplayerForm.StartPosition = FormStartPosition.Manual; // 手动指定位置
                multiplayerForm.Location = this.Location;
                multiplayerForm.Show();
                this.Hide(); // 隐藏菜单
                multiplayerForm.FormClosed += (s2, ev2) => this.Show(); // 游戏结束再回菜单
            }
            catch (Exception ex) { }
         
        }
    }
}
