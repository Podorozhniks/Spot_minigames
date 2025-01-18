using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IconTrigger : MonoBehaviour
{
    public GameObject[] icons; // Assign your icons in the Inspector

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) // Ensure your player has the "Player" tag
        {
            foreach (GameObject icon in icons)
            {
                icon.SetActive(true); // Show the icons
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            foreach (GameObject icon in icons)
            {
                icon.SetActive(false); // Hide the icons
            }
        }
    }
}