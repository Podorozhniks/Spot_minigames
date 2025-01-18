using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerModelChanger : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] TMP_Text readData;
    [SerializeField] TMP_InputField writeData;
    [SerializeField] Button writeButton;

    [Header("Arduino connection")]
    [SerializeField] SerialController serialController;

    [Header("Player Models")]
    // Drag and drop up to 5 player models in the Inspector
    [SerializeField] private GameObject[] playerModels;

    // Start is called before the first frame update
    void Start()
    {
        if (!serialController)
        {
            Debug.LogError("Property 'serialController' is null. Please assign it in the Inspector.");
        }

        if (!readData)
        {
            Debug.LogError("Property 'readData' is null. Please assign a TMP_Text in the Inspector.");
        }

        if (!writeData)
        {
            Debug.LogError("Property 'writeData' is null. Please assign a TMP_InputField in the Inspector.");
        }

        if (!writeButton)
        {
            Debug.LogError("Property 'writeButton' is null. Please assign a Button in the Inspector.");
        }
        else
        {
            writeButton.onClick.AddListener(WriteData);
        }
    }

    private void OnEnable()
    {
        if (writeButton)
        {
            writeButton.onClick.AddListener(WriteData);
        }
    }

    private void OnDisable()
    {
        if (writeButton)
        {
            writeButton.onClick.RemoveListener(WriteData);
        }
    }

    // Update is called once per frame
    void Update()
    {
        ReadData();
    }

    /// <summary>
    /// Sends the content of the input field to the Arduino via serial.
    /// </summary>
    void WriteData()
    {
        serialController.SendSerialMessage(writeData.text);
        Debug.Log($"Sending string '{writeData.text}' to Arduino on port '{serialController.portName}'");
    }

    /// <summary>
    /// Reads data from the serial port and updates UI / player model accordingly.
    /// </summary>
    void ReadData()
    {
        // Receive data
        string message = serialController.ReadSerialMessage();

        if (message == null)
            return; // no new message

        if (ReferenceEquals(message, SerialController.SERIAL_DEVICE_CONNECTED))
        {
            Debug.Log("Connection established");
        }
        else if (ReferenceEquals(message, SerialController.SERIAL_DEVICE_DISCONNECTED))
        {
            Debug.Log("Connection attempt failed or disconnection detected");
        }
        else
        {
            Debug.Log("Message arrived: " + message);
            readData.text = message; // Show in the UI text

            // Attempt to parse the message into an integer
            if (int.TryParse(message, out int modelIndex))
            {
                // Adjust for array index if the NFC tag is from 1 to 5
                // e.g., NFC = 1 => array index = 0
                //       NFC = 5 => array index = 4
                ChangePlayerModel(modelIndex);
            }
        }
    }

    /// <summary>
    /// Deactivates all player models and activates the one corresponding to modelIndex (1–5).
    /// </summary>
    /// <param name="modelNumber">Number from 1 to 5 read from the NFC</param>
    private void ChangePlayerModel(int modelNumber)
    {
        // Ensure the number is in the 1–5 range 
        // and we have that many models in our array
        if (modelNumber < 1 || modelNumber > playerModels.Length)
        {
            Debug.LogWarning($"modelNumber '{modelNumber}' is out of range or not in [1..{playerModels.Length}].");
            return;
        }

        // First, hide all models
        foreach (var model in playerModels)
        {
            if (model != null)
                model.SetActive(false);
        }

        // Then activate the one that matches the number (subtract 1 to convert to zero-based index)
        int targetIndex = modelNumber - 1;
        if (playerModels[targetIndex] != null)
        {
            playerModels[targetIndex].SetActive(true);
            Debug.Log($"Switched to player model: {playerModels[targetIndex].name}");
        }
        else
        {
            Debug.LogWarning("The model is not assigned in the array or is null.");
        }
    }
}
