using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using VRCFaceTracking;
using VRCFaceTracking.Params;
using VRCFaceTracking.Params.Lip;

namespace VRCFT_Quest_Pro_Module
{
    public class QuestProModule : ExtTrackingModule
    {
        public IPAddress localAddr;
        public int PORT = 13191;
        
        private TcpClient client;
        private NetworkStream stream;
        private bool connected = false;
        
        private const int expressionsSize = 63; 
        private byte[] rawExpressions = new byte[expressionsSize * 4];
        private float[] expressions = new float[expressionsSize];

        public override (bool SupportsEye, bool SupportsLip) Supported => (true, true);

        public override (bool eyeSuccess, bool lipSuccess) Initialize(bool eye, bool lip)
        {
            string configPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "questProIP.txt");
            if (!File.Exists(configPath))
            {
                Logger.Msg("Failed to find config JSON! Please maker sure it is present in the same directory as the DLL.");
                return (false, false);
            }

            string text = File.ReadAllText(configPath).Trim();

            if (!IPAddress.TryParse(text, out localAddr))
            {
                Logger.Error("The IP provided in questProIP.txt is not valid. Please check the file and try again.");
                return (false, false);
            }

            ConnectToTCP();

            Logger.Msg("ALXR handshake successful! Data will be broadcast to VRCFaceTracking.");
            return (true, true);
        }

        private bool ConnectToTCP()
        {
            try
            {
                client = new TcpClient();
                Logger.Msg($"Trying to establish a Quest Pro connection at {localAddr}:{PORT}...");

                client.Connect(localAddr, PORT);
                Logger.Msg("Connected to Quest Pro!");

                stream = client.GetStream();
                connected = true;

                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e.Message);
                return false;
            }
        }

        public override Action GetUpdateThreadFunc()
        {
            return () =>
            {
                while (true)
                {
                    Update();
                    // Thread.Sleep(10); Blocked by IO, no need to sleep
                }
            };
        }

        private void Update()
        {
            try
            {
                // Attempt reconnection if needed
                if (!connected || stream == null)
                {
                    ConnectToTCP();
                }

                // If we fail to reconnect, try again
                if (stream == null)
                {
                    Logger.Warning("No network stream was found! Trying to reconnect...");
                    return;
                }

                if (!stream.CanRead)
                {
                    Logger.Warning("Can't read from network stream just yet! Trying again soon...");
                    return;
                }

                int offset = 0;
                int readBytes;
                do
                {
                    readBytes = stream.Read(rawExpressions, offset, rawExpressions.Length - offset);
                    offset += readBytes;
                } 
                while (readBytes > 0 && offset < rawExpressions.Length);

                if (offset < rawExpressions.Length)
                {
                    // Reconnect to the server if we lose connection
                    Logger.Warning("End of stream! Reconnecting...");
                    connected = false;
                    try
                    {
                        stream.Close();
                    }
                    catch (SocketException e) 
                    {
                        Logger.Error(e.Message);
                        Thread.Sleep(1000);
                    }
                }

                // We receive information from the stream as a byte array 63*4 bytes long, since floats are 32 bits long and we have 63 expressions.
                // We then need to convert this byte array to a float array. Thankfully, this can all be done in a single line of code.
                Buffer.BlockCopy(rawExpressions, 0, expressions, 0, expressionsSize * 4);

                PrepareUpdate();
                UpdateExpressions();
            }
            catch (Exception e)
            {
                Logger.Error(e.Message);
                Thread.Sleep(1000);
            }
        }


        // Preprocess our expressions per the Meta Documentation
        private void PrepareUpdate()
        {
            expressions[FBExpression.Lid_Tightener_L] = ((
                (expressions[FBExpression.Eyes_Closed_L] +
                    Math.Min(expressions[FBExpression.Eyes_Look_Down_L],
                    expressions[FBExpression.Eyes_Look_Down_R])))); // Temporary
            expressions[FBExpression.Lid_Tightener_R] = ((
                (expressions[FBExpression.Eyes_Closed_R] +
                    Math.Min(expressions[FBExpression.Eyes_Look_Down_L],
                    expressions[FBExpression.Eyes_Look_Down_R])))); // Temporary

            expressions[FBExpression.Eyes_Closed_L] = 0.9f - (expressions[FBExpression.Lid_Tightener_L] * 3);
            expressions[FBExpression.Eyes_Closed_R] = 0.9f - (expressions[FBExpression.Lid_Tightener_R] * 3);

            expressions[FBExpression.Upper_Lid_Raiser_L] = Math.Max(0, expressions[FBExpression.Upper_Lid_Raiser_L] - 0.5f);
            expressions[FBExpression.Upper_Lid_Raiser_R] = Math.Max(0, expressions[FBExpression.Upper_Lid_Raiser_R] - 0.5f);

            expressions[FBExpression.Lid_Tightener_L] = Math.Max(0, expressions[FBExpression.Lid_Tightener_L] - 0.5f);
            expressions[FBExpression.Lid_Tightener_R] = Math.Max(0, expressions[FBExpression.Lid_Tightener_R] - 0.5f);

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

            UnifiedTrackingData.LatestEyeData.EyesDilation = 0.5f;
            UnifiedTrackingData.LatestEyeData.EyesPupilDiameter = 0.0035f;

            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.CheekPuffLeft] = expressions[FBExpression.Cheek_Puff_L];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.CheekPuffRight] = expressions[FBExpression.Cheek_Puff_R];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.CheekSuck] = (expressions[FBExpression.Cheek_Suck_L] + expressions[FBExpression.Cheek_Suck_R]) / 2;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.JawOpen] = expressions[FBExpression.Jaw_Drop];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.JawLeft] = expressions[FBExpression.Mouth_Left];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.JawRight] = expressions[FBExpression.Mouth_Right];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.JawForward] = expressions[FBExpression.Jaw_Thrust];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.MouthPout] = (expressions[FBExpression.Lip_Pucker_L] + expressions[FBExpression.Lip_Pucker_R]) / 2;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.MouthUpperLeft] = expressions[FBExpression.Mouth_Left];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.MouthLowerLeft] = expressions[FBExpression.Mouth_Left];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.MouthUpperRight] = expressions[FBExpression.Mouth_Right];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.MouthLowerRight] = expressions[FBExpression.Mouth_Right];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.MouthSmileLeft] = expressions[FBExpression.Lip_Corner_Puller_L];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.MouthSmileRight] = expressions[FBExpression.Lip_Corner_Puller_R];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.MouthSadLeft] = expressions[FBExpression.Lip_Corner_Depressor_L];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.MouthSadRight] = expressions[FBExpression.Lip_Corner_Depressor_R];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.MouthUpperOverturn] = expressions[FBExpression.Lips_Toward];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.MouthLowerOverturn] = expressions[FBExpression.Lips_Toward];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.MouthUpperUpLeft] = expressions[FBExpression.Upper_Lip_Raiser_L];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.MouthUpperUpRight] = expressions[FBExpression.Upper_Lip_Raiser_R];

            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.Lips_Toward] = expressions[FBExpression.Lips_Toward];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.Lip_Funneler_LB] = expressions[FBExpression.Lip_Funneler_LB];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.Lip_Funneler_RB] = expressions[FBExpression.Lip_Funneler_RB];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.Lip_Funneler_LT] = expressions[FBExpression.Lip_Funneler_LT];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.Lip_Funneler_RT] = expressions[FBExpression.Lip_Funneler_RT];
            
            // Possible matches
            //UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.MouthLowerOverlay] = expressions[FBExpression.Chin_Raiser_B];
            //UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.MouthUpperInside] = expressions[FBExpression.Chin_Raiser_B];
            //UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.MouthLowerInside] = expressions[FBExpression.Chin_Raiser_T];
            //UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.MouthLowerDownLeft] = expressions[FBExpression.Lower_Lip_Depressor_L];
            //UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v3.MouthLowerDownRight] = expressions[FBExpression.Lower_Lip_Depressor_R];

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
                Look = new Vector2(LookRight - LookLeft, LookUp - LookDown),
                Openness = Openness,
                Squeeze = Squeeze,
                Widen = Widen
            };
        }
        
        public override void Teardown()
        {
            stream.Close();
            stream.Dispose();
            client.Close();
            client.Dispose();
        }
    }
}
