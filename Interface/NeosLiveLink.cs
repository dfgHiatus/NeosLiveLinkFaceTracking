using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using BaseX;
using System;
using NeosLiveLinkIntegration.Interface;

namespace NeosLiveLinkIntegration
{
	public class NeosLiveLink : NeosMod
	{
		public override string Name => "Neos-WCFace-Integration";
		public override string Author => "dfgHiatus";
		public override string Version => "1.0.0";
		public override string Link => "https://github.com/dfgHiatus/Neos-Eye-Face-API/";
		public override void OnEngineInit()
		{
			Harmony harmony = new Harmony("net.dfgHiatus.Neos-Eye-Face-API");
			harmony.PatchAll();
		}

		[HarmonyPatch(typeof(InputInterface), MethodType.Constructor)]
		[HarmonyPatch(new[] { typeof(Engine)})]
		public class InputInterfaceCtorPatch
		{
			public static void Postfix(InputInterface __instance)
			{
				try
				{
					GenericInputDevice gen = new GenericInputDevice();
					Debug("Module Name: " + gen.ToString());
					__instance.RegisterInputDriver(gen);
				}
				catch (Exception e)
				{
					Warn("Module failed to initiallize.");
					Warn(e.ToString());
				}
			}
		}
	}

	public class GenericInputDevice : IInputDriver
	{
		public Eyes eyes;
		public Mouth mouth;
		private float Alpha = 2f;
		private float Beta = 2f;
		public int UpdateOrder => 100;
		public static LiveLinkTrackingModule liveLinkTrackingModule;

		[HarmonyPatch(typeof(Engine), "Shutdown")]
		public class ShutdownPatch
		{
			public static bool Prefix()
			{
				UniLog.Log("Closing LiveLink Face Tracking...");
				liveLinkTrackingModule.Teardown();
				UniLog.Log("LiveLink shutdown successful.");
				return true;
			}
		}

		public void CollectDeviceInfos(BaseX.DataTreeList list)
        {
			DataTreeDictionary EyeDataTreeDictionary = new DataTreeDictionary();
			EyeDataTreeDictionary.Add("Name", "LiveLink Eye Tracking");
			EyeDataTreeDictionary.Add("Type", "Eye Tracking");
			EyeDataTreeDictionary.Add("Model", "iOS");
			list.Add(EyeDataTreeDictionary);

			DataTreeDictionary MouthDataTreeDictionary = new DataTreeDictionary();
			MouthDataTreeDictionary.Add("Name", "LiveLink Face Tracking");
			MouthDataTreeDictionary.Add("Type", "Face Tracking");
			MouthDataTreeDictionary.Add("Model", "iOS");
			list.Add(MouthDataTreeDictionary);
		}

		public void RegisterInputs(InputInterface inputInterface)
		{
			liveLinkTrackingModule = new LiveLinkTrackingModule();
			if (liveLinkTrackingModule.Initialize().None())
			{
				UniLog.Log("LiveLinkTracking was not initialized.");
				return;
			}
			liveLinkTrackingModule.GetUpdateThreadFunc();

			eyes = new Eyes(inputInterface, "Generic Eye Tracking");
			mouth = new Mouth(inputInterface, "Generic Mouth Tracking");
		}

		private float3 convertTo3DGaze(float eyeX, float eyeY)
		{
			return new float3(MathX.Tan(Alpha * eyeX),
							  MathX.Tan(Beta * (-1f) * eyeY),
							  1f).Normalized;
		}

		public void UpdateInputs(float deltaTime)
        {
			UpdateEyes();
			UpdateMouth();
		}

		public void UpdateEyes()
        {
			eyes.LeftEye.IsDeviceActive = !Engine.Current.InputInterface.VR_Active;
			eyes.RightEye.IsDeviceActive = !Engine.Current.InputInterface.VR_Active;
			eyes.CombinedEye.IsDeviceActive = !Engine.Current.InputInterface.VR_Active;

			eyes.LeftEye.IsTracking = !Engine.Current.InputInterface.VR_Active;
			eyes.RightEye.IsTracking = !Engine.Current.InputInterface.VR_Active;
			eyes.CombinedEye.IsTracking = !Engine.Current.InputInterface.VR_Active;

			eyes.LeftEye.Direction = new float3(TrackingData.liveLinkTrackingDataStruct.left_eye.EyePitch,
												TrackingData.liveLinkTrackingDataStruct.left_eye.EyeYaw * -1f,
												TrackingData.liveLinkTrackingDataStruct.left_eye.EyeRoll);
			eyes.RightEye.Direction = new float3(TrackingData.liveLinkTrackingDataStruct.right_eye.EyePitch,
												TrackingData.liveLinkTrackingDataStruct.right_eye.EyeYaw * -1f,
												TrackingData.liveLinkTrackingDataStruct.right_eye.EyeRoll);

			eyes.LeftEye.Direction = new float3(TrackingData.liveLinkTrackingDataStruct.getCombined().EyePitch,
												TrackingData.liveLinkTrackingDataStruct.getCombined().EyeYaw * -1f,
												TrackingData.liveLinkTrackingDataStruct.getCombined().EyeRoll);

			// Is In/Out left or right?
/*			eyes.LeftEye.Direction = convertTo3DGaze(TrackingData.liveLinkTrackingDataStruct.left_eye.EyeLookIn - TrackingData.liveLinkTrackingDataStruct.left_eye.EyeLookOut,
													 TrackingData.liveLinkTrackingDataStruct.left_eye.EyeLookUp - TrackingData.liveLinkTrackingDataStruct.left_eye.EyeLookDown);
			eyes.RightEye.Direction = convertTo3DGaze(TrackingData.liveLinkTrackingDataStruct.right_eye.EyeLookIn - TrackingData.liveLinkTrackingDataStruct.right_eye.EyeLookOut,
													  TrackingData.liveLinkTrackingDataStruct.right_eye.EyeLookUp - TrackingData.liveLinkTrackingDataStruct.right_eye.EyeLookDown);
			eyes.CombinedEye.Direction = convertTo3DGaze(TrackingData.liveLinkTrackingDataStruct.getCombined().EyeLookIn - TrackingData.liveLinkTrackingDataStruct.getCombined().EyeLookOut,
														 TrackingData.liveLinkTrackingDataStruct.getCombined().EyeLookUp - TrackingData.liveLinkTrackingDataStruct.getCombined().EyeLookDown);*/

			eyes.LeftEye.Squeeze = TrackingData.liveLinkTrackingDataStruct.left_eye.EyeSquint;
			eyes.RightEye.Squeeze = TrackingData.liveLinkTrackingDataStruct.right_eye.EyeSquint;
			eyes.CombinedEye.Squeeze = TrackingData.liveLinkTrackingDataStruct.getCombined().EyeSquint;

			eyes.LeftEye.Widen = TrackingData.liveLinkTrackingDataStruct.left_eye.EyeWide;
			eyes.RightEye.Widen = TrackingData.liveLinkTrackingDataStruct.right_eye.EyeWide;
			eyes.CombinedEye.Widen = TrackingData.liveLinkTrackingDataStruct.getCombined().EyeWide;

			eyes.LeftEye.Openness = TrackingData.eyeCalc(TrackingData.liveLinkTrackingDataStruct.left_eye.EyeWide, TrackingData.liveLinkTrackingDataStruct.left_eye.EyeSquint);
			eyes.RightEye.Openness = TrackingData.eyeCalc(TrackingData.liveLinkTrackingDataStruct.right_eye.EyeWide, TrackingData.liveLinkTrackingDataStruct.right_eye.EyeSquint);
			eyes.CombinedEye.Openness = TrackingData.eyeCalc(TrackingData.liveLinkTrackingDataStruct.getCombined().EyeWide, TrackingData.liveLinkTrackingDataStruct.getCombined().EyeSquint);
		}

		public void UpdateMouth()
        {
			mouth.IsDeviceActive = !Engine.Current.InputInterface.VR_Active;
			mouth.IsTracking = !Engine.Current.InputInterface.VR_Active;

			mouth.Jaw = new float3(TrackingData.liveLinkTrackingDataStruct.lips.JawRight
				- TrackingData.liveLinkTrackingDataStruct.lips.JawLeft, 
				0,
				TrackingData.liveLinkTrackingDataStruct.lips.JawForward);

			mouth.Tongue = new float3(
				0,
				0,
				TrackingData.liveLinkTrackingDataStruct.lips.TongueOut);

			mouth.JawOpen = TrackingData.liveLinkTrackingDataStruct.lips.JawOpen;
			mouth.MouthPout = (TrackingData.liveLinkTrackingDataStruct.lips.MouthFunnel + 
							   TrackingData.liveLinkTrackingDataStruct.lips.MouthPucker) / 2;

			mouth.LipBottomOverturn = TrackingData.liveLinkTrackingDataStruct.lips.MouthShrugLower;
			mouth.LipTopOverturn = TrackingData.liveLinkTrackingDataStruct.lips.MouthShrugUpper;

			mouth.LipLowerLeftRaise = TrackingData.liveLinkTrackingDataStruct.lips.MouthLeft;
			mouth.LipLowerRightRaise = TrackingData.liveLinkTrackingDataStruct.lips.MouthRight;
			mouth.LipUpperRightRaise = TrackingData.liveLinkTrackingDataStruct.lips.MouthLeft;
			mouth.LipUpperLeftRaise = TrackingData.liveLinkTrackingDataStruct.lips.MouthRight;

/*			mouth.LipLowerLeftRaise = TrackingData.liveLinkTrackingDataStruct.lips.MouthUpperUpLeft ;
			mouth.LipLowerRightRaise = TrackingData.liveLinkTrackingDataStruct.lips.MouthUpperUpRight ;
			mouth.LipUpperRightRaise = TrackingData.liveLinkTrackingDataStruct.lips.MouthUpperUpLeft ;
			mouth.LipUpperLeftRaise = TrackingData.liveLinkTrackingDataStruct.lips.MouthUpperUpRight ;*/

			mouth.MouthRightSmileFrown = TrackingData.liveLinkTrackingDataStruct.lips.MouthSmileRight
									   - TrackingData.liveLinkTrackingDataStruct.lips.MouthFrownRight;
			mouth.MouthLeftSmileFrown = TrackingData.liveLinkTrackingDataStruct.lips.MouthSmileLeft
									  - TrackingData.liveLinkTrackingDataStruct.lips.MouthFrownLeft;
			mouth.CheekLeftPuffSuck = TrackingData.liveLinkTrackingDataStruct.lips.CheekPuff;
			mouth.CheekRightPuffSuck = TrackingData.liveLinkTrackingDataStruct.lips.CheekPuff;
		}
	}
}
