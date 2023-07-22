using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using UnityEngine;
using System.Collections.Generic;

public class DataReaders : MonoBehaviour
{
    // Communication types
    public enum CommunicationType
    {
        Serial,
        UDP
    }

    // Variables for communication
    private Thread communicationThread;
    private SerialPort serialPort;
    private UdpClient udpClient;
    public ConcurrentQueue<float> dataQueue = new ConcurrentQueue<float>();

    private string serialPortName = "COM3";
    private int baudRate = 115200;
    private int udpPort = 8000;
    [SerializeField]
    public List<CommunicationType> communicationTypesPriority = new List<CommunicationType>() { CommunicationType.Serial, CommunicationType.UDP };
    private CommunicationType currentType;

    // Constants
    private const int retryDelay = 1000;

    // Start is called before the first frame update
    void Start()
    {
        StartCommunication();
    }

    private void StartCommunication()
    {
        // Try to start communication for each type in priority
        foreach (CommunicationType type in communicationTypesPriority)
        {
            currentType = type;
            if (currentType == CommunicationType.Serial)
            {
                StartSerial();
            }
            else if (currentType == CommunicationType.UDP)
            {
                StartUDP();
            }
        }
    }

    // Start Serial communication
    private void StartSerial()
    {
        communicationThread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    serialPort = new SerialPort(serialPortName, baudRate);
                    serialPort.Open();

                    while (true)
                    {
                        string data = serialPort.ReadLine();
                        if (int.TryParse(data.Trim(), out int photoResistorValue))
                        {
                            dataQueue.Enqueue(Mathf.SmoothStep(0, 24, photoResistorValue / 1023.0f));
                        }
                    }
                }
                catch (IOException)
                {
                    Debug.Log("Serial port error. Retrying connection in " + retryDelay / 1000 + " seconds.");
                    Thread.Sleep(retryDelay);
                }
                catch (ThreadAbortException)
                {
                    Debug.Log("Serial thread aborted. Closing connection.");
                    break;
                }
            }
        });
        communicationThread.Start();
    }

    // Start UDP communication
    private void StartUDP()
    {
        communicationThread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    udpClient = new UdpClient(udpPort);
                    IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

                    while (true)
                    {
                        byte[] data = udpClient.Receive(ref anyIP);
                        string text = Encoding.UTF8.GetString(data);
                        if (int.TryParse(text.Trim(), out int photoResistorValue))
                        {
                            dataQueue.Enqueue(Mathf.SmoothStep(0, 24, photoResistorValue / 1023.0f));
                        }
                    }
                }
                catch (SocketException)
                {
                    Debug.Log("UDP error. Retrying connection in " + retryDelay / 1000 + " seconds.");
                    Thread.Sleep(retryDelay);
                }
                catch (ThreadAbortException)
                {
                    Debug.Log("UDP thread aborted. Closing connection.");
                    break;
                }
            }
        });
        communicationThread.Start();
    }

    // OnDisable is called when the MonoBehaviour will be disabled or destroyed
    private void OnDisable()
    {
        // Abort the communication thread
        if (communicationThread != null)
        {
            communicationThread.Abort();
            communicationThread = null;
        }

        // Close the serial port
        if (serialPort != null)
        {
            if (serialPort.IsOpen)
            {
                serialPort.Close();
            }
            serialPort = null;
        }

        // Close the UDP client
        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }
    }
}
