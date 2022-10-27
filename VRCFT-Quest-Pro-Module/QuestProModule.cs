using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ViveSR.anipal.Lip;
using VRCFaceTracking;
using VRCFaceTracking.Params;

namespace VRCFT_Quest_Pro_Module
{
    public class QuestProModule : ExtTrackingModule
    {
        public int listenPort = 10000;

        private UdpClient listener;
        private IPEndPoint groupEP;
        private const int expressionsSize = 63 * 4;
        private float[] expressions = new float[expressionsSize];

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

        private void Update()
        {
            try
            {
                // We receive information from the UDP listener as a byte array 63*4 bytes long, since floats are 32 bits long and we have 63 expressions.
                // We then need to convert this byte array to a float array. Thankfully, this can all be done in a single line of code.
                Buffer.BlockCopy(listener.Receive(ref groupEP), 0, expressions, 0, expressionsSize);
                
                UpdateExpressions();
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
