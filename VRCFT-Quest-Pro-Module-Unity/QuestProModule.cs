using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using VRCFaceTracking;
using VRCFaceTracking.Params;
using VRCFaceTracking.Params.Lip;

namespace VRCFTQuestProModule
{
    public class QuestProModule : ExtTrackingModule
    {
        private MemoryMappedFile MemMapFile;
        private MemoryMappedViewAccessor ViewAccessor;
        private Process CompanionProcess;
        private FBData.AllData FBData;

        public override (bool SupportsEye, bool SupportsLip) Supported => (true, true);

        public override (bool eyeSuccess, bool lipSuccess) Initialize(bool eye, bool lip)
        {
            string modDir = Path.Combine(Utils.PersistentDataDirectory, "CustomLibs");
            string exePath = Path.Combine(modDir, "Meta2.exe");
            if (!File.Exists(exePath))
            {
                Logger.Error("Meta2 executable wasn't found!");
                return (false, false);
            }
            CompanionProcess = new Process();
            CompanionProcess.StartInfo.WorkingDirectory = modDir;
            CompanionProcess.StartInfo.FileName = exePath;
            CompanionProcess.Start();

            for (int i = 0; i < 5; i++)
            {
                try
                {
                    MemMapFile = MemoryMappedFile.OpenExisting("QuestProEyeTracking");
                    ViewAccessor = MemMapFile.CreateViewAccessor();
                    return (true, true);
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine("QuestProEyeTracking mapped file doesn't exist; the companion app probably isn't running");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Could not open the mapped file: " + ex);
                    return (false, false);
                }
                Thread.Sleep(500);
            }

            return (false, false);
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
            if (MemMapFile == null) return;
            ViewAccessor.Read(0, out FBData);
            
            PrepareUpdate();
            UpdateExpressions();
        }


        // Preprocess our expressions per the Meta Documentation
        private void PrepareUpdate()
        {
            FBData.faceData.LidTightenerL = ((
                (FBData.faceData.EyesClosedL +
                    Math.Min(FBData.faceData.EyesLookDownL,
                    FBData.faceData.EyesLookDownR)))); // Temporary
            FBData.faceData.LidTightenerR = ((
                (FBData.faceData.EyesClosedR +
                    Math.Min(FBData.faceData.EyesLookDownL,
                    FBData.faceData.EyesLookDownR)))); // Temporary

            FBData.faceData.EyesClosedL = 0.9f - (FBData.faceData.LidTightenerL * 3);
            FBData.faceData.EyesClosedR = 0.9f - (FBData.faceData.LidTightenerR * 3);

            FBData.faceData.UpperLidRaiserL = Math.Max(0, FBData.faceData.UpperLidRaiserL - 0.5f);
            FBData.faceData.UpperLidRaiserR = Math.Max(0, FBData.faceData.UpperLidRaiserR - 0.5f);

            FBData.faceData.LidTightenerL = Math.Max(0, FBData.faceData.LidTightenerL - 0.5f);
            FBData.faceData.LidTightenerR = Math.Max(0, FBData.faceData.LidTightenerR - 0.5f);
        }

        // Thank you @adjerry on the VRCFT discord for these conversions! https://docs.google.com/spreadsheets/d/118jo960co3Mgw8eREFVBsaJ7z0GtKNr52IB4Bz99VTA/edit#gid=0
        private void UpdateExpressions()
        {
            UnifiedTrackingData.LatestEyeData.Left = MakeEye
            (
                LookLeft: FBData.faceData.EyesLookLeftL,
                LookRight: FBData.faceData.EyesLookRightL,
                LookUp: FBData.faceData.EyesLookUpL,
                LookDown: FBData.faceData.EyesLookDownL,
                Openness: FBData.faceData.EyesClosedL,
                Squint: FBData.faceData.LidTightenerL,
                Squeeze: FBData.faceData.LidTightenerL,
                Widen: FBData.faceData.UpperLidRaiserL,
                InnerUp: FBData.faceData.InnerBrowRaiserL,
                InnerDown: FBData.faceData.BrowLowererL,
                OuterUp: FBData.faceData.OuterBrowRaiserL,
                OuterDown: FBData.faceData.BrowLowererL
            );
            
            UnifiedTrackingData.LatestEyeData.Right = MakeEye
            (
                LookLeft: FBData.faceData.EyesLookLeftR,
                LookRight: FBData.faceData.EyesLookRightR,
                LookUp: FBData.faceData.EyesLookUpR,
                LookDown: FBData.faceData.EyesLookDownR,
                Openness: FBData.faceData.EyesClosedR,
                Squint: FBData.faceData.LidTightenerR,
                Squeeze: FBData.faceData.LidTightenerR,
                Widen: FBData.faceData.UpperLidRaiserR,
                InnerUp: FBData.faceData.InnerBrowRaiserR,
                InnerDown: FBData.faceData.BrowLowererR,
                OuterUp: FBData.faceData.OuterBrowRaiserR,
                OuterDown: FBData.faceData.BrowLowererR
            );
            
            UnifiedTrackingData.LatestEyeData.Combined = MakeEye
            (
                LookLeft: (FBData.faceData.EyesLookLeftL + FBData.faceData.EyesLookLeftR) / 2,
                LookRight: (FBData.faceData.EyesLookRightL + FBData.faceData.EyesLookRightR) / 2,
                LookUp: (FBData.faceData.EyesLookUpL + FBData.faceData.EyesLookUpR) / 2,
                LookDown: (FBData.faceData.EyesLookDownL + FBData.faceData.EyesLookDownR) / 2,
                Openness: (FBData.faceData.EyesClosedL + FBData.faceData.EyesClosedR) / 2,
                Squint: (FBData.faceData.LidTightenerL + FBData.faceData.LidTightenerR) / 2,
                Squeeze: (FBData.faceData.LidTightenerL + FBData.faceData.LidTightenerR) / 2,
                Widen: (FBData.faceData.UpperLidRaiserL + FBData.faceData.UpperLidRaiserR) / 2,
                InnerUp: (FBData.faceData.InnerBrowRaiserL + FBData.faceData.InnerBrowRaiserR) / 2,
                InnerDown: FBData.faceData.BrowLowererL,
                OuterUp: (FBData.faceData.OuterBrowRaiserL + FBData.faceData.OuterBrowRaiserR) / 2,
                OuterDown:FBData.faceData.BrowLowererL
            );

            UnifiedTrackingData.LatestEyeData.EyesDilation = 0.5f;
            UnifiedTrackingData.LatestEyeData.EyesPupilDiameter = 0.0035f;

            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.CheekPuffLeft] = FBData.faceData.CheekPuffL;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.CheekPuffRight] = FBData.faceData.CheekPuffR;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.CheekSuck] = (FBData.faceData.CheekSuckL + FBData.faceData.CheekSuckR) / 2;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.JawOpen] = FBData.faceData.JawDrop;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.JawLeft] = FBData.faceData.MouthLeft;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.JawRight] = FBData.faceData.MouthRight;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.JawForward] = FBData.faceData.JawThrust;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.MouthPout] = (FBData.faceData.LipPuckerL + FBData.faceData.LipPuckerR) / 2;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.MouthUpperLeft] = FBData.faceData.MouthLeft;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.MouthLowerLeft] = FBData.faceData.MouthLeft;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.MouthUpperRight] = FBData.faceData.MouthRight;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.MouthLowerRight] = FBData.faceData.MouthRight;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.MouthSmileLeft] = FBData.faceData.LipCornerPullerL;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.MouthSmileRight] = FBData.faceData.LipCornerPullerR;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.MouthSadLeft] = FBData.faceData.LipCornerDepressorL;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.MouthSadRight] = FBData.faceData.LipCornerDepressorR;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.MouthUpperOverturn] = FBData.faceData.LipsToward;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.MouthLowerOverturn] = FBData.faceData.LipsToward;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.MouthUpperUpLeft] = FBData.faceData.UpperLipRaiserL;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.MouthUpperUpRight] = FBData.faceData.UpperLipRaiserR;
            
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.LipFunnelerLB] = FBData.faceData.LipFunnelerLB;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.LipFunnelerRB] = FBData.faceData.LipFunnelerRB;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.LipFunnelerLT] = FBData.faceData.LipFunnelerLT;
            UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.LipFunnelerRT] = FBData.faceData.LipFunnelerRT;
            
            // Possible matches
            //UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.MouthLowerOverlay = FBData.faceData.ChinRaiserB;
            //UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.MouthUpperInside = FBData.faceData.ChinRaiserB;
            //UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.MouthLowerInside = FBData.faceData.ChinRaiserT;
            //UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.MouthLowerDownLeft = FBData.faceData.LowerLipDepressorL;
            //UnifiedTrackingData.LatestLipData.LatestShapes[(int)UnifiedExpression.MouthLowerDownRight = FBData.faceData.LowerLipDepressorR;

            // Expressions reported by @aero that look the best - would be best to get a side by side comparison
            //LeftEyeX = data[E["EYESLOOKRIGHTL"] - data[E["EYESLOOKLEFTL"]]
            //RightEyeX = data[E["EYESLOOKRIGHTR"] - data[E["EYESLOOKLEFTR"]]
            //EyesY = (((data[E["EYESLOOKUPL"] + data[E["EYESLOOKUPR"]) / 1.6) - (data[E["EYESLOOKDOWNL"] + data[E["EYESLOOKDOWNR"])) / 2
            //LeftEyeLid = data[E["EYESCLOSEDL"] + min(data[E["EYESLOOKDOWNL"], data[E["EYESLOOKDOWNR"])
            //RightEyeLid = data[E["EYESCLOSEDR"] + min(data[E["EYESLOOKDOWNL"], data[E["EYESLOOKDOWNR"])
            //JawOpen = data[E["JAWDROP"]]
            //MouthPout = (data[E["LIPPUCKERL"] + data[E["LIPPUCKERR"]) / 2
            //JawX = (data[E["MOUTHRIGHT"] - data[E["MOUTHLEFT"]) * 2
            //JawForward = data[E["JAWTHRUST"]]
            //CheekPuffLeft = data[E["CHEEKPUFFL"] * 4
            //CheekPuffRight = data[E["CHEEKPUFFR"] * 4
            //SmileSadLeft = (data[E["DIMPLERL"] + (data[E["UPPERLIPRAISERL"] * 2)) - ((data[E["LOWERLIPDEPRESSORL"]) * 4)
            //SmileSadRight = (data[E["DIMPLERR"] + (data[E["UPPERLIPRAISERR"] * 2)) - ((data[E["LOWERLIPDEPRESSORR"]) * 4)
        }

        private Eye MakeEye(float LookLeft, float LookRight, float LookUp, float LookDown, float Openness, float Squint, float Squeeze, float Widen, float InnerUp, float InnerDown, float OuterUp, float OuterDown)
        {
            return new Eye()
            {
                Look = new Vector2(LookRight - LookLeft, LookUp - LookDown),
                Openness = Openness,
                Squeeze = Squeeze,
                Squint = Squint,
                Widen = Widen,
                Brow = new Eye.EyeBrow()
                {
                    InnerUp = InnerUp,
                    InnerDown = InnerDown,
                    OuterUp = OuterUp,
                    OuterDown = OuterDown,
                }
            };
        }
        
        public override void Teardown()
        {
            if (MemMapFile == null) return;
            //memoryGazeData.shutdown = true; // tell the companion app to shut down gracefully but it doesn't work anyway
            ViewAccessor.Write(0, ref FBData);
            MemMapFile.Dispose();
            CompanionProcess.Kill();
        }
    }
}
