using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class CharacterMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    private Rigidbody rb;
    private Animator animator;
    private Vector3 movement;

    public KeyCode pickupKey = KeyCode.E;
    public KeyCode placeKey = KeyCode.R;

    // The item currently in range to pick up
    private ClothingItem nearbyClothingItem;

    // Mannequin reference (when in range)
    private GameObject nearbyMannequin;

    // Track picked-up items by their IDs
    public List<string> pickedUpItems = new List<string>();

    // Optional cloth object in the player's hands
    public GameObject clothObject;

    // This maps an item ID to a mannequin mesh GameObject.
    // Assign these in the Inspector, matching IDs to the mannequin's disabled clothes meshes.
    [System.Serializable]
    public class MannequinClothes
    {
        public string itemID;
        public GameObject mannequinMesh;
    }
    public MannequinClothes[] mannequinClothes;

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

        // Pickup logic
        if (nearbyClothingItem != null && Input.GetKeyDown(pickupKey))
        {
            PickUpClothing(nearbyClothingItem);
        }

        // Placing items on mannequin
        if (nearbyMannequin != null && Input.GetKeyDown(placeKey))
        {
            PlaceClothesOnMannequin();
        }

        // Update "IsCarryingItem" parameter and cloth object
        if (animator)
        {
            bool isCarrying = (pickedUpItems.Count > 0);
            animator.SetBool("IsCarryingItem", isCarrying);
            if (clothObject) clothObject.SetActive(isCarrying);
        }
    }

    void FixedUpdate()
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
        if (!animator) return;
        bool isMoving = (movement.magnitude > 0f);
        animator.SetBool("IsMoving", isMoving);
        animator.SetFloat("VelocityX", movement.x);
        animator.SetFloat("VelocityZ", movement.z);
    }

    // Detect clothing or mannequin
    private void OnTriggerEnter(Collider other)
    {
        // Check if it's clothing
        ClothingItem item = other.GetComponent<ClothingItem>();
        if (item != null)
        {
            nearbyClothingItem = item;
            return;
        }

        // Check if it's the mannequin (by tag or a "Mannequin" script)
        if (other.CompareTag("Mannequin"))
        {
            nearbyMannequin = other.gameObject;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (nearbyClothingItem != null && other.gameObject == nearbyClothingItem.gameObject)
        {
            nearbyClothingItem = null;
        }

        if (nearbyMannequin != null && other.gameObject == nearbyMannequin)
        {
            nearbyMannequin = null;
        }
    }

    void PickUpClothing(ClothingItem item)
    {
        if (!pickedUpItems.Contains(item.itemID))
        {
            pickedUpItems.Add(item.itemID);
            item.gameObject.SetActive(false);
        }
        nearbyClothingItem = null;
    }

    void PlaceClothesOnMannequin()
    {
        // For each item ID the player has, see if there's a matching mannequin mesh to enable
        foreach (var id in pickedUpItems)
        {
            foreach (var mc in mannequinClothes)
            {
                if (mc.itemID == id && mc.mannequinMesh != null)
                {
                    mc.mannequinMesh.SetActive(true);
                }
            }
        }
        // Optionally: if you want to clear the items from the player's inventory:
        // pickedUpItems.Clear();
    }
}
