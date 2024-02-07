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

    // For textures and image output
    private Texture2D yPlaneTexture;
    private Texture2D uvPlaneTexture;    
    private byte[] m_outputData;
    
    byte[] m_yPlane;
    byte[] m_uvPlane;


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
        // Release the textures
        yPlaneTexture = null;
        uvPlaneTexture = null;

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

    public void GetTextures(ref Texture2D yPlaneTexture, ref Texture2D uvPlaneTexture)
    {
        yPlaneTexture = this.yPlaneTexture;
        uvPlaneTexture = this.uvPlaneTexture;
    }

    public void SetWidthAndHeight(int width, int height)
    {
        m_width = width;
        m_height = height;
    }

    public int ProcessFrame(byte[] inData, int width, int height)
    {
        // The caller needs to privde the width and height as it may not be the same as the internal buffer

        int submitResult = SubmitInputToDecoder(inData, inData.Length);
        if (submitResult != 0)  // Failed
        {
            return -1;
        }

        // Process output
        // This may not return anything on the first few frames
        
        // Make sure that m_outputData is at least as big as width * height * 3 / 2
        if (m_outputData == null || m_outputData.Length < width * height * 3 / 2)
        {
            m_outputData = new byte[width * height * 3 / 2];
        }
        // Make sure that yPlane and uvPlane are at least as big as we need
        if (m_yPlane == null || m_yPlane.Length < width * height)
        {
            m_yPlane = new byte[width * height];
        }
        if (m_uvPlane == null || m_uvPlane.Length < width * height / 2)         // half the size of Y
        {
            m_uvPlane = new byte[width * height / 2];
        }

        bool getOutputResult = GetOutputFromDecoder(m_outputData, m_outputData.Length);

        if (getOutputResult)
        {
            // Get Y and UV size
            int ySize = width * height;
            int uvSize = width * height / 2;

            System.Buffer.BlockCopy(m_outputData, 0, m_yPlane, 0, ySize);
            System.Buffer.BlockCopy(m_outputData, ySize, m_uvPlane, 0, uvSize);  

            // // Update the textures on the main thread
            MainThreadDispatcher.Enqueue(() =>
             {
                //check size of texture and resize if necessary
                if (yPlaneTexture.width != m_width || yPlaneTexture.height != m_height)
                {
                    yPlaneTexture.Reinitialize(m_width, m_height);
                }
                if (uvPlaneTexture.width != m_width / 2 || uvPlaneTexture.height != m_height / 2)
                {
                    uvPlaneTexture.Reinitialize(m_width / 2, m_height / 2);
                }

                yPlaneTexture.LoadRawTextureData(m_yPlane);
                yPlaneTexture.Apply();

                uvPlaneTexture.LoadRawTextureData(m_uvPlane);
                uvPlaneTexture.Apply();            
            });             
        }
        else
        {
            // Failed to get output - handle error in calling function            
            return -1;
        }

        return 0;
    }

}