using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class CombinedPlayerController : MonoBehaviour
{
    [Header("General Setup")]
    public SerialController serialController;
    public GameObject[] playerModels;
    public float moveSpeed = 5f;

    private static bool created;
    private Rigidbody rb;
    private Animator animator;
    private Vector3 movement;

    [Header("Pickup / Carrying")]
    public KeyCode pickupKey = KeyCode.E;
    public GameObject clothObject;
    private GameObject nearbyItem;
    public List<string> pickedUpItems = new List<string>();

    void Awake()
    {
        // Ensure this object persists across scenes and isn't duplicated
        if (!created)
        {
            DontDestroyOnLoad(gameObject);
            created = true;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        rb.freezeRotation = true;
        if (clothObject) clothObject.SetActive(false);
        if (playerModels != null && playerModels.Length > 0)
        {
            // Deactivate all models initially
            for (int i = 0; i < playerModels.Length; i++)
                if (playerModels[i]) playerModels[i].SetActive(false);
        }
    }

    void Update()
    {
        ReadNFC();
        ProcessInput();
        AnimateMovement();

        if (nearbyItem != null && Input.GetKeyDown(pickupKey))
        {
            PickUpItem(nearbyItem);
        }
        if (animator)
        {
            bool isCarryingItem = (pickedUpItems.Count > 0);
            animator.SetBool("IsCarryingItem", isCarryingItem);
            if (clothObject) clothObject.SetActive(isCarryingItem);
        }
    }

    void FixedUpdate()
    {
        MovePlayer();
    }

    void ReadNFC()
    {
        if (!serialController) return;
        string message = serialController.ReadSerialMessage();
        if (string.IsNullOrEmpty(message)) return;
        if (ReferenceEquals(message, SerialController.SERIAL_DEVICE_CONNECTED)) return;
        if (ReferenceEquals(message, SerialController.SERIAL_DEVICE_DISCONNECTED)) return;
        int modelIndex;
        if (int.TryParse(message, out modelIndex))
        {
            ChangePlayerModel(modelIndex);
        }
    }

    void ChangePlayerModel(int modelNumber)
    {
        if (playerModels == null || playerModels.Length == 0) return;
        if (modelNumber < 1 || modelNumber > playerModels.Length) return;
        for (int i = 0; i < playerModels.Length; i++)
        {
            if (playerModels[i]) playerModels[i].SetActive(false);
        }
        int index = modelNumber - 1;
        if (playerModels[index]) playerModels[index].SetActive(true);
    }

    void ProcessInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        movement = new Vector3(h, 0f, v).normalized;
    }

    void MovePlayer()
    {
        if (movement.magnitude > 0f)
        {
            Vector3 targetPos = rb.position + movement * moveSpeed * Time.fixedDeltaTime;
            rb.MovePosition(targetPos);
            Quaternion rot = Quaternion.LookRotation(movement) * Quaternion.Euler(0, 180, 0);
            rb.MoveRotation(Quaternion.Lerp(transform.rotation, rot, 0.2f));
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

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("ClothingItem"))
        {
            nearbyItem = other.gameObject;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject == nearbyItem)
        {
            nearbyItem = null;
        }
    }

    void PickUpItem(GameObject itemObject)
    {
        string itemID = itemObject.name;
        pickedUpItems.Add(itemID);
        itemObject.SetActive(false);
        nearbyItem = null;
    }
}
