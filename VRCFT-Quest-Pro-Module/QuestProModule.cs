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
                // We start by receiving the data from the UDP listener.
                // The data is a byte array 63*4 bytes long, since floats are 32 bits long
                Buffer.BlockCopy(listener.Receive(ref groupEP), 0, expressions, 0, expressionsSize);
                UpdateExpressions();

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

        // TODO Test these expressions in game, thanks @Aero on the VRCFT discord for these conversions
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

            UnifiedTrackingData.LatestLipData.LatestShapes[(int) LipShape_v2.CheekPuffLeft] = expressions[FBExpression.Cheek_Puff_L];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int) LipShape_v2.CheekPuffRight] = expressions[FBExpression.Cheek_Puff_R];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.CheekSuck] = (expressions[FBExpression.Cheek_Suck_L] + expressions[FBExpression.Cheek_Suck_R]) / 2;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.JawForward] = expressions[FBExpression.Jaw_Thrust];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.JawLeft] = expressions[FBExpression.Mouth_Left];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.JawRight] = expressions[FBExpression.Mouth_Right];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.JawOpen] = expressions[FBExpression.Jaw_Drop];
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)LipShape_v2.MouthPout] = (expressions[FBExpression.Lip_Pucker_L] + expressions[FBExpression.Lip_Pucker_R]) / 2;
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
