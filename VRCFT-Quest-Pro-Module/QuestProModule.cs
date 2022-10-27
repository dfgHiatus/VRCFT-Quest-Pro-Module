using System;
using System.Collections.Generic;
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
        private Dictionary<FBExpression, float> expressions = new Dictionary<FBExpression, float>();
        private byte[] buffer = new byte[63 * 4];
        private byte[] slice = new byte[4];

        public override (bool SupportsEye, bool SupportsLip) Supported => (true, true);
        
        public override (bool eyeSuccess, bool lipSuccess) Initialize(bool eye, bool lip)
        {
            listener = new UdpClient(listenPort);
            groupEP = new IPEndPoint(IPAddress.Any, listenPort);
            
            foreach (FBExpression expression in Enum.GetValues(typeof(FBExpression)))
            {
                expressions.Add(expression, 0f);
            }

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
                buffer = listener.Receive(ref groupEP);
                for (int i = 0; i < buffer.Length; i += 4)
                {
                    slice[0] = buffer[i];
                    slice[1] = buffer[i + 1];
                    slice[2] = buffer[i + 2];
                    slice[3] = buffer[i + 3];
                    expressions[(FBExpression)i] = BitConverter.ToSingle(slice, 0);
                }
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
