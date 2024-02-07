using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Runtime.InteropServices;
using System;
using System.Net.Sockets;
using System.Threading;

using Stopwatch = System.Diagnostics.Stopwatch;

public class Myh264Player : MonoBehaviour
{

    
    #if UNITY_EDITOR
    private const string DllName = "MFh264Decoder.dll";
    #else
    private const string DllName = "MFh264Decoder_ARM64.dll";
    #endif


    [DllImport(DllName)]
    public extern static int InitializeDecoder(int width, int height);

    [DllImport(DllName)]
    public extern static int SubmitInputToDecoder(byte[] pInData, int dwInSize);

    [DllImport(DllName)]
    public extern static bool GetOutputFromDecoder(byte[] pOutData, int dwOutSize);

    [DllImport(DllName)]
    public extern static int GetFrameWidth();

    [DllImport(DllName)]
    public extern static int GetFrameHeight();

    [DllImport(DllName)]
    public extern static void ReleaseDecoder();

    // Network constants
    private const byte MSG_TYPE_IMAGE = 0;
    private const byte MSG_TYPE_OBJECT = 1;
    private const byte MSG_TYPE_MESH_DATA = 2;
    
    // Network related variables
    private TcpClient client;
    private NetworkStream stream;
    private Thread receiverThread;
    private const int Port = 12345;
    private string ServerIpAddress = "192.168.1.62"; // Replace with your server IP
    private bool continueReceiving = true;

    public int width = 1920;
    public int height = 800;
    public string h264FileName;
    private List<int> nalUnitPositions;
    private byte[] h264Data;
    private Texture2D yPlaneTexture;
    private Texture2D uvPlaneTexture;

    private GameObject videoQuad;

    public DebugLog debugLog;   // For debug logging

    private Dictionary<int, h264Stream> h264Streams = new Dictionary<int, h264Stream>();

    void Start()
    {
        // Start network connection and receiving thread
        client = new TcpClient();
        receiverThread = new Thread(NetworkReceiverThread);
        receiverThread.IsBackground = true;
        receiverThread.Start();

        debugLog.Log("Network receiver thread started");

        // Print out name of the dll
        debugLog.Log("Using DLL: " + DllName);
        // Print out if DLL load was successful
        try
        {
            int hr = InitializeDecoder(width, height);
            debugLog.Log("DLL load successful!");
        }
        catch (Exception ex)
        {
            debugLog.Log("DLL load failed: " + ex.Message);
        }

        Debug.Log("Decoder initialized successfully");
        debugLog.Log("Decoder initialized successfully");

        // Initialize the textures
        yPlaneTexture = new Texture2D(width, height, TextureFormat.R8, false);
        uvPlaneTexture = new Texture2D(width / 2, height / 2, TextureFormat.RG16, false); // Assuming width and height are even -- UV is half the size of Y

        debugLog.Log("Textures initialized successfully");

        // Find the GameObject with the Quad and set the texture
        // Also save the GameObject in a variable so we can use it later
        videoQuad = GameObject.Find("Quad");
        Renderer renderer = videoQuad.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.SetTexture("_YTex", yPlaneTexture);
            renderer.material.SetTexture("_UVTex", uvPlaneTexture);
        }

        debugLog.Log("Textures set to quad successfully");
    }

    private void OnDestroy()
    {
        // Set continueReceiving to false to stop the receiving thread
        continueReceiving = false;
        if (receiverThread != null)
        {
            receiverThread.Join();
        }

        // Close the stream first if it's open
        if (stream != null)
        {
            stream.Close();
            stream = null;
        }

        // Cycle through the streams and release them
        foreach (KeyValuePair<int, h264Stream> entry in h264Streams)
        {
            entry.Value.Release();
        }

        // Then close the TCP client
        if (client != null)
        {
            client.Close();
            client = null;
        }
    }

    private void AddStream(int id, h264Stream stream)
    {
        h264Streams.Add(id, stream);
    }

    void NetworkReceiverThread()
    {
        try
        {
            client.Connect(ServerIpAddress, Port);
            stream = client.GetStream();

            // Print connected to clinet
            Debug.Log("Connected to server");

            while (continueReceiving)
            {
                // All messages header is
                // magic number (2 bytes)
                // message type (1 byte)
                // message size (4 bytes)
                // total 7 bytes

                // Receive magic number
                byte[] magicData = new byte[2];
                int bytesRead = 0;
                while (bytesRead < 2)
                {
                    bytesRead += stream.Read(magicData, bytesRead, 2 - bytesRead);
                }
                ushort magic = BitConverter.ToUInt16(magicData, 0);

                if (magic != 0xABCD)
                {
                    Debug.LogError("Received invalid magic number: " + magic);
                    continue;
                }
                else
                {
                    // print out the magic number
                    //Debug.Log("Received magic number: " + magic);
                }

                // Receive rest of header data
                // have already read 2 bytes for the magic number
                // so read 5, 1 for message type and 4 for message size
                byte[] headerData = new byte[5];
                bytesRead = 0;
                while (bytesRead < 5)
                {
                    bytesRead += stream.Read(headerData, bytesRead, 5 - bytesRead);
                }
                byte msgType = headerData[0];
                int msgSize = (int)BitConverter.ToUInt32(headerData, 1);                

                // // print out the msgType as a binary string
                //Debug.Log("Received message type: " + Convert.ToString(msgType, 2));
                //Debug.Log("Received message size: " + msgSize);

                // Note stream.read requires int, not uint

                //Now accept the rest of the message data based on the given size
                byte[] dataPacket = new byte[msgSize];
                bytesRead = 0;
                while (bytesRead < msgSize)
                {
                    bytesRead += stream.Read(dataPacket, bytesRead, msgSize - bytesRead);
                }

                // Rest should be message type dependant and contained in dataPacket               
                switch (msgType)
                {
                    case MSG_TYPE_IMAGE:
                        DecodeAndProcessFrame(dataPacket, msgSize);
                        break;
                    case MSG_TYPE_OBJECT:
                        Debug.Log("Received object update.");
                        break;
                    case MSG_TYPE_MESH_DATA:
                        //ProcessMeshDataPacket(width, height, channels, imageSize);
                        Debug.Log("Received mesh data. Do Nothing for now.");
                        break;
                    default:
                        Debug.LogError("Unknown message type received: " + Convert.ToString(msgType, 2));
                        break;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Network error: " + e.Message);
        }
        finally
        {
            if (client != null)
            {
                client.Close();
            }
            Debug.Log("Network receiver thread stopped");
        }
    }

    void DecodeAndProcessFrame(byte[] messageData, int messageSize)
    {
        // Network decode     
        byte id = messageData[0];
        int offset = 1;

        ushort hostWidth = ParseUShortFromByteArray(messageData, offset);
        offset += sizeof(ushort);

        ushort hostHeight = ParseUShortFromByteArray(messageData, offset);
        offset += sizeof(ushort);

        // Check if a stream with this id exists
        // if not, then create, initialize with width and height and add to the list
         if (!h264Streams.ContainsKey(id))
         {
            h264Stream stream = new h264Stream();
            // Initialize the decoder with the width and height on the mainthread (as it involves creating textures)
            MainThreadDispatcher.Enqueue(() =>
            {
                stream.Initialize(hostWidth, hostHeight);
            });


            Debug.Log("Stream initialized with id: " + id + ", resolution: " + hostWidth + "x" + hostHeight);
            AddStream(id, stream);  // If successful, add the stream to the list
        }        

        // Get a pointer to the stream
        h264Stream h264Stream = h264Streams[id];                

        // Get the image data from the message
        byte[] imgData = new byte[messageSize - offset];
        Array.Copy(messageData, offset, imgData, 0, messageSize - offset);       
        
        int imageSize = messageSize - offset;
        
        // Now do the decoding

        // // Submit H.264 data to the decoder
        int submitResult = h264Stream.ProcessFrame(imgData);
        if (submitResult != 0)  // Failed
        {
            Debug.LogError("Failed to submit input to decoder");
            return;
        }

        // Now get the textures from the decoder
        h264Stream.GetYPlaneTexture(ref yPlaneTexture);
        h264Stream.GetUVPlaneTexture(ref uvPlaneTexture);

        width = hostWidth;
        height = hostHeight; 
        //Debug.Log("Frame width and height from decoder: " + width + "x" + height);
        
        UpdateTextures();            


        // // Check for frame size change                   
    }

    void UpdateTextures()
    {

        Debug.Log("Textures being updated on main thread");

        // int ySize = width * height;
        // int uvSize = outputBuffer.Length - ySize;

        // byte[] yPlane = new byte[ySize];
        // byte[] uvPlane = new byte[uvSize];

        // System.Buffer.BlockCopy(outputBuffer, 0, yPlane, 0, ySize);
        // System.Buffer.BlockCopy(outputBuffer, ySize, uvPlane, 0, uvSize);        

        // MainThreadDispatcher.Enqueue(() =>
        // {

        //     // Save the output buffer to a file for debugging
        //     //SaveBufferToFile(outputBuffer, "output_stream.nv12");

        //     //check size of texture and resize if necessary
        //     if (yPlaneTexture.width != width || yPlaneTexture.height != height)
        //     {
        //         yPlaneTexture.Reinitialize(width, height);
        //     }
        //     if (uvPlaneTexture.width != width / 2 || uvPlaneTexture.height != height / 2)
        //     {
        //         uvPlaneTexture.Reinitialize(width / 2, height / 2);
        //     }
            
        //     yPlaneTexture.LoadRawTextureData(yPlane);
        //     yPlaneTexture.Apply();

        //     uvPlaneTexture.LoadRawTextureData(uvPlane);
        //     uvPlaneTexture.Apply();

        //     //Debug.Log("Textures updated on main thread");
        // });        
    }
    void SaveBufferToFile(byte[] buffer, string fileName)
    {
        string filePath = Path.Combine(Application.persistentDataPath, fileName);
        try
        {
            File.WriteAllBytes(filePath, buffer);
            Debug.Log("Saved buffer to file: " + filePath);
        }
        catch (Exception ex)
        {
            Debug.LogError("Error saving file: " + ex.Message);
        }
    }

    public static ushort ParseUShortFromByteArray(byte[] data, int startIndex)
    {
        if (data == null || data.Length < startIndex + 2)
            throw new ArgumentException("Invalid data array or startIndex.");

        return (ushort)((data[startIndex] << 8) | data[startIndex + 1]);
    }

    public static float ParseFloatFromByteArray(byte[] data, int startIndex)
    {
        if (data == null || data.Length < startIndex + 4)
            throw new ArgumentException("Invalid data array or startIndex.");

        return BitConverter.ToSingle(data, startIndex);
    }

}

