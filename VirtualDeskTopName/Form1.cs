using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VirtualDeskTopName
{
    public partial class Form1 : Form
    {
        private Bitmap _bitmap;
        private static IServiceProvider10 _shell;
        private static IVirtualDesktopManagerInternal _manager;

        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private Queue<string> _desktops = new Queue<string>();


        public Form1()
        {
            //  this.DoubleBuffered = true;
            this.SetStyle(
                          ControlStyles.OptimizedDoubleBuffer |
                          ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint, true);
            InitializeComponent();
            _bitmap = new Bitmap(this.Width, this.Height);
            _shell = (IServiceProvider10)Activator.CreateInstance(Type.GetTypeFromCLSID(Guids.CLSID_ImmersiveShell));
            _manager = (IVirtualDesktopManagerInternal)_shell.QueryService(Guids.CLSID_VirtualDesktopManagerInternal, typeof(IVirtualDesktopManagerInternal).GUID);
            SetShape();
            timer1.Interval = 1000;
            timer1.Tick += timer1_Tick;
            timer1.Start();
            SetLocation();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            IntPtr handle = this.Handle;
            int exStyle = GetWindowLong(handle, GWL_EXSTYLE);
            SetWindowLong(handle, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);

        }

        protected override void OnPaint(PaintEventArgs e)
        {
            //base.OnPaint(e);
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            if(_bitmap != null)
                e.Graphics.DrawImage(_bitmap,0,0);


        }

        private void Draw()
        {
            if(_bitmap != null) 
                _bitmap = new Bitmap(this.Width, this.Height);
            Graphics g = Graphics.FromImage(_bitmap);
           
            var curr = _manager.GetCurrentDesktop();
            var vdname = curr.GetName();
            if(_desktops.Count() == 0 || _desktops.Last<string>() != vdname)
            {
                _desktops.Enqueue(vdname);
            }
            var name = _desktops.Dequeue();

            g.Clear(Color.Black);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.DrawString(name,
                new Font("Reddis Sans", 14, FontStyle.Regular),
                new SolidBrush(Color.White),
                new Rectangle(0, 0, 200, 50),
                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center}
                );
            g.Dispose();

        }

   

        private void SetShape()
        {
            var graphicsPath = RoundedRect(new Rectangle(0,0,200,50), 5);
            this.Region = new Region(graphicsPath);
        }



        private GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            System.Drawing.Size size = new System.Drawing.Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location,size);
            GraphicsPath path = new GraphicsPath();
            

            if (radius == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            // top left arc  
            path.AddArc(arc, 180, 90);

            // top right arc  
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);

            // bottom right arc  
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // bottom left arc 
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        protected override void WndProc(ref Message m)
        {
            const int RESIZE_HANDLE_SIZE = 10;

            switch (m.Msg)
            {
                case 0x0084/*NCHITTEST*/ :
                    base.WndProc(ref m);

                    if ((int)m.Result == 0x01/*HTCLIENT*/)
                    {
                        Point screenPoint = new Point(m.LParam.ToInt32());
                        Point clientPoint = this.PointToClient(screenPoint);
                        if (clientPoint.Y <= RESIZE_HANDLE_SIZE)
                        {
                            if (clientPoint.X <= RESIZE_HANDLE_SIZE)
                                m.Result = (IntPtr)13/*HTTOPLEFT*/ ;
                            else if (clientPoint.X < (Size.Width - RESIZE_HANDLE_SIZE))
                                m.Result = (IntPtr)12/*HTTOP*/ ;
                            else
                                m.Result = (IntPtr)14/*HTTOPRIGHT*/ ;
                        }
                        else if (clientPoint.Y <= (Size.Height - RESIZE_HANDLE_SIZE))
                        {
                            if (clientPoint.X <= RESIZE_HANDLE_SIZE)
                                m.Result = (IntPtr)10/*HTLEFT*/ ;
                            else if (clientPoint.X < (Size.Width - RESIZE_HANDLE_SIZE))
                                m.Result = (IntPtr)2/*HTCAPTION*/ ;
                            else
                                m.Result = (IntPtr)11/*HTRIGHT*/ ;
                        }
                        else
                        {
                            if (clientPoint.X <= RESIZE_HANDLE_SIZE)
                                m.Result = (IntPtr)16/*HTBOTTOMLEFT*/ ;
                            else if (clientPoint.X < (Size.Width - RESIZE_HANDLE_SIZE))
                                m.Result = (IntPtr)15/*HTBOTTOM*/ ;
                            else
                                m.Result = (IntPtr)17/*HTBOTTOMRIGHT*/ ;
                        }
                    }
                    return;
            }
            base.WndProc(ref m);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            Draw();
            this.Invalidate();

        }
        private void SetLocation()
        {
            // Check location to see if it is offscreen
            //
            var savedLocation = Properties.Settings.Default.Location;
            //var savedSize = Properties.Settings.Default.Size;



            if (savedLocation.X >= 0 && savedLocation.Y >= 0)
            {
                // Ensure the saved location is visible on any connected screen
                if (IsLocationVisible(savedLocation, this.Height,this.Width))
                {
                    this.StartPosition = FormStartPosition.Manual;
                    this.Location = savedLocation;
                }
                else
                {
                    // If not visible, center on primary screen
                    this.StartPosition = FormStartPosition.CenterScreen;
                }
            }


        }
        private void SaveLocation()
        {
            Properties.Settings.Default.Location = Location;

            Properties.Settings.Default.Save();
        }
        private bool IsLocationVisible(Point location, int h,int w)
        {
            Rectangle formRect = new Rectangle(location.X,location.Y, w,h);
            return Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(formRect));
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            timer1.Stop();
            SaveLocation();
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
        }

        private void Form1_KeyPress(object sender, KeyPressEventArgs e)
        {
            var j = 1;
        }
    }
}
