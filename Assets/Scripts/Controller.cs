using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Controller : MonoBehaviour
{
    UdpClient client;
    string telloIP = "192.168.10.1";
    int port = 8889;

    bool isFlying = false;


    [Header("UI Elements")]
    public RawImage videoDisplay;

    public TMP_InputField ipInputField;

    public TextMeshProUGUI connectedIPText;

    [Header("Network Settings")]
    public string pythonIP = "192.168.12.222";
    public int pythonPort = 5005;
    public int videoPort = 5006;

    private UdpClient videoReceiver;
    private Thread videoThread;


    private byte[] latestFrameBytes;
    private bool frameReceived = false;
    private Texture2D videoTexture;
    private object lockObject = new object();


    [Header("ControllerButtons")]

    [SerializeField] private OVRInput.Controller rightController = OVRInput.Controller.RTouch;
    [SerializeField] private OVRInput.Controller leftController = OVRInput.Controller.RTouch;
    [SerializeField] private OVRInput.Button takeoffButton = OVRInput.Button.One;
    [SerializeField] private OVRInput.Button landButton = OVRInput.Button.Two;
    [SerializeField] private Vector2 leftAxis;
    [SerializeField] private Vector2 rightAxis;




    void Start()
    {
        


        
    }

    public void ConnectToTello()
    {
        pythonIP = ipInputField.text;
        client = new UdpClient();
        client.Connect(pythonIP, pythonPort);

        videoTexture = new Texture2D(320, 240, TextureFormat.RGB24, false);
        videoDisplay.texture = videoTexture;

        // Obrim el receptor de vídeo en un fil independent de Unity
        videoReceiver = new UdpClient(videoPort);
        videoThread = new Thread(new ThreadStart(ReceiveVideo));
        videoThread.IsBackground = true;
        videoThread.Start();

        connectedIPText.text = $"Connected to: {pythonIP}";

        SendCommand("command"); // modo SDK

    }

    public void Disconnect()
    {
        client.Close();
        if (videoReceiver != null) videoReceiver.Close();
        if (videoThread != null && videoThread.IsAlive) videoThread.Abort();
        connectedIPText.text = $"Disconnected";

    }

    private void ReceiveVideo()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, videoPort);
        while (true)
        {
            try
            {
                byte[] receivedData = videoReceiver.Receive(ref remoteEndPoint);
                lock (lockObject)
                {
                    latestFrameBytes = receivedData;
                    frameReceived = true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Error rebent vídeo: " + e.Message);
            }
        }
    }

    void Update()
    {
        leftAxis = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        rightAxis = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
        //Debug.Log(OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick));
        float lr = leftAxis.x; //Input.GetAxis("Horizontal");
        float fb = leftAxis.y;
        float yaw = rightAxis.x;
        float ud = rightAxis.y;

        // Debug.Log(Input.GetAxis("UpDown"));
        // Debug.Log(Input.GetAxis("UpDown"));

        int ilr = Mathf.RoundToInt(lr * 100);
        int ifb = Mathf.RoundToInt(fb * 100);
        int iud = Mathf.RoundToInt(ud * 100);
        int iyaw = Mathf.RoundToInt(yaw * 100);

        if (isFlying)
        {
            SendCommand($"rc {ilr} {ifb} {iud} {iyaw}");
        }

        if (OVRInput.GetDown(OVRInput.Button.One))
        {
            Debug.LogError("Takeoff button pressed");
            SendCommand("takeoff");
            isFlying = true;
        }

        if (OVRInput.GetDown(OVRInput.Button.Two))
        {
            SendCommand("land");
            isFlying = false;
        }


        if (frameReceived)
        {
            lock (lockObject)
            {
                videoTexture.LoadImage(latestFrameBytes);
                videoTexture.Apply();
                frameReceived = false;
            }
        }
    }

    public void SendCommand(string command)
    {
        try
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(command);
            client.Send(data, data.Length);
        }
        catch (Exception e)
        {
            Debug.LogError("Error enviant ordre: " + e.Message);
        }
    }


        void OnApplicationQuit()
    {
        // Tanquem sockets al sortir
        if (client != null) client.Close();
        if (videoReceiver != null) videoReceiver.Close();
        if (videoThread != null && videoThread.IsAlive) videoThread.Abort();
    }
}
