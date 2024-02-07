// Class that encapsulates the h264 decoder functionality
// as well as the stream parameters (eg. width, height, textures, etc.)
// Each source of h264 stream should have an instance of this class


using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Runtime.InteropServices;
using System;
using System.Net.Sockets;
using System.Threading;

public class h264Stream 
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

    private int m_width;
    private int m_height;
    private Texture2D yPlaneTexture;
    private Texture2D uvPlaneTexture;

    public int Initialize(int width, int height)
    {
        m_width = width;
        m_height = height;

        // try to init the decoder, return -1 if failed
        try
        {
            int hr = InitializeDecoder(width, height);        
        }
        catch (Exception ex)
        {
            return -1;
        }

        yPlaneTexture = new Texture2D(width, height, TextureFormat.R8, false);
        uvPlaneTexture = new Texture2D(width / 2, height / 2, TextureFormat.RG16, false); // Assuming width and height are even -- UV is half the size of Y

        return 0;
    }    

    public void Release()
    {
        ReleaseDecoder();
    }

    public int GetWidth()
    {
        return GetFrameWidth();     // Returns the width of the internal frame buffer
    }

    public int GetHeight()
    {
        return GetFrameHeight();   // Returns the height of the internal frame buffer
    }

    public void GetYPlaneTexture(ref Texture2D yPlaneTexture)
    {
        yPlaneTexture = this.yPlaneTexture;
    }

    public void GetUVPlaneTexture(ref Texture2D uvPlaneTexture)
    {
        uvPlaneTexture = this.uvPlaneTexture;
    }

    public void SetWidthAndHeight(int width, int height)
    {
        m_width = width;
        m_height = height;
    }

    public int ProcessFrame(byte[] inData)
    {
        int submitResult = SubmitInputToDecoder(inData, inData.Length);
        if (submitResult != 0)  // Failed
        {
            return -1;
        }

        // Process output
        // This may not return anything on the first few frames
        byte[] outputData = new byte[m_width* m_height * 3 / 2];  // NV12 format
        bool getOutputResult = GetOutputFromDecoder(outputData, outputData.Length);

        if (getOutputResult)
        {
            int ySize = m_width * m_height;
            int uvSize = outputData.Length - ySize;

            byte[] yPlane = new byte[ySize];
            byte[] uvPlane = new byte[uvSize];

            System.Buffer.BlockCopy(outputData, 0, yPlane, 0, ySize);
            System.Buffer.BlockCopy(outputData, ySize, uvPlane, 0, uvSize);               
        }
        else
        {
            return -1;
        }

        return 0;
    }

}