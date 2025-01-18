using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NFC_Communication : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] TMP_Text readData;
    [SerializeField] TMP_InputField writeData;
    [SerializeField] Button writeButton;

    [Header("Arduino connection")]
    [SerializeField] SerialController serialController;

    // Start is called before the first frame update
    void Start()
    {
        if(!serialController)
        {
            Debug.LogError($"property 'serialController' is null. Connect a serial controller in the inspector.");
        }

        if (!readData)
        {
            Debug.LogError($"property 'readData' is null. Connect a text field in the inspector to display the data on the NFC tag.");
        }

        if (!writeData)
        {
            Debug.LogError($"property 'writeData' is null. Connect an input field in the inspector to type data to write on the NFC tag.");
        }

        if (!writeButton)
        {
            Debug.LogError($"property 'writeButton' is null. Connect a button in the inspector to send data to the Arduino to write to the NFC tag.");
        }

        writeButton.onClick.AddListener(WriteData);
    }

    private void OnEnable()
    {
        if (!writeButton)
        {
            Debug.LogError($"property 'writeButton' is null. Connect a button in the inspector to send data to the Arduino to write to the NFC tag.");
        }

        writeButton.onClick.AddListener(WriteData);
    }

    private void OnDisable()
    {
        writeButton.onClick.RemoveListener(WriteData);
    }

    // Update is called once per frame
    void Update()
    {
        ReadData();
    }

    void WriteData()
    {
        serialController.SendSerialMessage(writeData.text);
        Debug.Log($"Sending string '{writeData.text}' to Arduino on port '{serialController.portName}'");
    }

    void ReadData()
    {
        //---------------------------------------------------------------------
        // Receive data
        //---------------------------------------------------------------------

        string message = serialController.ReadSerialMessage();

        if (message == null)
            return;

        // Check if the message is plain data or a connect/disconnect event.
        if (ReferenceEquals(message, SerialController.SERIAL_DEVICE_CONNECTED))
            Debug.Log("Connection established");
        else if (ReferenceEquals(message, SerialController.SERIAL_DEVICE_DISCONNECTED))
            Debug.Log("Connection attempt failed or disconnection detected");
        else
        {
            Debug.Log("Message arrived: " + message);
            readData.text = message;
        }
    }
}
