using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static UnityEditor.Experimental.GraphView.GraphView;

public class DressUpManager : MonoBehaviour
{
    //animations in the beginning of the game
    //(wave animation for the mannequin and the tutorial animation)
    public Animator DummyDressUp;
    //public Animator tutorial animation;

    public GameObject player;
    public Transform EndArea;
    public float detectionRadius = 2f;
    public GameObject dummy7;
    public GameObject clothes_on_dummy;
    public GameObject reactionSprite;
    public GameObject Point1;
    public GameObject Point2;
    public Transform player_character;

    //objects fo tutorial 
    public GameObject bear_head;
    public GameObject bear_headD;
    public GameObject controlsSprite;
    public Transform targetPoint1;
    public Transform targetPoint2;
    

    private Vector3 bear_headInitialPosition;
    private Vector3 bear_headDInitialPosition;
    private Vector3 controlsSpriteInitialPosition;
    private bool target1Reached = false;
    private bool target2Reached = false;

    //time the player must stand in the designated area to end the game
    public float endTime = 0f;
    public float requiredTimeInEndArea = 10f;
    public float afterEndTime = 10f;
    private bool gamePlayEnded = false; //variable for when the game play has ended 
    private bool gameEnded = false; //variable for when the minigame is over 

    //tutorial related stuff
    private Rigidbody playerRigidbody; //reference to the player's rigidbody
    public float moveSpeed = 3f;
    public Transform[] tutorialPoints; //tutorial waypoints
    public float targetProximity = 1f; //distance to consider reached
    private int currentStep = 0; //tracks the current tutorial step
    private bool isTutorialActive = true; //tracks if tutorial is active
    private CharacterMovement characterMovement;    //reference to player movement script


    void Start()
    {
        player = GameObject.FindWithTag("Player");
        playerRigidbody = player.GetComponent<Rigidbody>();
        player_character = GameObject.FindWithTag("Player").transform;
        characterMovement = player.GetComponent<CharacterMovement>();

        //disable player movement at the start of the tutorial 
        DisablePlayerControl();

        //stores the initial positions to reset later
        bear_headInitialPosition = bear_head.transform.position;
        bear_headDInitialPosition = bear_headD.transform.position;
        controlsSpriteInitialPosition = controlsSprite.transform.position;

        //ensures initial states
        bear_head.SetActive(true);
        bear_headD.SetActive(false);
        controlsSprite.SetActive(false);
        reactionSprite.SetActive(false);

        //start the intro aniamtions
        StartCoroutine(PlayIntroAnimations());

        
    }

    IEnumerator PlayIntroAnimations()
    {
        DummyDressUp.SetTrigger("Play");
        yield return new WaitForSeconds(DummyDressUp.GetCurrentAnimatorStateInfo(0).length);

        Debug.Log("Intro aniamtions done");
        StartCoroutine(RunTutorial()); //begin the tutorial after the into aniamtion
    }

    IEnumerator RunTutorial()
    {
        Debug.Log("tutorial started");
        isTutorialActive = true;

        while (currentStep < tutorialPoints.Length)
        {
            Vector3 targetPosition = tutorialPoints[currentStep].position;

            while (Vector3.Distance(player.transform.position, targetPosition) > targetProximity)
            {
                Vector3 direction = (targetPosition - player.transform.position).normalized;
                playerRigidbody.MovePosition(player.transform.position + direction * moveSpeed * Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();

               
            }
            Debug.Log($"Reached tutorial point {currentStep + 1}");
            currentStep++;

            // Small delay after reaching each waypoint
            yield return new WaitForSeconds(1f);

        }

        Debug.Log("All tutorial steps completed.");
        EndTutorial();
    }

    void EndTutorial()
    {
        isTutorialActive = false;
        EnablePlayerControl(); // Allow player control after tutorial ends
        Debug.Log("Tutorial ended. Player can now take control.");
    }
        
    


    IEnumerator PlayOutroAnimations()
    {
        dummy7.gameObject.SetActive(false);
        clothes_on_dummy.SetActive(false);

        
        reactionSprite.gameObject.SetActive(true);

        yield return new WaitForSeconds(afterEndTime);

        SceneManager.LoadScene("MinigameHub");
    }



    void Update()
    {
        // Check for end area logic after the tutorial is done
        if (!isTutorialActive && !gameEnded)
        {
            EndAreaLogic();
        }

        // Existing logic for checking target points
        CheckTargetPoints();
    }

    void CheckTargetPoints()
    {
        //checks if player reaches target point 1
        if (!target1Reached && Vector3.Distance(player.transform.position, targetPoint1.position) <= targetProximity)
        {
            target1Reached = true;

            //deactivates object1 and activate object3
            bear_head.SetActive(false);
            controlsSprite.SetActive(true);

            Debug.Log("Target Point 1 reached. Object 1 deactivated, Object 3 activated.");
        }

        //checks if player reaches target point 2
        if (target1Reached && !target2Reached && Vector3.Distance(player.transform.position, targetPoint2.position) <= targetProximity)
        {
            target2Reached = true;

            //activates object2
            bear_headD.SetActive(true);

            Debug.Log("Target Point 2 reached. Object 2 activated.");
        }
    }

    void EndAreaLogic()
    {
        // Increment the timer if the player is within the detection radius of the EndArea
        if (PlayerInEndArea())
        {
            endTime += Time.deltaTime;

            // Check if the player has stayed for the required amount of time
            if (endTime >= requiredTimeInEndArea && !gameEnded)
            {
                gameEnded = true;
                StartCoroutine(PlayOutroAnimations());
                Debug.Log("Player has stayed in the end area for the required time. Game ending...");
            }
        }
        else
        {
            // Reset the timer if the player leaves the area
            endTime = 0f;
        }
    }

    bool PlayerInEndArea()
    {
        float distance = Vector3.Distance(player.transform.position, EndArea.position);
        return distance <= 1f;
    }

    private void DisablePlayerControl()
    {
        //disables player input or movement
        var characterMovement = player.GetComponent<CharacterMovement>();
        if (characterMovement != null)
        {
            characterMovement.EnableMovement(false);
        }
    }

    void EnablePlayerControl()
    {
        //enables player input or movement
        var characterMovement = player.GetComponent<CharacterMovement>();
        if (characterMovement != null)
        {
            characterMovement.EnableMovement(true);
        }

        ResetTutorial();
    }

    public void ResetTutorial()
    {
        //resets objects to their initial states
        bear_head.transform.position = bear_headInitialPosition;
        bear_headD.transform.position = bear_headDInitialPosition;
        controlsSprite.transform.position = controlsSpriteInitialPosition;

        bear_head.SetActive(true);
        bear_headD.SetActive(false);
        controlsSprite.SetActive(false);
        Point1.SetActive(false);
        Point2.SetActive(false);

        //resets flags
        target1Reached = false;
        target2Reached = false;

        Debug.Log("Tutorial reset. Objects and states restored.");
    }
}
