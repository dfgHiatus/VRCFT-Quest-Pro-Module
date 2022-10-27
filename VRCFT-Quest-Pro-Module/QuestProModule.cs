using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using VRCFaceTracking;

namespace VRCFT_Quest_Pro_Module
{
    public class QuestProModule : ExtTrackingModule
    {
        public int listenPort = 10000;

        private UdpClient listener;
        private IPEndPoint groupEP;
        private float[] expressions = new float[63 * 4];
        
        public override (bool SupportsEye, bool SupportsLip) Supported => (true, true);
        
        public override (bool eyeSuccess, bool lipSuccess) Initialize(bool eye, bool lip)
        {
            listener = new UdpClient(listenPort);
            groupEP = new IPEndPoint(IPAddress.Any, listenPort);
            return (true, true);
        }
        
        public override Action GetUpdateThreadFunc()
        {
            return () =>
            {
                while (true)
                {
                    Update();
                    Thread.Sleep(10);
                }
            };
        }

        public void Update()
        {
            try
            {
                // We start by receiving the data from the UDP listener.
                // The data is a byte array 63*4 bytes long, since floats are 32 bits long
                Buffer.BlockCopy(listener.Receive(ref groupEP), 0, expressions, 0, 63 * 4);

                // Usage:
                // expressions[FBExpression.Cheek_Suck_L]
            }
            catch (SocketException e)
            {
                Logger.Error(e.Message);
            }
        }

        public override void Teardown()
        {
            listener.Close();
        }
    }
}
