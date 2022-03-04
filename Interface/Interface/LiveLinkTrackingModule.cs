using BaseX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

// Huge thanks to Dazbme#0001 for this!

namespace NeosLiveLinkIntegration.Interface
{
    // This class contains the overrides for any VRCFT Tracking Data struct functions
    public static class TrackingData
    {
        public static LiveLinkTrackingDataStruct liveLinkTrackingDataStruct;

        // Map the EyeBlink and EyeSquint LiveLink blendshapes to the openness SRanipal blendshape
        public static float eyeCalc(float eyeBlink, float eyeSquint)
        {
            return (float)Math.Pow(0.05 + eyeBlink, 6) + eyeSquint;
        }

        // Map the JawOpen and MouthClose LiveLink blendshapes to the apeShape SRanipal blendshape
        public static float apeCalc(float jawOpen, float mouthClose)
        {
            return (0.05f + jawOpen) * (float)Math.Pow(0.05 + mouthClose, 2);
        }
    }
    public class LiveLinkTrackingModule
    {
        private static CancellationTokenSource _cancellationToken;

        public UdpClient liveLinkConnection;
        public IPEndPoint liveLinkRemoteEndpoint;

        // Starts listening and waits for the first packet to come in to initialize
        public bool2 Initialize()
        {
            UniLog.Log("Initializing Live Link Tracking module");
            _cancellationToken?.Cancel();
            liveLinkConnection = new UdpClient(Constants.Port);
            liveLinkRemoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
            ReadData(liveLinkConnection, liveLinkRemoteEndpoint);
            return new bool2(true, true);
        }

        // Update the face pose every 10ms, this is the same frequency that Pimax and SRanipal use
        public Action GetUpdateThreadFunc()
        {
            _cancellationToken = new CancellationTokenSource();
            return () =>
            {
                while (!_cancellationToken.IsCancellationRequested)
                {
                    Update();
                    Thread.Sleep(10);
                }
            };
        }

        // The update function needs to be defined separately in case the user is running with the --vrcft-nothread launch parameter
        // Currently doing all data processing in this Update function, should probably move into TrackingData
        public void Update()
        {
            LiveLinkTrackingDataStruct? newData = ReadData(liveLinkConnection, liveLinkRemoteEndpoint);
            if (newData is LiveLinkTrackingDataStruct d)
            {
                TrackingData.liveLinkTrackingDataStruct = d;
            }
        }

        // A chance to de-initialize everything. This runs synchronously inside main game thread. Do not touch any Unity objects here.
        public void Teardown()
        {
            _cancellationToken.Cancel();
            _cancellationToken.Dispose();
            liveLinkConnection.Close();
            UniLog.Log("LiveLink Teardown");
        }

        public bool SupportsEye => true;
        public bool SupportsLip => true;

        // Read the data from the LiveLink UDP stream and place it into a LiveLinkTrackingDataStruct
        private LiveLinkTrackingDataStruct? ReadData(UdpClient liveLinkConnection, IPEndPoint liveLinkRemoteEndpoint)
        {
            Dictionary<string, float> values = new Dictionary<string, float>();

            try
            {
                // Grab the packet
                // TODO: This just blocks and waits to receive, are we sure this is the freshest packet?
                Byte[] recieveBytes = liveLinkConnection.Receive(ref liveLinkRemoteEndpoint);

                // There is a bunch of static data at the beginning of the packet, it may be variable length because it includes phone name
                // So grab the last 244 bytes of the packet sent using some Linq magic, since that's where our blendshapes live
                IEnumerable<Byte> trimmedBytes = recieveBytes.Skip(Math.Max(0, recieveBytes.Count() - 244));

                // More Linq magic, this splits our 244 bytes into 61, 4-byte chunks which we can then turn into floats
                List<List<Byte>> chunkedBytes = trimmedBytes
                    .Select((x, i) => new { Index = i, Value = x })
                    .GroupBy(x => x.Index / 4)
                    .Select(x => x.Select(v => v.Value).ToList())
                    .ToList();

                // Process each float in out chunked out list
                foreach (var item in chunkedBytes.Select((value, i) => new { i, value }))
                {
                    // First, reverse the list because the data will be in big endian, then convert it to a float
                    item.value.Reverse();
                    values.Add(Constants.LiveLinkNames[item.i], BitConverter.ToSingle(item.value.ToArray(), 0));
                }
            }
            catch (Exception e)
            {
                UniLog.Log("An exception when receiving data.");
                UniLog.Log(e.ToString());
            }

            // Check that we got all 61 values before we go processing things
            if (values.Count() != 61)
            {
                return null;
            }
            return ProcessData(values);
        }

        // This is all terrible, I am almost certain that there is no need to use relfection for any of this
        private LiveLinkTrackingDataStruct ProcessData(Dictionary<string, float> values)
        {
            LiveLinkTrackingDataStruct processedData = new LiveLinkTrackingDataStruct();

            // For each of the eye tracking blendshapes
            foreach (var field in typeof(LiveLinkTrackingDataEye).GetFields(BindingFlags.Instance |
                                                                            BindingFlags.NonPublic |
                                                                            BindingFlags.Public))
            {
                string leftName = field.Name + "Left";
                string rightName = field.Name + "Right";

                // Values have to be boxed before they're set otherwise it won't actually get written
                object tempLeft = processedData.left_eye;
                object tempRight = processedData.right_eye;
                field.SetValue(tempLeft, values[leftName]);
                field.SetValue(tempRight, values[rightName]);
                processedData.left_eye = (LiveLinkTrackingDataEye)tempLeft;
                processedData.right_eye = (LiveLinkTrackingDataEye)tempRight;
            }

            // For each of the lip tracking blendshapes
            foreach (var field in typeof(LiveLinkTrackingDataLips).GetFields(BindingFlags.Instance |
                                                                            BindingFlags.NonPublic |
                                                                            BindingFlags.Public))
            {
                // Box them and set them
                object temp = processedData.lips;
                field.SetValue(temp, values[field.Name]);
                processedData.lips = (LiveLinkTrackingDataLips)temp;
            }

            // For each of the brow tracking blendshapes
            foreach (var field in typeof(LiveLinkTrackingDataBrow).GetFields(BindingFlags.Instance |
                                                                BindingFlags.NonPublic |
                                                                BindingFlags.Public))
            {
                // Box them and set them
                object temp = processedData.brow;
                field.SetValue(processedData.brow, values[field.Name]);
                processedData.brow = (LiveLinkTrackingDataBrow)temp;
            }

            return processedData;
        }
    }
}