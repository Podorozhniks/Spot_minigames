using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f; // Movement speed

    void Update()
    {
        // Get input from WASD or arrow keys
        float moveZ = Input.GetAxis("Horizontal"); // A/D or Left/Right arrows
        float moveX = Input.GetAxis("Vertical");   // W/S or Up/Down arrows

        // Create a movement vector
        Vector3 move = new Vector3(moveX, 0, moveZ) * speed * Time.deltaTime;

        // Apply the movement to the player's position
        transform.Translate(move, Space.World);
    }
}