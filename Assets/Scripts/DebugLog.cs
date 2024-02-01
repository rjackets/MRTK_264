using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // Import TextMeshPro namespace

public class DebugLog : MonoBehaviour
{
    public TMP_Text debugText; // Reference to the TMP_Text component
    private List<string> messages = new List<string>(); // List to store messages

    // Method to add a new message
    public void Log(string message)
    {
        // Add new message to the list
        messages.Add(message);

        // Optional: Limit the number of messages in the list to avoid performance issues
        if (messages.Count > 6) 
        {
            messages.RemoveAt(0); // Remove the oldest message
        }

        // Update the TMP_Text component
        debugText.text = string.Join("\n", messages.ToArray());
    }

    // Optional: Clear all messages
    public void ClearLog()
    {
        messages.Clear();
        debugText.text = "";
    }
}
