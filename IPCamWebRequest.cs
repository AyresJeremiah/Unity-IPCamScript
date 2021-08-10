using System;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Events;


/**
 * Custom IP Cam WebRequest 
 * 
 * @author Jeremiah Ayres
 * @ver 18MAY2021
 * 
 * This script/class connects to an IP cam so that it could be streamed in to Unity
 * 
 */

public class JPEGDownloaded : UnityEvent<byte[]> { }

public class IPCamWebRequest : DownloadHandlerScript
{
    /** 
     * Public Variables
     * 
     *         OnJpegDownloaded - Used with the invoke call
     *               bufferSize - the size of the array that holds the data going into the ReceiveData method.
     *                            This is set by the constructor of the inherited class DownloadHandlerScript
     * 
     */
    public CustomWebRequest_JpegDownloaded OnJpegDownloaded = new CustomWebRequest_JpegDownloaded() { };
    public int bufferSize;

    /** 
     * Private Variables
     * 
     *         contentLength  -  The length stored in each header for the jpg file.  
     *    bytesFromLastCycle  -  Holds data from the previous cycle to be used when all the data for the jpg is received.
     * indexPacketDataStarts  -  The index at which the data we care about for the image in the byte stream.
     *     dataLengthTotalIn  -  The total data stored in bytesFromLastCycle
     * 
     */
    private int contentLength;
    private byte[] bytesFromLastCycle=new byte[0];
    private int indexPacketDataStarts=0;
    private int dataLengthTotalIn = 0;

    /**
     * *******************************Constructors******************************
     */
    public IPCamWebRequest() : base()
    {
    }
    public IPCamWebRequest(byte[] buffer) : base(buffer)
    {

    }

    /** 
     * Required method(s) for DownloadHandler base class
     */
    protected override byte[] GetData() { return null; }

    /**
     * *****************************Class Methods*****************************
     */

    /**
     * Receive data from the input sream and process it into an image.
     * 
     * @pram
     *      byteFromCamera - byte array of data from camera
     *      dataLength     - index at which the data ends in byteFromCamera
     *      
     * @returns
     *         True if the stream is successful in processing an image
     *         False if byteFromCamera==null || byteFromCamera.Length < 1
     * 
     */
    protected override bool ReceiveData(byte[] byteFromCamera, int dataLength)
    {
        /*
         * If the stream(byteFromCamera) is null or empty
         */
        if (byteFromCamera == null || byteFromCamera.Length < 1)
        {
            Debug.Log("CustomWebRequest :: ReceiveData - received a null/empty buffer");
            return false;
        }

        /*
         * Update variables
         */

       

        byteFromCamera = concatByteWithEndIndex(bytesFromLastCycle, byteFromCamera, dataLength);
        bytesFromLastCycle = new byte[0];
        dataLengthTotalIn += dataLength;

        /*
        * Get the length of the packet's body (The information we want)
        */
        contentLength = FindLength(byteFromCamera);
        
        if (contentLength <= 0)
        {
            /*
             * Bad packet received!
             * Dump packet reset variables
             */
            dataLengthTotalIn = 0;
           
            Debug.Log("Packet error! \n Dumping packet");

            //Restart Receive data
            return true;
        }

        if (dataLengthTotalIn < contentLength+indexPacketDataStarts)
        {
            //Hasnt received all the data yet save to a variable and read more packets
            bytesFromLastCycle = byteFromCamera;
        }
        else
        {
            //Received all the data Continue with processing the image.

            //Parse byte stream
            byteFromCamera = ParseInput(byteFromCamera);

            //Display Image
            OnJpegDownloaded.Invoke(byteFromCamera);

            //Reset Variables
            contentLength = 0;
            dataLengthTotalIn = bytesFromLastCycle.Length;
        }
        
        return true;
    }

    /*
     * Finds the length of the input packet from the packet header.
     * This method also saves the index at which the header ends.
     * 
     * @globalVariablesUsed 
     *      indexPacketDataStarts
     *      
     * @return 
     *       If content length exists it will return it 
     *       Else it returns -1;
     *      
     */
    private int FindLength(byte[] bytesReceived)
    {
        string line = "";
        int result = -1;

        for (int i = 0; i < bytesReceived.Length - 1; i++)
        {
            if (bytesReceived[i] == 13)
            { // CR
                if (line.StartsWith("Content-Length:"))
                {
                    result = Convert.ToInt32(line.Substring(16).Trim());

                    indexPacketDataStarts = i+4; //The plus four is to skip whitespace
                    
                    break;
                }
                else
                {
                    line = "";
                }
                if (bytesReceived[i+1] == 13) // Two blank lines means end of header
                {  
                    break;
                }
            }
            else if(bytesReceived[i] != 10)// Ignore LF char
            {
                line += (char)bytesReceived[i];
            }
        }
        return result;
    }

    /**
     * Parses the input byte stream to only have neccessary data needed. 
     * 
     * @pram 
     *      input - byte array containing jpg
     * 
     * @requires input != null
     * 
     * @updates
     *      bytesFromLastCycle[] - saves bytes at the end of input that are for another jpg.
     *      
     * @globalVariablesUsed 
     *      contentLength
     *      indexPacketDataStarts
     *      bytesFromLastCycle
     *      
     * @returns
     *      parsedBytes[] containing only the jpg
     * 
     */
    private byte[] ParseInput(byte[] input)
    {
        /*
         * Build new array
         */
        byte[] parsedBytes = new byte[contentLength];

        for (int i = 0; i < contentLength ; i++)
        {
            parsedBytes[i] = input[i + indexPacketDataStarts];
        }

        /*
         * Need to save excess into bytesFromLastCycle to use them on the next image.
         */

        bytesFromLastCycle = new byte[input.Length - parsedBytes.Length-indexPacketDataStarts];

        for (int i = 0; i < bytesFromLastCycle.Length; i++)
        {
            bytesFromLastCycle[i] = input[parsedBytes.Length + indexPacketDataStarts + i];
        }

        return parsedBytes;
    }

    /*
     * Concatanate two byte arrays
     * 
     * @pram
     *      stream1 - byte array 1
     *      stream2 - byte array 2
     *      index   - the index to copy to
     * @requires 
     *      stream1 and stream2 != null
     * @return
     *      stream1 + stream2(0,index) = output
     * @ensures 
     *      |stream1| + index = |output|
     */
    private byte[] concatByteWithEndIndex(byte[] stream1, byte[] stream2, int index)
    {
        int stream1Length = stream1.Length;

        byte[] output = new byte[stream1Length + index];

        for (int i = 0; i < stream1Length; i++)
        {
            output[i] = stream1[i];
        }
        for (int i = 0; i < index; i++)
        {
            output[stream1Length + i] = stream2[i];
        }
        return output;
    }
    

}
