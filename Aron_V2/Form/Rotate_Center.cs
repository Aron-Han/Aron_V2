using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aron_V2
{
    public partial class Rotate_Center : Form
    {
        public Rotate_Center()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            double Rotate_X;
            double Rotate_Y;
            RCenter(Convert.ToDouble(this.textBox1.Text), Convert.ToDouble(this.textBox2.Text), Convert.ToDouble(this.textBox3.Text),
                Convert.ToDouble(this.textBox4.Text), Convert.ToDouble(this.textBox5.Text), out Rotate_X, out Rotate_Y);
            this.textBox6.Text = Rotate_X.ToString();
            this.textBox7.Text = Rotate_Y.ToString();

        }
        /// 相机固定带旋转时，用此方法求旋转中心
        /// </summary>
        /// <param name="X_Current">在拍照位旋转前X坐标</param>
        /// <param name="Y_Current">在拍照位旋转前Y坐标</param>
        /// <param name="X_Rotate">在拍照位旋转后X坐标</param>
        /// <param name="Y_Rotate">在拍照位旋转后Y坐标</param>
        /// <param name="R">旋转角度（单位度）</param>
        /// <param name="X_Center">旋转中心X坐标</param>
        /// <param name="Y_Center">旋转中心Y坐标</param>
        public static void RCenter(double X_Current, double Y_Current, double X_Rotate, double Y_Rotate, double R, out double X_Center, out double Y_Center)
        {
            R = R * Math.PI / 180;
            double A = (X_Rotate - X_Current * Math.Cos(R) + Y_Current * Math.Sin(R)) * Math.Sin(R);
            double B = (Y_Rotate - X_Current * Math.Sin(R) - Y_Current * Math.Cos(R)) * (1 - Math.Cos(R));
            Y_Center = (A + B) / (Math.Pow((1 - Math.Cos(R)), 2) + Math.Pow(Math.Sin(R), 2));

            double C = X_Rotate - X_Current * Math.Cos(R) + Y_Current * Math.Sin(R) - Y_Center * Math.Sin(R);
            X_Center = C / (1 - Math.Cos(R));
        }
    }
}
