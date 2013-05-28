/*
 * -----------------------------------------*
 * Made By PKU_3_IDIORTS                    *
 * 北京大学 计算机系：闫任驰，许伦博，赵恺  *
 * 最终完成时间：2011年12月1日              *
 * -----------------------------------------*
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Forms;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Research.Kinect.Nui;
using Coding4Fun.Kinect.Wpf;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace CURSORS
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {

        private const int PlayerNum = 2;
        private const int SpeedNum = 6;
        private const int TurnNum = 5;

        private const float BeginThreshold = 0.18f;     //开始命令的阈值
        private const float StopThreshold = 0.15f;      //停止命令的阈值
        private const float FrontThreshold = 0.05f;     //调整身体朝向的阈值
        private const float TailThreshold = 0.20f;      //侧踢甩尾的阈值
        private const float ForwardThreshold = 0.45f;   //手向前伸的阈值
        private const float NearThreshold = 0.10f;      //两手靠近的阈值
        private const float KneeThreshold = 0.08f;      //膝盖动作的阈值
        private readonly float[] SpeedThreshold = new float[SpeedNum]   //速度阈值0~5档  
            { 0.00f, 0.30f, 0.38f, 0.46f, 0.54f, 0.60f };
        private readonly float[] TurnThreshold = new float[TurnNum]     //转弯阈值0~4档
            { 0.00f, 0.30f, 0.40f, 0.50f, 0.60f };
        private readonly float[] HeadShakeThreshold = new float[TurnNum]//摇头转弯阈值0~4档
            { 0.00f, 0.08f, 0.14f, 0.20f, 0.25f };
        private readonly float[] SteeringWheelThreshold = new float[TurnNum] //方向盘转弯阈值0~4档
           { 0.00f, 0.04f, 0.07f, 0.10f, 0.13f };

        private const float SkeletonMaxX = 0.50f;
        private const float SkeletonMaxY = 0.30f;

        Point[] oldleftpoint = new Point[PlayerNum];
        Point[] oldrightpoint = new Point[PlayerNum];
        Point newpoint;

        int[] now_speed = new int[PlayerNum] { 0, 0 };          //记录当前速度档数
        int[] now_left = new int[PlayerNum] { 0, 0 };           //记录当前左转档数
        int[] now_right = new int[PlayerNum] { 0, 0 };          //记录当前右转档数

        int[] frame_left = new int[PlayerNum] { 0, 0 };         //记录帧数：左手无动作
        int[] frame_right = new int[PlayerNum] { 0, 0 };        //记录帧数：右手无动作      
        int[] frame_shake = new int[PlayerNum] { 0, 0 };        //记录帧数：扭屁股的次数
        int[] shake = new int[PlayerNum] { 0, 0 };              //记录扭屁股次数
        float[] lastshake = new float[PlayerNum] { 0f, 0f };    //记录上一次扭屁股的参数

        bool[] RaceMode = new bool[PlayerNum] { false, false }; //记录是否在竞速模式        
        bool[] begin = new bool[PlayerNum] { false, false };    //记录是否正在开始
        bool[] stop = new bool[PlayerNum] { true, true };       //记录是否正在停止
        bool[] last_stop = new bool[PlayerNum] { false, false };//记录上一帧有没有发送停止命令
        bool[] TailLeft = new bool[PlayerNum] { false, false }; //记录是否正在左甩尾        
        bool[] TailRight = new bool[PlayerNum] { false, false };//记录是否正在右甩尾
        bool[] left_knee = new bool[PlayerNum] { false, false };//记录当前是否抬左膝
        bool[] right_knee = new bool[PlayerNum] { false, false };//记录当前是否抬右膝

        private NotifyIcon _notifyIcon = new NotifyIcon();
        public MainWindow()
        {
            InitializeComponent();

            newpoint = new Point(0, 0);
            for (int i = 0; i < PlayerNum; i++)
            {
                oldleftpoint[i] = new Point(0, 0);
                oldrightpoint[i] = new Point(0, 0);
            }

            InitializeComponent();
            _notifyIcon.Icon = new System.Drawing.Icon("CursorControl.ico");
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += delegate
            {
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Focus();
            };
        }
        Runtime nui;
        int totalFrames = 0;
        int lastFrames = 0;
        DateTime lastTime = DateTime.MaxValue;
        const int RED_IDX = 2;
        const int GREEN_IDX = 1;
        const int BLUE_IDX = 0;
        byte[] depthFrame32 = new byte[320 * 240 * 4];

        //命令编码常量表
        private readonly int BEGIN = 0;
        private readonly int CHANGE_SPEED = 1;
        private readonly int CHANGE_DIRECTION = 2;
        private readonly int CHANGE_TAIL = 3 ;
        private readonly int STOPFISH = 5;
        
        private readonly int[] SPEED = new int[SpeedNum] { 10, 11, 12, 13, 14, 15 };
        private readonly int[] LEFT = new int[TurnNum] { 7, 5, 3, 1, 0 };
        private readonly int[] RIGHT = new int[TurnNum] { 7, 9, 11, 13, 14 };


        Dictionary<JointID, Brush> jointColors = new Dictionary<JointID, Brush>() { 
            {JointID.HipCenter, new SolidColorBrush(Color.FromRgb(169, 176, 155))},
            {JointID.Spine, new SolidColorBrush(Color.FromRgb(169, 176, 155))},
            {JointID.ShoulderCenter, new SolidColorBrush(Color.FromRgb(168, 230, 29))},
            {JointID.Head, new SolidColorBrush(Color.FromRgb(200, 0,   0))},
            {JointID.ShoulderLeft, new SolidColorBrush(Color.FromRgb(79,  84,  33))},
            {JointID.ElbowLeft, new SolidColorBrush(Color.FromRgb(84,  33,  42))},
            {JointID.WristLeft, new SolidColorBrush(Color.FromRgb(255, 126, 0))},
            {JointID.HandLeft, new SolidColorBrush(Color.FromRgb(215,  86, 0))},
            {JointID.ShoulderRight, new SolidColorBrush(Color.FromRgb(33,  79,  84))},
            {JointID.ElbowRight, new SolidColorBrush(Color.FromRgb(33,  33,  84))},
            {JointID.WristRight, new SolidColorBrush(Color.FromRgb(77,  109, 243))},
            {JointID.HandRight, new SolidColorBrush(Color.FromRgb(37,   69, 243))},
            {JointID.HipLeft, new SolidColorBrush(Color.FromRgb(77,  109, 243))},
            {JointID.KneeLeft, new SolidColorBrush(Color.FromRgb(69,  33,  84))},
            {JointID.AnkleLeft, new SolidColorBrush(Color.FromRgb(229, 170, 122))},
            {JointID.FootLeft, new SolidColorBrush(Color.FromRgb(255, 126, 0))},
            {JointID.HipRight, new SolidColorBrush(Color.FromRgb(181, 165, 213))},
            {JointID.KneeRight, new SolidColorBrush(Color.FromRgb(71, 222,  76))},
            {JointID.AnkleRight, new SolidColorBrush(Color.FromRgb(245, 228, 156))},
            {JointID.FootRight, new SolidColorBrush(Color.FromRgb(77,  109, 243))}
        };

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("User32.dll", EntryPoint = "FindWindow")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll", EntryPoint = "SendMessage", SetLastError = true)]
        public static extern int SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref COPYDATASTRUCT lParam);


        public struct COPYDATASTRUCT
        {
            public int dwData; /// 用户自定义数据
            public int cbData;/// 数据长度
            public string lpData; /// 数据地址指针
        }

        public static void SendMsg(int ID, int direction, int value = 0)
        {
         // const int WM_CHAR = 0x0102;
            const int WM_COPYDATA = 0x004A;
            string msg=new string( new char[]{(char)ID,(char)direction,(char)value });
            
            COPYDATASTRUCT cpd;
            cpd.cbData = msg.Length;
            cpd.lpData = msg;
            cpd.dwData = 0;


            string MyName = "#32770";
            string captionName = "机器鱼控制器"; //可以通过SPY++了解到.  
            IntPtr hWnd = FindWindow(MyName, captionName);//找主窗口.
            //Message msg = Message.Create(hWnd, WM_CHAR, new IntPtr(item), IntPtr.Zero);//创建一个WM_CHAR消息.
           // PostMessage(msg.HWnd, msg.Msg, msg.WParam, msg.LParam);//调用Win32API函数 ,发送消息  
            SendMessage(hWnd, WM_COPYDATA, IntPtr.Zero, ref cpd);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            nui = new Runtime();

            try
            {
                nui.Initialize(RuntimeOptions.UseDepthAndPlayerIndex | RuntimeOptions.UseSkeletalTracking | RuntimeOptions.UseColor);
            }
            catch (InvalidOperationException)
            {
                System.Windows.MessageBox.Show("Runtime initialization failed. Please make sure Kinect device is plugged in.");
                return;
            }

            try
            {
                nui.VideoStream.Open(ImageStreamType.Video, 2, ImageResolution.Resolution640x480, ImageType.Color);
                nui.DepthStream.Open(ImageStreamType.Depth, 2, ImageResolution.Resolution320x240, ImageType.DepthAndPlayerIndex);
            }
            catch (InvalidOperationException)
            {
                System.Windows.MessageBox.Show("Failed to open stream. Please make sure to specify a supported image type and resolution.");
                return;
            }

            lastTime = DateTime.Now;

            nui.DepthFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_DepthFrameReady);
            nui.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(nui_SkeletonFramReady);
            nui.VideoFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_ColorFrameReady);

            nui.SkeletonEngine.TransformSmooth = true;
            TransformSmoothParameters parameters = new TransformSmoothParameters();
            parameters.Smoothing = 0.3f;
            parameters.Correction = 0.0f;
            parameters.Prediction = 0.0f;
            parameters.JitterRadius = 1.0f;
            parameters.MaxDeviationRadius = 0.5f;
            nui.SkeletonEngine.SmoothParameters = parameters;


            
        }

        void nui_DepthFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
           // SendMsg(0, 5, 0);
            PlanarImage Image = e.ImageFrame.Image;
            byte[] convertedDepthFrame = convertDepthFrame(Image.Bits);

            depth.Source = BitmapSource.Create(
                Image.Width, Image.Height, 96, 96, PixelFormats.Bgr32, null, convertedDepthFrame, Image.Width * 4);

            ++totalFrames;

            DateTime cur = DateTime.Now;
            if (cur.Subtract(lastTime) > TimeSpan.FromSeconds(1))
            {
                int frameDiff = totalFrames - lastFrames;
                lastFrames = totalFrames;
                lastTime = cur;
                frameRate.Text = frameDiff.ToString() + " fps";
            }
        }

        private Point getDisplayPosition(Joint joint)
        {
            float depthX, depthY;
            nui.SkeletonEngine.SkeletonToDepthImage(joint.Position, out depthX, out depthY);
            depthX = depthX * 320; //convert to 320, 240 space
            depthY = depthY * 240; //convert to 320, 240 space
            int colorX, colorY;
            ImageViewArea iv = new ImageViewArea();
            // only ImageResolution.Resolution640x480 is supported at this point
            nui.NuiCamera.GetColorPixelCoordinatesFromDepthPixel(ImageResolution.Resolution640x480, iv, (int)depthX, (int)depthY, (short)0, out colorX, out colorY);

            // map back to skeleton.Width & skeleton.Height
            return new Point((int)(skeleton.Width * colorX / 640.0), (int)(skeleton.Height * colorY / 480));
        }

        Polyline getBodySegment(Microsoft.Research.Kinect.Nui.JointsCollection joints, Brush brush, params JointID[] ids)
        {
            PointCollection points = new PointCollection(ids.Length);
            for (int i = 0; i < ids.Length; ++i)
            {
                points.Add(getDisplayPosition(joints[ids[i]]));
            }

            Polyline polyline = new Polyline();
            polyline.Points = points;
            polyline.Stroke = brush;
            polyline.StrokeThickness = 5;
            return polyline;
        }

        void nui_SkeletonFramReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            SkeletonFrame skeletonframe = e.SkeletonFrame;
            int iSkeleton = 0;
            Brush[] brushes = new Brush[6];
            brushes[0] = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            brushes[1] = new SolidColorBrush(Color.FromRgb(0, 255, 0));
            brushes[2] = new SolidColorBrush(Color.FromRgb(64, 255, 255));
            brushes[3] = new SolidColorBrush(Color.FromRgb(255, 255, 64));
            brushes[4] = new SolidColorBrush(Color.FromRgb(255, 64, 255));
            brushes[5] = new SolidColorBrush(Color.FromRgb(128, 128, 255));

            skeleton.Children.Clear();
            foreach (SkeletonData data in skeletonframe.Skeletons)
            {
                if (SkeletonTrackingState.Tracked == data.TrackingState)
                {
                    // Draw bones
                    Brush brush = brushes[iSkeleton % brushes.Length];
                    skeleton.Children.Add(getBodySegment(data.Joints, brush, JointID.HipCenter, JointID.Spine, JointID.ShoulderCenter, JointID.Head));
                    skeleton.Children.Add(getBodySegment(data.Joints, brush, JointID.ShoulderCenter, JointID.ShoulderLeft, JointID.ElbowLeft, JointID.WristLeft, JointID.HandLeft));
                    skeleton.Children.Add(getBodySegment(data.Joints, brush, JointID.ShoulderCenter, JointID.ShoulderRight, JointID.ElbowRight, JointID.WristRight, JointID.HandRight));
                    skeleton.Children.Add(getBodySegment(data.Joints, brush, JointID.HipCenter, JointID.HipLeft, JointID.KneeLeft, JointID.AnkleLeft, JointID.FootLeft));
                    skeleton.Children.Add(getBodySegment(data.Joints, brush, JointID.HipCenter, JointID.HipRight, JointID.KneeRight, JointID.AnkleRight, JointID.FootRight));

                    // Draw joints
                    foreach (Joint joint in data.Joints)
                    {
                        Point jointPos = getDisplayPosition(joint);
                        Line jointLine = new Line();
                        jointLine.X1 = jointPos.X - 3;
                        jointLine.X2 = jointLine.X1 + 6;
                        jointLine.Y1 = jointLine.Y2 = jointPos.Y;
                        jointLine.Stroke = jointColors[joint.ID];
                        jointLine.StrokeThickness = 6;
                        skeleton.Children.Add(jointLine);
                    }
                }
                iSkeleton++;
                //System.Windows.Forms.MessageBox.Show(data.UserIndex+"dddd");
                if (data.UserIndex == 254)
                {
                    int new_left = 0;   //新左转档数
                    int new_right = 0;  //新右转档数
                    int new_speed = 0;  //新速度档数

                    bool left_hold = false;     //左手停留
                    bool right_hold = false;    //右手停留

                    bool send_begin = false;    //是否发送开始命令
                    bool send_stop = false;     //是否发送停止命令
                    bool send_left = false;     //是否发送左转命令
                    bool send_right = false;    //是否发送右转命令
                    bool send_speed = false;    //是否发送速度命令
                    bool send_tailleft = false; //是否发送左甩尾命令
                    bool send_tailright = false;//是否发送右甩尾命令

                    //读取左手、右手、脊椎、头部、左肩、右键的数据
                    Joint jointleft = data.Joints[JointID.HandLeft];
                    Joint jointright = data.Joints[JointID.HandRight];
                    Joint jointspine = data.Joints[JointID.Spine];
                    Joint jointhead = data.Joints[JointID.Head];
                    Joint jointleftshoulder = data.Joints[JointID.ShoulderLeft];
                    Joint jointrightshoulder = data.Joints[JointID.ShoulderRight];
                    Joint jointleftfoot = data.Joints[JointID.FootLeft];
                    Joint jointrightfoot = data.Joints[JointID.FootRight];
                    Joint jointleftknee = data.Joints[JointID.KneeLeft];
                    Joint jointrightknee = data.Joints[JointID.KneeRight];
                    Joint jointleftelbow = data.Joints[JointID.ElbowLeft];
                    Joint jointrightelbow = data.Joints[JointID.ElbowRight];


                    Joint scaleRight = jointright.ScaleTo((int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight, SkeletonMaxX, SkeletonMaxY);
                    Joint scaleLeft = jointleft.ScaleTo((int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight, SkeletonMaxX, SkeletonMaxY);

                    //识别是几号玩家；
                    int ID = 0;
                    if ((jointhead.Position.X + jointspine.Position.X) > 0) ID = 1;

                    //判断左手是否停留超过1s
                    newpoint = new Point((int)scaleLeft.Position.X, (int)scaleLeft.Position.Y);
                    if (newpoint.X > (oldleftpoint[ID].X - 30) && newpoint.Y > (oldleftpoint[ID].Y - 30) &&
                        newpoint.X < (oldleftpoint[ID].X + 30) && newpoint.Y < (oldleftpoint[ID].Y + 30))
                    {
                        frame_left[ID]++;
                    }
                    else
                    {
                        frame_left[ID] = 0;
                        oldleftpoint[ID] = newpoint;
                    }
                    if (frame_left[ID] > 15)
                    {
                        oldleftpoint[ID] = newpoint;
                        left_hold = true;
                        frame_left[ID] = 0;
                    }

                    //判断右手是否停留超过1s
                    newpoint = new Point((int)scaleRight.Position.X, (int)scaleRight.Position.Y);
                    if (newpoint.X > (oldrightpoint[ID].X - 30) && newpoint.Y > (oldrightpoint[ID].Y - 30) &&
                        newpoint.X < (oldrightpoint[ID].X + 30) && newpoint.Y < (oldrightpoint[ID].Y + 30))
                    {
                        frame_right[ID]++;
                    }
                    else
                    {
                        frame_right[ID] = 0;
                        oldrightpoint[ID] = newpoint;
                    }
                    if (frame_right[ID] > 15)
                    {
                        oldrightpoint[ID] = newpoint;
                        right_hold = true;
                        frame_right[ID] = 0;
                    }

                    //判断是否有一只手举过头顶超过1s，表示鱼的开始
                    if (((right_hold && jointright.Position.Y - jointhead.Position.Y > BeginThreshold) ||
                        (left_hold && jointleft.Position.Y - jointhead.Position.Y > BeginThreshold)) && stop[ID])
                    {
                        begin[ID] = true;
                        send_begin = true;
                        stop[ID] = false;
                    }

                    //判断鱼的停止或刹车
                    if (((System.Math.Abs(jointright.Position.Y - jointhead.Position.Y) < StopThreshold) &&
                         (System.Math.Abs(jointright.Position.Z - jointhead.Position.Z) < StopThreshold))
                       || ((System.Math.Abs(jointleft.Position.Y - jointhead.Position.Y) < StopThreshold) &&
                          (System.Math.Abs(jointleft.Position.Z - jointhead.Position.Z) < StopThreshold)))
                    {
                        send_stop = true;//相当于发送刹车命令，一只手停在头旁边表示刹车，放下手时刹车取消
                        //判断是否双手抱头且超过1s，表示鱼的停止，游戏结束                    
                        if ((System.Math.Abs(jointright.Position.Y - jointhead.Position.Y) < StopThreshold) && begin[ID] &&
                        (System.Math.Abs(jointleft.Position.Y - jointhead.Position.Y) < StopThreshold) && (right_hold || left_hold))
                        {
                            stop[ID] = true;
                            begin[ID] = false;
                        }
                    }

                    //输出身体姿势调整信息
                    if (ID == 0)
                    {
                        if (jointrightshoulder.Position.Z - jointspine.Position.Z > FrontThreshold)
                            this.textBlock0.Text = "请左侧选手调整身体朝向,向左微转以更好的识别！";
                        else
                            if (jointleftshoulder.Position.Z - jointspine.Position.Z > FrontThreshold)
                                this.textBlock0.Text = "请左侧选手调整身体朝向,向右微转以更好的识别！";
                            else
                                this.textBlock0.Text = "左侧选手身体朝向正确！";
                    }
                    if (ID == 1)
                    {
                        if (jointrightshoulder.Position.Z - jointspine.Position.Z > FrontThreshold)
                            this.textBlock1.Text = "请右侧选手调整身体朝向,向左微转以更好的识别！";
                        else
                            if (jointleftshoulder.Position.Z - jointspine.Position.Z > FrontThreshold)
                                this.textBlock1.Text = "请右侧选手调整身体朝向,向右微转以更好的识别！";
                            else
                                this.textBlock1.Text = "右侧选手身体朝向正确！";
                    }

                    //输出选手停止信息
                    if (!begin[ID])
                    {
                        if (ID == 0)
                            this.textBlock2.Text = "左侧选手已停止";
                        else
                            this.textBlock3.Text = "右侧选手已停止";
                    }

                    //竞速模式开启：双手平行前伸，作握方向盘状停留1秒
                    if ((jointspine.Position.Z - jointleft.Position.Z > ForwardThreshold) && (jointspine.Position.Z - jointright.Position.Z > ForwardThreshold)
                        && (System.Math.Abs(jointleft.Position.Y - jointright.Position.Y) < NearThreshold / 2) && (left_hold || right_hold))
                    {
                        if (!RaceMode[ID])
                        {
                            send_begin = true;
                            now_speed[ID] = 0;
                        }
                        RaceMode[ID] = true;
                    }
                    //竞速模式关闭：双手同时自然下垂
                    if ((jointspine.Position.Z - jointleft.Position.Z < ForwardThreshold - 0.2) && (jointspine.Position.Z - jointright.Position.Z < ForwardThreshold - 0.2))
                    {
                        if (RaceMode[ID])
                        {
                            send_begin = true;
                            now_speed[ID] = 0;
                        }
                        RaceMode[ID] = false;
                    }

                    //分模式分析动作
                    if (RaceMode[ID])//处理竞速模式下的控制
                    {
                        if (begin[ID])
                        {
                            if (ID == 0)
                                this.textBlock2.Text = "!!左侧选手已开启竞速模式!!";
                            else
                                this.textBlock3.Text = "!!右侧选手已开启竞速模式!!";
                        }
                        //在竞速模式下判断加速、减速
                        float deltaKneeY = jointleftknee.Position.Y - jointrightknee.Position.Y;
                        float deltaKneeZ = System.Math.Abs(jointleftknee.Position.Z - jointrightknee.Position.Z);
                        if (deltaKneeZ > KneeThreshold)
                        {
                            if (deltaKneeY < -KneeThreshold)//识别速度+1档
                            {
                                if (!right_knee[ID] && now_speed[ID] < 5)//超过上限就不再加了
                                {
                                    send_speed = true;
                                    now_speed[ID]++;
                                    right_knee[ID] = true;
                                }
                            }
                            else
                                right_knee[ID] = false;
                            if (deltaKneeY > KneeThreshold)//识别速度-1档
                            {
                                if (!left_knee[ID])
                                {
                                    send_speed = true;
                                    now_speed[ID]--;
                                    left_knee[ID] = true;
                                    if (now_speed[ID] == -1)//从速度最低档0档再减则认为是刹车
                                        send_stop = true;
                                }
                            }
                            else
                                left_knee[ID] = false;
                            if (now_speed[ID] < 0)//保持刹车状态，以便下次+1档后即恢复速度最低档0档
                            {
                                now_speed[ID] = -1;
                                send_speed = false;
                            }
                        }
                        else
                            left_knee[ID] = right_knee[ID] = false;

                        //在竞速模式下通过方向盘转弯,双手握方向盘才能转弯
                        if ((jointspine.Position.Z - jointleft.Position.Z > ForwardThreshold) && (jointspine.Position.Z - jointright.Position.Z > ForwardThreshold))
                        {
                            float steeringWheel = jointleft.Position.Y - jointright.Position.Y;
                            if (steeringWheel < 0)
                            {
                                for (int i = TurnNum - 1; i >= 0; i--)
                                    if (steeringWheel < -SteeringWheelThreshold[i])
                                    {
                                        new_left = i;
                                        break;
                                    }
                                if (new_left != now_left[ID])
                                {
                                    now_left[ID] = new_left;
                                    send_left = true;
                                }
                            }
                            else
                            {
                                for (int i = TurnNum - 1; i >= 0; i--)
                                    if (steeringWheel > SteeringWheelThreshold[i])
                                    {
                                        new_right = i;
                                        break;
                                    }
                                if (new_right != now_right[ID])
                                {
                                    now_right[ID] = new_right;
                                    send_right = true;
                                }
                            }
                        }
                    }
                    else//处理普通模式下的控制
                    {
                        if (begin[ID])
                        {
                            if (ID == 0)
                                this.textBlock2.Text = "左侧选手已开始，目前处于非竞速模式~";
                            else
                                this.textBlock3.Text = "右侧选手已开始，目前处于非竞速模式~";
                        }
                        //转向控制
                        //计算摇头转弯
                        float headshake = jointhead.Position.X - jointspine.Position.X;
                        //判断鱼的左转幅度是几档，0不转，1最小，4最大
                        float turn = jointspine.Position.X - jointleft.Position.X;
                        for (int i = TurnNum - 1; i >= 0; i--)
                            if (turn > TurnThreshold[i] || headshake < -HeadShakeThreshold[i])
                            {
                                new_left = i;
                                break;
                            }
                        if (new_left != now_left[ID])
                        {
                            now_left[ID] = new_left;
                            send_left = true;
                        }
                        //判断鱼的右转幅度是几档，0不转，1最小，4最大
                        turn = jointright.Position.X - jointspine.Position.X;
                        for (int i = TurnNum - 1; i >= 0; i--)
                            if (turn > TurnThreshold[i] || headshake > HeadShakeThreshold[i])
                            {
                                new_right = i;
                                break;
                            }
                        if (new_right != now_right[ID])
                        {
                            now_right[ID] = new_right;
                            send_right = true;
                        }

                        //速度控制                    
                        //判断鱼的前进速度是几档，0最小，5最大
                        float speed = System.Math.Max(jointspine.Position.Z - jointleft.Position.Z, jointspine.Position.Z - jointright.Position.Z);
                        for (int i = SpeedNum - 1; i >= 0; i--)
                            if (speed > SpeedThreshold[i])
                            {
                                new_speed = i;
                                break;
                            }
                        //如果连续60帧内被识别的扭屁股次数>=2则全速前进
                        if (++frame_shake[ID] < 60)
                        {
                            if (headshake * lastshake[ID] < 0)
                                shake[ID]++;
                        }
                        else
                            shake[ID] = frame_shake[ID] = 0;
                        lastshake[ID] = headshake;
                        if (shake[ID] >= 2) new_speed = SpeedNum - 1;
                        //速度如有更改或刚刹车结束则发送速度命令
                        if ((new_speed != now_speed[ID]) || (last_stop[ID] && !send_stop))
                        {
                            now_speed[ID] = new_speed;
                            send_speed = true;
                        }
                        else
                            send_speed = false;

                        //判断伸出左腿,左甩尾击打球
                        float tail = jointspine.Position.X - jointleftfoot.Position.X;
                        if (tail > TailThreshold)
                        {
                            if (!TailLeft[ID]) send_tailleft = true;
                            TailLeft[ID] = true;
                        }
                        else TailLeft[ID] = false;

                        //判断伸出右腿，右甩尾击打球
                        tail = jointrightfoot.Position.X - jointspine.Position.X;
                        if (tail > TailThreshold)
                        {
                            if (!TailRight[ID]) send_tailright = true;
                            TailRight[ID] = true;
                        }
                        else TailRight[ID] = false;
                    }

                    //处理发送命令的优先级
                    if (send_begin)
                        SendMsg(ID, BEGIN);
                    if (begin[ID])//所有命令在开始后才可以执行
                    {
                        if (!send_stop && send_speed)//只要没有刹车命令就可以发送改变速度的命令
                            SendMsg(ID, CHANGE_SPEED, SPEED[now_speed[ID]]);
                        if (send_tailleft && !send_tailright)//左右侧踢只能执行一个，都踢时只执行左踢
                            SendMsg(ID, CHANGE_TAIL,0);
                        if (send_tailright && !send_tailleft)
                            SendMsg(ID, CHANGE_TAIL,1);
                        if (send_left && send_right)//左右转向只能执行一个,档数相同时只执行左转
                        {
                            if (new_left >= new_right)
                                send_right = false;
                            else
                                send_left = false;
                        }
                        if (send_left && new_left >= now_right[ID])//执行左转的条件还有要比当前已有的右转的档数大
                            SendMsg(ID, CHANGE_DIRECTION, LEFT[now_left[ID]]);
                        if (send_right && new_right >= now_left[ID])//执行右转的条件还有要比当前已有的左转的档数大
                            SendMsg(ID, CHANGE_DIRECTION, RIGHT[now_right[ID]]);
                    }
                    if (send_stop)
                        SendMsg(ID, STOPFISH);
                    last_stop[ID] = send_stop;//记录当前帧有没有发送刹车命令，用于下一帧
                }
            }
        }


        byte[] convertDepthFrame(byte[] depthFrame16)
        {
            for (int i16 = 0, i32 = 0; i16 < depthFrame16.Length && i32 < depthFrame32.Length; i16 += 2, i32 += 4)
            {
                int player = depthFrame16[i16] & 0x07;
                int realDepth = (depthFrame16[i16 + 1] << 5) | (depthFrame16[i16] >> 3);
                // transform 13-bit depth information into an 8-bit intensity appropriate
                // for display (we disregard information in most significant bit)
                byte intensity = (byte)(255 - (255 * realDepth / 0x0fff));

                depthFrame32[i32 + RED_IDX] = 0;
                depthFrame32[i32 + GREEN_IDX] = 0;
                depthFrame32[i32 + BLUE_IDX] = 0;
                // choose different display colors based on player
                switch (player)
                {
                    case 0:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity / 2);
                        break;
                    case 1:
                        depthFrame32[i32 + RED_IDX] = intensity;
                        break;
                    case 2:
                        depthFrame32[i32 + GREEN_IDX] = intensity;
                        break;
                    case 3:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity / 4);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity);
                        break;
                    case 4:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity / 4);
                        break;
                    case 5:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity / 4);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity);
                        break;
                    case 6:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity);
                        break;
                    case 7:
                        depthFrame32[i32 + RED_IDX] = (byte)(255 - intensity);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(255 - intensity);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(255 - intensity);
                        break;
                }
            }
            return depthFrame32;
        }

        void nui_ColorFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            // 32-bit per pixel, RGBA image
            PlanarImage Image = e.ImageFrame.Image;
            video.Source = BitmapSource.Create(
                Image.Width, Image.Height, 96, 96, PixelFormats.Bgr32, null, Image.Bits, Image.Width * Image.BytesPerPixel);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _notifyIcon.Visible = false;
            nui.Uninitialize();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState.Minimized == WindowState.Minimized)
            {
                this.Hide();
            }
        }

        private void video_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {

        }

        private void depth_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {

        }

    }
} 