using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Reflection;
using ViveSR.anipal.Lip;
using VRCFaceTracking;
using VRCFaceTracking.Params;


namespace VRCFT_Quest_Pro_Module
{
    public class QuestProModule : ExtTrackingModule
    {
        public IPAddress localAddr; // = IPAddress.Parse("192.168.1.163");
        public int port; // = 13191;
        
        // private TcpListener server;
        private TcpClient client;
        private NetworkStream stream;
        
        private const int expressionsSize = 63 * 4;
        private byte[] rawExpressions = new byte[expressionsSize];
        private float[] expressions = new float[expressionsSize];

        public override (bool SupportsEye, bool SupportsLip) Supported => (true, true);
        
        public override (bool eyeSuccess, bool lipSuccess) Initialize(bool eye, bool lip)
        {
            // Open the config file in the same directory called config.json
            string configPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json");
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<Config>(json);
                localAddr = config.IP;
                port = config.Port;
            }
            else
            {
                Logger.Error("Failed to find config JSON! Please maker sure it is present in the same directory as the DLL.");
                return (false, false);
            }

            // server = new TcpListener(localAddr, port);
            // server.Start();
            // client = server.AcceptTcpClient(); // Blocks indefintely until a connection is made

            client = new TcpClient(new IPEndPoint(localAddr, port));
            if (client == null)
            {
                Logger.Error("Failed to connect to client");
                return (false, false);
            }

            stream = client.GetStream();
            if (stream == null)
            {
                Logger.Error("Failed to get stream");
                return (false, false);
            }

            Logger.Msg("Connected to client and stream!");
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

        private void Update()
        {
            try
            {
                if (!stream.CanRead)
                    return;

                // Read from the stream. If there is nothing to read, return
                if (stream.Read(rawExpressions, 0, expressionsSize) != 0)
                    return;

                // We receive information from the stream as a byte array 63*4 bytes long, since floats are 32 bits long and we have 63 expressions.
                // We then need to convert this byte array to a float array. Thankfully, this can all be done in a single line of code.
                Buffer.BlockCopy(rawExpressions, 0, expressions, 0, expressionsSize);
                
                UpdateExpressions();
            }
            catch (SocketException e)
            {
                Logger.Error(e.Message);
            }
        }

        public override void Teardown()
        {
            stream.Close();
            stream.Dispose();
            client.Close();
            client.Dispose();
            // server.Stop();
        }

        // Thank you @adjerry on the VRCFT discord for these conversions! https://docs.google.com/spreadsheets/d/118jo960co3Mgw8eREFVBsaJ7z0GtKNr52IB4Bz99VTA/edit#gid=0
        private void UpdateExpressions()
        {
            UnifiedTrackingData.LatestEyeData.Left = MakeEye
            (
                LookLeft: expressions[FBExpression.Eyes_Look_Left_L],
                LookRight: expressions[FBExpression.Eyes_Look_Right_L],
                LookUp: expressions[FBExpression.Eyes_Look_Up_L],
                LookDown: expressions[FBExpression.Eyes_Look_Down_L],
                Openness: expressions[FBExpression.Eyes_Closed_L],
                Squeeze: expressions[FBExpression.Lid_Tightener_L],
                Widen: expressions[FBExpression.Upper_Lid_Raiser_L]
            );
            
            UnifiedTrackingData.LatestEyeData.Right = MakeEye
            (
                LookLeft: expressions[FBExpression.Eyes_Look_Left_R],
                LookRight: expressions[FBExpression.Eyes_Look_Right_R],
                LookUp: expressions[FBExpression.Eyes_Look_Up_R],
                LookDown: expressions[FBExpression.Eyes_Look_Down_R],
                Openness: expressions[FBExpression.Eyes_Closed_R],
                Squeeze: expressions[FBExpression.Lid_Tightener_R],
                Widen: expressions[FBExpression.Upper_Lid_Raiser_R]
            );
            
            UnifiedTrackingData.LatestEyeData.Combined = MakeEye
            (
                LookLeft: (expressions[FBExpression.Eyes_Look_Left_L] + expressions[FBExpression.Eyes_Look_Left_R]) / 2,
                LookRight: (expressions[FBExpression.Eyes_Look_Right_L] + expressions[FBExpression.Eyes_Look_Right_R]) / 2,
                LookUp: (expressions[FBExpression.Eyes_Look_Up_L] + expressions[FBExpression.Eyes_Look_Up_R]) / 2,
                LookDown: (expressions[FBExpression.Eyes_Look_Down_L] + expressions[FBExpression.Eyes_Look_Down_R]) / 2,
                Openness: (expressions[FBExpression.Eyes_Closed_L] + expressions[FBExpression.Eyes_Closed_R]) / 2,
                Squeeze: (expressions[FBExpression.Lid_Tightener_L] + expressions[FBExpression.Lid_Tightener_R]) / 2,
                Widen: (expressions[FBExpression.Upper_Lid_Raiser_L] + expressions[FBExpression.Upper_Lid_Raiser_R]) / 2
            );

            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.CheekPuffLeft] = expressions[FBExpression.Cheek_Puff_L];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.CheekPuffRight] = expressions[FBExpression.Cheek_Puff_R];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.CheekSuck] = (expressions[FBExpression.Cheek_Suck_L] + expressions[FBExpression.Cheek_Suck_R]) / 2;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.JawOpen] = expressions[FBExpression.Jaw_Drop];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.JawLeft] = expressions[FBExpression.Mouth_Left];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.JawRight] = expressions[FBExpression.Mouth_Right];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.JawForward] = expressions[FBExpression.Jaw_Thrust];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.MouthPout] = (expressions[FBExpression.Lip_Pucker_L] + expressions[FBExpression.Lip_Pucker_R]) / 2;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.MouthUpperLeft] = expressions[FBExpression.Mouth_Left];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.MouthLowerLeft] = expressions[FBExpression.Mouth_Left];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.MouthUpperRight] = expressions[FBExpression.Mouth_Right];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.MouthLowerRight] = expressions[FBExpression.Mouth_Right];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.MouthSmileLeft] = expressions[FBExpression.Lip_Corner_Puller_L];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.MouthSmileRight] = expressions[FBExpression.Lip_Corner_Puller_R];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.MouthSadLeft] = expressions[FBExpression.Lip_Corner_Depressor_L];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.MouthSadRight] = expressions[FBExpression.Lip_Corner_Depressor_R];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.MouthUpperOverturn] = expressions[FBExpression.Lips_Toward];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.MouthLowerOverturn] = expressions[FBExpression.Lips_Toward];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.MouthUpperUpLeft] = expressions[FBExpression.Upper_Lip_Raiser_L];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.MouthUpperUpRight] = expressions[FBExpression.Upper_Lip_Raiser_R];

            // Possible matches
            //UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.MouthLowerOverlay] = expressions[FBExpression.Chin_Raiser_B];
            //UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.MouthUpperInside] = expressions[FBExpression.Chin_Raiser_B];
            //UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.MouthLowerInside] = expressions[FBExpression.Chin_Raiser_T];
            //UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.MouthLowerDownLeft] = expressions[FBExpression.Lower_Lip_Depressor_L];
            //UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.MouthLowerDownRight] = expressions[FBExpression.Lower_Lip_Depressor_R];

            // Expressions reported by @aero that look the best - would be best to get a side by side comparison
            //LeftEyeX = data[E["EYES_LOOK_RIGHT_L"]] - data[E["EYES_LOOK_LEFT_L"]]
            //RightEyeX = data[E["EYES_LOOK_RIGHT_R"]] - data[E["EYES_LOOK_LEFT_R"]]
            //EyesY = (((data[E["EYES_LOOK_UP_L"]] + data[E["EYES_LOOK_UP_R"]]) / 1.6) - (data[E["EYES_LOOK_DOWN_L"]] + data[E["EYES_LOOK_DOWN_R"]])) / 2
            //LeftEyeLid = data[E["EYES_CLOSED_L"]] + min(data[E["EYES_LOOK_DOWN_L"]], data[E["EYES_LOOK_DOWN_R"]])
            //RightEyeLid = data[E["EYES_CLOSED_R"]] + min(data[E["EYES_LOOK_DOWN_L"]], data[E["EYES_LOOK_DOWN_R"]])
            //JawOpen = data[E["JAW_DROP"]]
            //MouthPout = (data[E["LIP_PUCKER_L"]] + data[E["LIP_PUCKER_R"]]) / 2
            //JawX = (data[E["MOUTH_RIGHT"]] - data[E["MOUTH_LEFT"]]) * 2
            //JawForward = data[E["JAW_THRUST"]]
            //CheekPuffLeft = data[E["CHEEK_PUFF_L"]] * 4
            //CheekPuffRight = data[E["CHEEK_PUFF_R"]] * 4
            //SmileSadLeft = (data[E["DIMPLER_L"]] + (data[E["UPPER_LIP_RAISER_L"]] * 2)) - ((data[E["LOWER_LIP_DEPRESSOR_L"]]) * 4)
            //SmileSadRight = (data[E["DIMPLER_R"]] + (data[E["UPPER_LIP_RAISER_R"]] * 2)) - ((data[E["LOWER_LIP_DEPRESSOR_R"]]) * 4)
        }

        private Eye MakeEye(float LookLeft, float LookRight, float LookUp, float LookDown, float Openness, float Squeeze, float Widen)
        {
            return new Eye()
            {
                Look = new Vector2(LookLeft + LookRight, LookUp + LookDown),
                Openness = Openness,
                Squeeze = Squeeze,
                Widen = Widen
            };
        }
    }
}
