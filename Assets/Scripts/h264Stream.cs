// Class that encapsulates the h264 decoder functionality
// as well as the stream parameters (eg. width, height, textures, etc.)
// Each source of h264 stream should have an instance of this class

// TODO:
//   - The c++ getoutput can return multiple outputs in one frame
//   - This is currently only doing frame in and frame out
//   - this can lead to bloat if frames are comming in faster than the framerate
//   - because the texture update is queued on the main thread
//   - instead should have a queue of frames and update the texture at the framerate
//   - this means that some frames will be dropped if they come in too fast
//  - but they still need to be processed



using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Runtime.InteropServices;
using System;
using System.Net.Sockets;
using System.Threading;

public class h264Stream : MonoBehaviour    
{
    
    #if UNITY_EDITOR
        private const string DllName = "MFh264Decoder.dll";
    #else
        private const string DllName = "MFh264Decoder_ARM64.dll";
    #endif



    // Updated P/Invoke declarations to include the decoder instance
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr CreateDecoder();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ReleaseDecoder(IntPtr decoder);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int InitializeDecoder(IntPtr decoder, int width, int height);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SubmitInputToDecoder(IntPtr decoder, byte[] pInData, int dwInSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool GetOutputFromDecoder(IntPtr decoder, byte[] pOutData, int dwOutSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetFrameWidth(IntPtr decoder);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetFrameHeight(IntPtr decoder);

    private int m_width;
    private int m_height;

    // For textures and image output
    private Texture2D yPlaneTexture;
    private Texture2D uvPlaneTexture;    
    private byte[] m_outputData;
    
    byte[] m_yPlane;
    byte[] m_uvPlane;

    public bool IsInitialized { get; private set; } = false;

     private IntPtr decoderInstance = IntPtr.Zero;

    void Start()
    {

    }

    void OnDestroy()
    {
        if (decoderInstance != IntPtr.Zero)
        {
            ReleaseDecoder(decoderInstance);
            decoderInstance = IntPtr.Zero;
        }
        
        IsInitialized = false;

        // Delete textures
        Destroy(yPlaneTexture);
        Destroy(uvPlaneTexture);     
    }

    public int Initialize(int width, int height)
    {
        m_width = width;
        m_height = height;

        decoderInstance = CreateDecoder();
        if (decoderInstance == IntPtr.Zero)
        {            
            return -1;
        }

        int hr = InitializeDecoder(decoderInstance, width, height);
        if (hr != 0)
        {
            return -1;
        }

        yPlaneTexture = new Texture2D(width, height, TextureFormat.R8, false);
        uvPlaneTexture = new Texture2D(width / 2, height / 2, TextureFormat.RG16, false);

        IsInitialized = true;

        return 0;
    }    

    public int GetWidth()
    {
        if (decoderInstance == IntPtr.Zero) return -1;
        return GetFrameWidth(decoderInstance);
    }

    public int GetHeight()
    {
        if (decoderInstance == IntPtr.Zero) return -1;
        return GetFrameHeight(decoderInstance);
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

        if (decoderInstance == IntPtr.Zero) return -1;

        int submitResult = SubmitInputToDecoder(decoderInstance, inData, inData.Length);
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

        bool getOutputResult = GetOutputFromDecoder(decoderInstance, m_outputData, m_outputData.Length);

        if (getOutputResult)
        {
            // Get Y and UV size
            int ySize = width * height;
            int uvSize = width * height / 2;

            System.Buffer.BlockCopy(m_outputData, 0, m_yPlane, 0, ySize);
            System.Buffer.BlockCopy(m_outputData, ySize, m_uvPlane, 0, uvSize);  


            // TODO: Process all frames, but only output the most recent. 
            // The network controller should dump frames into the decoder as fast as possible


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