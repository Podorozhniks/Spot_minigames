using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement; // For SceneManager.LoadScene

/// <summary>
/// A robust pose checker / minigame script that:
/// 1) References a PipeServer for MediaPipe data.
/// 2) Loads/saves poses as JSON files.
/// 3) Stores loaded poses in a Dictionary by a canonical name (trimmed + lowercase).
/// 4) Provides a minigame workflow (sequence of required poses).
/// 5) Displays a PNG (Sprite) for each required pose in one UI Image.
/// 6) Uses root-orientation-scale normalization to check the user's pose.
/// 7) After the final pose, shows a separate 'victoryUIImage' with a 'wellDoneSprite' for 'victoryDisplayTime' seconds,
///    then loads a hub scene.
/// </summary>
public class PoseGameFlow : MonoBehaviour
{
    [System.Serializable]
    public class PoseData
    {
        public string poseName;
        public List<int> landmarkKeys;
        public List<Vector3> landmarkValues;
        public float tolerance;  // how close each landmark must be (in meters) to count as "matched"

        public PoseData(string poseName, Dictionary<int, Vector3> landmarks, float tolerance)
        {
            this.poseName = poseName;
            landmarkKeys = new List<int>();
            landmarkValues = new List<Vector3>();

            if (landmarks != null)
            {
                foreach (var kvp in landmarks)
                {
                    landmarkKeys.Add(kvp.Key);
                    landmarkValues.Add(kvp.Value);
                }
            }
            else
            {
                Debug.LogError($"[PoseData] Pose '{poseName}' initialized with null landmarks!");
            }

            this.tolerance = tolerance;
        }

        /// <summary>
        /// Convert stored landmark lists back to a dictionary, keyed by index (0..32).
        /// </summary>
        public Dictionary<int, Vector3> GetLandmarksAsDictionary()
        {
            Dictionary<int, Vector3> dict = new Dictionary<int, Vector3>();
            for (int i = 0; i < landmarkKeys.Count; i++)
            {
                dict[landmarkKeys[i]] = landmarkValues[i];
            }
            return dict;
        }
    }

    [System.Serializable]
    public class PoseGoal
    {
        [Tooltip("Must match the 'poseName' in the saved JSON pose (ignoring case/spaces).")]
        public string poseName;

        [Tooltip("UI image to display (PNG/Sprite) of how the user should pose.")]
        public Sprite poseSprite;

        [Range(0f, 1f)]
        [Tooltip("How close (fraction matched) the user must be to this pose to succeed, e.g. 0.75.")]
        public float requiredSimilarity = 0.75f;
    }

    // -------------------------------------------------------------------------
    //  Inspector Fields
    // -------------------------------------------------------------------------

    [Header("PipeServer Reference")]
    public PipeServer pipeServer;

    [Header("Pose File Settings")]
    public string saveDirectory = "Assets/Poses/";
    public float defaultLandmarkTolerance = 0.15f;

    [Header("Minigame Settings")]
    [Tooltip("List of poses the user must perform in order.")]
    public List<PoseGoal> poseGoals = new List<PoseGoal>();

    [Header("Pose UI Image (for instructions)")]
    [Tooltip("UI Image where we display each required pose.")]
    public Image poseUIImage;

    [Header("Victory UI Image (separate)")]
    [Tooltip("UI Image used ONLY to show the 'Well Done' or 'Good Job' sprite at the end.")]
    public Image victoryUIImage;

    [Tooltip("Sprite to display after the final pose is completed.")]
    public Sprite wellDoneSprite;

    [Tooltip("How many seconds to wait after showing the 'Well Done' sprite before loading the hub scene.")]
    public float victoryDisplayTime = 20f;

    [Header("Hub Scene Settings")]
    [Tooltip("Scene to load once the user sees the 'Well Done' image for victoryDisplayTime seconds.")]
    public string hubSceneName = "HubLevel";

    [Header("Game Flow")]
    public bool autoStartMinigame = true;

    [Header("Debug")]
    [Tooltip("If true, will print debug logs for all major steps.")]
    public bool showDebugLogs = true;

    // -------------------------------------------------------------------------
    //  Private State
    // -------------------------------------------------------------------------

    private const int LandmarkCount = 33; // typical Mediapipe full-body

    /// <summary>
    /// Dictionary of all loaded poses keyed by a canonical name (trimmed + lowercase).
    /// Example: "Pose A" => "pose a".
    /// </summary>
    private Dictionary<string, PoseData> poseMap = new Dictionary<string, PoseData>();

    private int currentPoseIndex = 0;
    private bool minigameActive = false;

    // -------------------------------------------------------------------------
    //  Unity Lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        // Ensure directory exists
        if (!Directory.Exists(saveDirectory))
        {
            if (showDebugLogs) Debug.Log($"[PoseGameFlow] Creating saveDirectory at: {saveDirectory}");
            System.IO.Directory.CreateDirectory(saveDirectory);
        }

        // Load any existing poses from disk
        LoadAllSavedPoses();  // populates poseMap
        if (showDebugLogs)
        {
            Debug.Log($"[PoseGameFlow] Done loading poses. We have {poseMap.Count} unique pose(s) in the dictionary:");
            foreach (var kvp in poseMap)
            {
                Debug.Log($"   Key='{kvp.Key}' => poseName='{kvp.Value.poseName}' (tolerance={kvp.Value.tolerance:F2})");
            }
        }

        // Hide the victory image (if assigned) at the start
        if (victoryUIImage != null)
        {
            victoryUIImage.gameObject.SetActive(false);
        }

        // Start minigame if requested
        if (autoStartMinigame && poseGoals.Count > 0)
        {
            StartMinigame();
        }
        else if (autoStartMinigame)
        {
            if (showDebugLogs)
                Debug.LogWarning("[PoseGameFlow] autoStartMinigame is true, but no poseGoals were assigned!");
        }
    }

    private void Update()
    {
        if (!minigameActive) return;

        // If we still have a valid current pose goal
        if (currentPoseIndex < poseGoals.Count)
        {
            PoseGoal currentGoal = poseGoals[currentPoseIndex];

            // Check how similar we are to the required pose
            float similarity = GetPoseSimilarity(currentGoal.poseName);

            if (showDebugLogs)
            {
                Debug.Log($"[PoseGameFlow] Checking pose '{currentGoal.poseName}' => similarity={similarity:F2}, " +
                          $"required={currentGoal.requiredSimilarity:F2}");
            }

            if (similarity >= currentGoal.requiredSimilarity)
            {
                if (showDebugLogs)
                {
                    Debug.Log($"[PoseGameFlow] Pose '{currentGoal.poseName}' matched at {similarity:P0}!");
                }
                NextPose();
            }
        }
        else
        {
            if (showDebugLogs)
                Debug.Log("[PoseGameFlow] All poses have been matched, but minigameActive is still true?");
        }
    }

    // -------------------------------------------------------------------------
    //  Public Minigame Methods
    // -------------------------------------------------------------------------

    /// <summary>
    /// Starts the pose minigame from the first pose in 'poseGoals'.
    /// </summary>
    public void StartMinigame()
    {
        minigameActive = true;
        currentPoseIndex = 0;

        ShowCurrentPoseUI();

        if (showDebugLogs)
        {
            if (poseGoals.Count == 0)
            {
                Debug.LogWarning("[PoseGameFlow] StartMinigame called, but poseGoals is empty!");
            }
            else
            {
                var firstPoseName = poseGoals[0].poseName;
                Debug.Log($"[PoseGameFlow] Minigame started. Pose #1 => '{firstPoseName}'");
            }
        }
    }

    /// <summary>
    /// Moves to next pose in the sequence (or ends the minigame if done).
    /// </summary>
    private void NextPose()
    {
        currentPoseIndex++;
        if (currentPoseIndex >= poseGoals.Count)
        {
            minigameActive = false;
            if (showDebugLogs)
                Debug.Log("[PoseGameFlow] All poses in the minigame sequence have been matched!");

            // Show the separate "victoryUIImage" with "wellDoneSprite" for victoryDisplayTime, then load hub scene
            if (victoryUIImage != null && wellDoneSprite != null)
            {
                victoryUIImage.gameObject.SetActive(true);
                victoryUIImage.sprite = wellDoneSprite;
            }
            else
            {
                if (showDebugLogs)
                    Debug.LogWarning("[PoseGameFlow] victoryUIImage or wellDoneSprite is not assigned!");
            }

            StartCoroutine(ShowVictoryThenLoad());
        }
        else
        {
            if (showDebugLogs)
            {
                PoseGoal nextGoal = poseGoals[currentPoseIndex];
                Debug.Log($"[PoseGameFlow] NextPose => Pose #{currentPoseIndex + 1}: '{nextGoal.poseName}'");
            }
            ShowCurrentPoseUI();
        }
    }

    /// <summary>
    /// Updates the poseUIImage with the current pose's Sprite (if assigned).
    /// </summary>
    private void ShowCurrentPoseUI()
    {
        if (poseUIImage && currentPoseIndex < poseGoals.Count)
        {
            poseUIImage.sprite = poseGoals[currentPoseIndex].poseSprite;
            if (showDebugLogs)
            {
                Debug.Log($"[PoseGameFlow] Showing pose sprite for: '{poseGoals[currentPoseIndex].poseName}'.");
            }
        }
    }

    /// <summary>
    /// Coroutine: show the 'well done' image for 'victoryDisplayTime' seconds, then load the hub scene.
    /// </summary>
    private System.Collections.IEnumerator ShowVictoryThenLoad()
    {
        // Wait for the desired time 
        yield return new WaitForSeconds(victoryDisplayTime);

        if (showDebugLogs)
            Debug.Log($"[PoseGameFlow] {victoryDisplayTime:F1} seconds have elapsed. Loading hub scene: '{hubSceneName}'");

        SceneManager.LoadScene(hubSceneName);
    }

    // -------------------------------------------------------------------------
    //  Pose Recording / JSON IO
    // -------------------------------------------------------------------------

    /// <summary>
    /// Call this from a UI Button to record & save a new pose from the current body posture.
    /// e.g. PoseGameFlow.RecordPose("MyCoolPose");
    ///
    /// NOTE: We .Trim() and .ToLowerInvariant() the name for robust dictionary keys.
    /// </summary>
    public void RecordPose(string rawPoseName)
    {
        if (showDebugLogs) Debug.Log($"[PoseGameFlow] RecordPose called for '{rawPoseName}'.");

        Dictionary<int, Vector3> current = GetCurrentMediaPipeLandmarks();
        if (current == null || current.Count < LandmarkCount)
        {
            Debug.LogError("[PoseGameFlow] Can't record pose: invalid or incomplete landmarks!");
            return;
        }

        // Clean the name for dictionary usage
        string cleanedName = CanonicalName(rawPoseName);

        // Create a new pose entry and store it
        PoseData newPose = new PoseData(cleanedName, current, defaultLandmarkTolerance);
        // Overwrite poseData name with the cleaned version for consistency
        newPose.poseName = cleanedName;

        // Add or replace in dictionary
        poseMap[cleanedName] = newPose;

        // Write to JSON file
        SavePoseToFile(newPose);

        if (showDebugLogs)
        {
            Debug.Log($"[PoseGameFlow] Pose '{cleanedName}' recorded & saved with tolerance={defaultLandmarkTolerance}.");
        }
    }

    /// <summary>
    /// Writes this pose to a JSON file in saveDirectory.
    /// We'll name the file based on the pose's cleaned name, e.g., 'pose_a.json'.
    /// </summary>
    private void SavePoseToFile(PoseData pose)
    {
        string safeFileName = pose.poseName.Replace(' ', '_');
        string path = Path.Combine(saveDirectory, $"{safeFileName}.json");
        string json = JsonUtility.ToJson(pose, true);
        File.WriteAllText(path, json);

        if (showDebugLogs)
        {
            Debug.Log($"[PoseGameFlow] Wrote JSON for pose '{pose.poseName}' to '{path}'.");
        }
    }

    /// <summary>
    /// Loads all *.json pose files in saveDirectory and stores them in poseMap,
    /// keyed by canonical (trimmed & lowercased) name.
    /// </summary>
    private void LoadAllSavedPoses()
    {
        if (showDebugLogs)
        {
            Debug.Log($"[PoseGameFlow] Loading poses from '{saveDirectory}'...");
        }

        poseMap.Clear(); // reset first, or you can merge

        string[] files = Directory.GetFiles(saveDirectory, "*.json");
        foreach (string file in files)
        {
            PoseData loaded = LoadPoseFromFile(file);
            if (loaded != null)
            {
                // Clean the name
                string key = CanonicalName(loaded.poseName);
                // store or replace in dictionary
                poseMap[key] = loaded;

                if (showDebugLogs)
                {
                    Debug.Log($"[PoseGameFlow] Loaded pose '{loaded.poseName}' from '{file}' => dictionary key='{key}'");
                }
            }
        }
    }

    private PoseData LoadPoseFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            if (showDebugLogs)
                Debug.LogWarning($"[PoseGameFlow] File not found at '{filePath}'!");
            return null;
        }

        string json = File.ReadAllText(filePath);
        try
        {
            PoseData pd = JsonUtility.FromJson<PoseData>(json);
            return pd;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PoseGameFlow] Error parsing JSON at '{filePath}': {e}");
            return null;
        }
    }

    // -------------------------------------------------------------------------
    //  Pose Similarity Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns how similar the user's *current* pose is to the named saved pose (range 0..1).
    /// This method uses root, orientation, & scale normalization:
    /// 
    /// 1) Root both poses at the midpoint of LEFT_HIP (23) and RIGHT_HIP (24).
    /// 2) Align orientation by rotating the user's hip vector to match the reference's.
    /// 3) Scale the user's offsets so hip-width matches the reference's.
    /// 4) Count how many landmarks are within the 'poseTolerance' in meters.
    /// </summary>
    public float GetPoseSimilarity(string rawPoseName)
    {
        // Convert user-provided name to canonical form
        string key = CanonicalName(rawPoseName);

        // Find the pose data in our dictionary
        if (!poseMap.ContainsKey(key))
        {
            if (showDebugLogs)
                Debug.LogWarning($"[PoseGameFlow] No loaded poseData for canonical key '{key}' (original: '{rawPoseName}')!");
            return 0f;
        }

        PoseData data = poseMap[key];

        // Get current 33 landmarks
        Dictionary<int, Vector3> current = GetCurrentMediaPipeLandmarks();
        if (current == null || current.Count < LandmarkCount)
        {
            if (showDebugLogs)
                Debug.LogWarning("[PoseGameFlow] Current landmarks not ready or incomplete!");
            return 0f;
        }

        float similarity = ComputePoseSimilarityFullNormalization(current, data.GetLandmarksAsDictionary(), data.tolerance);

        if (showDebugLogs)
        {
            Debug.Log($"[PoseGameFlow] getPoseSimilarity('{rawPoseName}') => {similarity:F3} " +
                      $"(key: '{key}', poseTolerance={data.tolerance:F2}, full normalization)");
        }

        return similarity;
    }

    /// <summary>
    /// A "full" normalization approach:
    /// 1) We root each pose at the midpoint of leftHip(23) & rightHip(24).
    /// 2) We align orientation so both poses face the same "forward" (hip direction).
    /// 3) We scale the user's pose so the user’s hip-width matches the reference’s hip-width.
    /// 4) Then we count how many landmarks are within 'poseTolerance'.
    /// 
    /// This is more robust if the user stands at a different location, angle, or is smaller/larger.
    /// </summary>
    private float ComputePoseSimilarityFullNormalization(
        Dictionary<int, Vector3> current,
        Dictionary<int, Vector3> reference,
        float poseTolerance
    )
    {
        if (current.Count != reference.Count) return 0f;

        // We'll treat LEFT_HIP=23, RIGHT_HIP=24 for "root" & orientation.
        int leftHip = 23, rightHip = 24;
        if (!reference.ContainsKey(leftHip) || !reference.ContainsKey(rightHip)) return 0f;
        if (!current.ContainsKey(leftHip) || !current.ContainsKey(rightHip)) return 0f;

        // 1) Root translation: subtract the midpoint of hips from all landmarks
        Vector3 refHipMid = (reference[leftHip] + reference[rightHip]) * 0.5f;
        Vector3 curHipMid = (current[leftHip] + current[rightHip]) * 0.5f;

        Dictionary<int, Vector3> refOffsets = new Dictionary<int, Vector3>();
        Dictionary<int, Vector3> curOffsets = new Dictionary<int, Vector3>();

        foreach (var kvp in reference)
        {
            refOffsets[kvp.Key] = kvp.Value - refHipMid;
        }
        foreach (var kvp in current)
        {
            curOffsets[kvp.Key] = kvp.Value - curHipMid;
        }

        // 2) Orientation alignment: rotate the user's offsets so their hips "face" the same direction.
        Vector3 refForward = (refOffsets[rightHip] - refOffsets[leftHip]).normalized;
        Vector3 curForward = (curOffsets[rightHip] - curOffsets[leftHip]).normalized;
        if (refForward.sqrMagnitude > 1e-6f && curForward.sqrMagnitude > 1e-6f)
        {
            Quaternion rot = Quaternion.FromToRotation(curForward, refForward);

            // Apply that rotation to each of current's offsets
            List<int> cKeys = new List<int>(curOffsets.Keys);
            foreach (int idx in cKeys)
            {
                curOffsets[idx] = rot * curOffsets[idx];
            }
        }
        else
        {
            if (showDebugLogs)
                Debug.LogWarning("[PoseGameFlow] Hips overlap or invalid forward vector, skipping orientation alignment.");
        }

        // 3) Scale normalization: match the distance between hips
        float refHipDist = Vector3.Distance(refOffsets[leftHip], refOffsets[rightHip]);
        float curHipDist = Vector3.Distance(curOffsets[leftHip], curOffsets[rightHip]);

        if (refHipDist > 1e-6f && curHipDist > 1e-6f)
        {
            float scaleFactor = refHipDist / curHipDist;

            // scale the user's offsets
            List<int> cKeys2 = new List<int>(curOffsets.Keys);
            foreach (int idx in cKeys2)
            {
                curOffsets[idx] *= scaleFactor;
            }
        }
        else
        {
            if (showDebugLogs)
                Debug.LogWarning("[PoseGameFlow] Could not scale: one of the hip distances is near zero!");
        }

        // 4) Now compare final positions
        int matchedCount = 0;
        float totalDist = 0f;
        int totalLandmarks = reference.Count; // typically 33

        foreach (var kvp in refOffsets)
        {
            int idx = kvp.Key;
            if (!curOffsets.ContainsKey(idx)) continue;

            float dist = Vector3.Distance(curOffsets[idx], refOffsets[idx]);
            totalDist += dist;

            if (dist <= poseTolerance)
            {
                matchedCount++;
            }
        }

        float avgDist = totalDist / totalLandmarks;
        float fractionMatched = (float)matchedCount / totalLandmarks;

        if (showDebugLogs)
        {
            Debug.Log($"[PoseGameFlow] ComputePoseSimilarityFullNormalization => matched={matchedCount}/{totalLandmarks}, " +
                      $"fraction={fractionMatched:F3}, avgDist={avgDist:F3}, tol={poseTolerance:F3}");
        }

        return fractionMatched;
    }

    // -------------------------------------------------------------------------
    //  Getting Landmarks from PipeServer
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gets the 33 world-space landmark positions from your PipeServer's Body.
    /// Returns a Dictionary keyed by index 0..32 with the absolute position of each transform.
    /// </summary>
    private Dictionary<int, Vector3> GetCurrentMediaPipeLandmarks()
    {
        if (!pipeServer)
        {
            if (showDebugLogs)
                Debug.LogWarning("[PoseGameFlow] No PipeServer reference assigned!");
            return null;
        }

        if (pipeServer.body == null)
        {
            if (showDebugLogs)
                Debug.LogWarning("[PoseGameFlow] PipeServer.body is null. Possibly not yet initialized?");
            return null;
        }

        Dictionary<int, Vector3> dict = new Dictionary<int, Vector3>();
        for (int i = 0; i < LandmarkCount; i++)
        {
            var inst = pipeServer.body.instances[i];
            if (inst != null)
            {
                dict[i] = inst.transform.position;
            }
            else
            {
                if (showDebugLogs)
                    Debug.LogWarning($"[PoseGameFlow] Landmark index {i} is null on pipeServer.body.instances!");
            }
        }

        return dict;
    }

    // -------------------------------------------------------------------------
    //  Utility
    // -------------------------------------------------------------------------

    /// <summary>
    /// Canonicalizes a string for dictionary usage: trim + lowerInvariant.
    /// </summary>
    private string CanonicalName(string rawName)
    {
        if (string.IsNullOrEmpty(rawName))
            return string.Empty;
        return rawName.Trim().ToLowerInvariant();
    }
}


