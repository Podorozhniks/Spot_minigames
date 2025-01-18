using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class CharacterMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    private Rigidbody rb;
    private Animator animator;
    private Vector3 movement;

    [Header("Pickup Settings")]
    public KeyCode pickupKey = KeyCode.E;
    private GameObject nearbyItem;
    public List<string> pickedUpItems = new List<string>();

    [Header("Held Cloth Object")]
    public GameObject clothObject;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        rb.freezeRotation = true;
        if (clothObject) clothObject.SetActive(false);
    }

    void Update()
    {
        ProcessInput();
        AnimateMovement();
        if (nearbyItem != null && Input.GetKeyDown(pickupKey))
        {
            PickUpItem(nearbyItem);
        }
        if (animator != null)
        {
            bool isCarryingItem = pickedUpItems.Count > 0;
            animator.SetBool("IsCarryingItem", isCarryingItem);
            if (clothObject) clothObject.SetActive(isCarryingItem);
        }
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    void ProcessInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        movement = new Vector3(horizontal, 0f, vertical).normalized;
    }

    void MovePlayer()
    {
        if (movement.magnitude > 0f)
        {
            Vector3 targetPosition = rb.position + movement * moveSpeed * Time.fixedDeltaTime;
            rb.MovePosition(targetPosition);
            Quaternion targetRotation = Quaternion.LookRotation(movement) * Quaternion.Euler(0, 180, 0);
            rb.MoveRotation(Quaternion.Lerp(transform.rotation, targetRotation, 0.2f));
        }
    }

    void AnimateMovement()
    {
        if (animator != null)
        {
            bool isMoving = movement.magnitude > 0f;
            animator.SetBool("IsMoving", isMoving);
            animator.SetFloat("VelocityX", movement.x);
            animator.SetFloat("VelocityZ", movement.z);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("ClothingItem"))
        {
            nearbyItem = other.gameObject;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject == nearbyItem)
        {
            nearbyItem = null;
        }
    }

    private void PickUpItem(GameObject itemObject)
    {
        string itemID = itemObject.name;
        pickedUpItems.Add(itemID);
        itemObject.SetActive(false);
        nearbyItem = null;
    }
}
