using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.ComponentModel;
using System.Threading;
using JuliusSweetland.OptiKey.Contracts;
using JuliusSweetland.OptiKey.Static;
using System.Runtime.InteropServices;

namespace EyeGestures
{
    public class MousePosition
    {
        public double x { get; set; }
        public double y { get; set; }
        public bool blink { get; set; }
        public bool fixation { get; set; }
    }


    public class EyeGesturesInput : IPointService, IDisposable
    {
        #region Fields
        private event EventHandler<Timestamped<Point>> pointEvent;

        private BackgroundWorker pollWorker;
        private TcpClient tcpClient;
        private NetworkStream networkStream;

        #endregion

        #region Ctor

        private const string ServerIp = "127.0.0.1"; // Replace with your server IP
        private const int ServerPort = 65432; // Replace with your server port

        public EyeGesturesInput()
        {

            try
            {
                pollWorker = new BackgroundWorker();
                pollWorker.DoWork += pollMouse;
                pollWorker.WorkerSupportsCancellation = true;

                tcpClient = new TcpClient();
                tcpClient.Connect(ServerIp, ServerPort);
                networkStream = tcpClient.GetStream();
            }
            catch (Exception ex)
            {
                PublishError(this, ex);
            }
        }

        public void Dispose()
        {
            pollWorker.CancelAsync();
            networkStream.Close();
            tcpClient.Close();
            pollWorker.Dispose();
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        #endregion

        #region Events

        public event EventHandler<Exception> Error;

        public event EventHandler<Timestamped<Point>> Point
        {
            add
            {
                if (pointEvent == null)
                {
                    // Start polling the mouse
                    pollWorker.RunWorkerAsync();
                }

                pointEvent += value;
            }
            remove
            {
                pointEvent -= value;

                if (pointEvent?.GetInvocationList().Length == 0)
                {
                    pollWorker.CancelAsync();
                }
            }
        }

        #endregion

        #region Private methods

        private void pollMouse(object sender, DoWorkEventArgs e)
        {
            while (!pollWorker.CancellationPending)
            {

                try{
                    // Get latest mouse position from the socket
                    var timeStamp = Time.HighResolutionUtcNow.ToUniversalTime();

                    // Read JSON data from the socket
                    byte[] buffer = new byte[1024];
                    int bytesRead = networkStream.Read(buffer, 0, buffer.Length);
                    string jsonData = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    // Parse JSON data
                    var mousePosition = JsonSerializer.Deserialize<MousePosition>(jsonData);

                    // Gets the absolute mouse position, relative to screen
                    double x = mousePosition.x;
                    double y = mousePosition.y;

                    // Emit a point event
                    pointEvent?.Invoke(this, new Timestamped<Point>(new Point((int)x, (int)y), timeStamp));

                }
                catch (Exception ex)
                {
                    PublishError(this, ex);
                }

                // Sleep thread to avoid hot loop
                int delay = 30; // ms
                Thread.Sleep(delay);
            }
        }
        #endregion

        #region Publish Error

        private void PublishError(object sender, Exception ex)
        {
            if (Error != null)
            {
                Error(sender, ex);
            }
        }

        #endregion
    }
}
