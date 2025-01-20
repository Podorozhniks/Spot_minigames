using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A combined script that:
/// 1) References a PipeServer for MediaPipe data.
/// 2) Loads/saves poses as JSON files.
/// 3) Provides a minigame workflow (sequence of required poses).
/// 4) Displays a PNG (Sprite) for each required pose.
/// 5) Has extra debug logs to help trace potential issues.
/// </summary>
public class PoseCheckerMinigame : MonoBehaviour
{
    [System.Serializable]
    public class PoseData
    {
        public string poseName;
        public List<int> landmarkKeys;
        public List<Vector3> landmarkValues;
        public float tolerance;  // how close each landmark must be to count as matched

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
        [Tooltip("Must match the 'poseName' in the saved JSON pose.")]
        public string poseName;

        [Tooltip("UI image to display (PNG/Sprite) of how the user should pose.")]
        public Sprite poseSprite;

        [Range(0f, 1f)]
        [Tooltip("How close the user must be to this pose (e.g. 0.75=75%).")]
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
    public List<PoseGoal> poseGoals = new List<PoseGoal>();
    public Image poseUIImage;

    [Header("Game Flow")]
    public bool autoStartMinigame = true;

    [Header("Debug")]
    [Tooltip("If true, will print debug logs for all major steps.")]
    public bool showDebugLogs = true;

    // -------------------------------------------------------------------------
    //  Private State
    // -------------------------------------------------------------------------

    private const int LandmarkCount = 33; // typical Mediapipe full-body

    private List<PoseData> savedPoses = new List<PoseData>(); // loaded from .json
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
            if (showDebugLogs) Debug.Log($"[PoseCheckerMinigame] Creating saveDirectory at: {saveDirectory}");
            Directory.CreateDirectory(saveDirectory);
        }

        // Load any existing poses from disk
        LoadAllSavedPoses();

        if (showDebugLogs)
            Debug.Log($"[PoseCheckerMinigame] Loaded {savedPoses.Count} poses from '{saveDirectory}'.");

        // Start minigame if requested
        if (autoStartMinigame && poseGoals.Count > 0)
        {
            StartMinigame();
        }
        else if (autoStartMinigame)
        {
            if (showDebugLogs) Debug.LogWarning("[PoseCheckerMinigame] autoStartMinigame is true, but no poseGoals were assigned!");
        }
    }

    private void Update()
    {
        // If the minigame is active and we still have poses to check...
        if (minigameActive && currentPoseIndex < poseGoals.Count)
        {
            PoseGoal currentGoal = poseGoals[currentPoseIndex];
            // We'll check how similar we are to the required pose name
            float similarity = GetPoseSimilarity(currentGoal.poseName);

            if (showDebugLogs)
            {
                Debug.Log($"[PoseCheckerMinigame] Checking pose '{currentGoal.poseName}'." +
                          $" Current similarity: {similarity:F2}," +
                          $" required: {currentGoal.requiredSimilarity:F2}");
            }

            if (similarity >= currentGoal.requiredSimilarity)
            {
                if (showDebugLogs)
                {
                    Debug.Log($"[PoseCheckerMinigame] Pose '{currentGoal.poseName}' matched at {similarity:P0} similarity!");
                }
                NextPose();
            }
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
                Debug.LogWarning("[PoseCheckerMinigame] StartMinigame was called, but poseGoals is empty!");
            }
            else
            {
                Debug.Log($"[PoseCheckerMinigame] Minigame started. " +
                          $"Now performing pose #1: '{poseGoals[currentPoseIndex].poseName}'");
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
                Debug.Log("[PoseCheckerMinigame] All poses in the minigame sequence have been matched!");
            // Fire an event or "win" logic here if desired.
        }
        else
        {
            if (showDebugLogs)
            {
                Debug.Log($"[PoseCheckerMinigame] NextPose -> Pose #{currentPoseIndex + 1}: '{poseGoals[currentPoseIndex].poseName}'");
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
                Debug.Log($"[PoseCheckerMinigame] Showing pose UI sprite for '{poseGoals[currentPoseIndex].poseName}'.");
            }
        }
    }

    // -------------------------------------------------------------------------
    //  Pose Recording / JSON IO
    // -------------------------------------------------------------------------

    /// <summary>
    /// Call this from a UI Button to record & save a new pose from the current body posture.
    /// e.g. PoseCheckerMinigame.RecordPose("MyCoolPose");
    /// </summary>
    public void RecordPose(string poseName)
    {
        if (showDebugLogs) Debug.Log($"[PoseCheckerMinigame] RecordPose called for '{poseName}'.");

        Dictionary<int, Vector3> current = GetCurrentMediaPipeLandmarks();
        if (current == null || current.Count < LandmarkCount)
        {
            Debug.LogError("[PoseCheckerMinigame] Can't record pose: invalid or incomplete landmarks!");
            return;
        }

        // Create a new pose entry and store it
        PoseData newPose = new PoseData(poseName, current, defaultLandmarkTolerance);
        savedPoses.Add(newPose);

        // Write to JSON file
        SavePoseToFile(newPose);

        if (showDebugLogs)
        {
            Debug.Log($"[PoseCheckerMinigame] Pose '{poseName}' recorded & saved to JSON (tolerance={defaultLandmarkTolerance}).");
        }
    }

    /// <summary>
    /// Writes this pose to a JSON file in saveDirectory.
    /// </summary>
    private void SavePoseToFile(PoseData pose)
    {
        string path = Path.Combine(saveDirectory, $"{pose.poseName}.json");
        string json = JsonUtility.ToJson(pose, true);
        File.WriteAllText(path, json);

        if (showDebugLogs)
        {
            Debug.Log($"[PoseCheckerMinigame] Wrote JSON file for pose '{pose.poseName}' to '{path}'.");
        }
    }

    /// <summary>
    /// Loads all *.json pose files in saveDirectory and appends them to 'savedPoses'.
    /// If a pose has the same name as an existing one, we replace it.
    /// </summary>
    private void LoadAllSavedPoses()
    {
        if (showDebugLogs)
        {
            Debug.Log($"[PoseCheckerMinigame] Loading all poses from '{saveDirectory}'...");
        }

        string[] files = Directory.GetFiles(saveDirectory, "*.json");
        foreach (string file in files)
        {
            PoseData loaded = LoadPoseFromFile(file);
            if (loaded != null)
            {
                var existing = savedPoses.Find(p => p.poseName == loaded.poseName);
                if (existing != null) savedPoses.Remove(existing);
                savedPoses.Add(loaded);

                if (showDebugLogs)
                {
                    Debug.Log($"[PoseCheckerMinigame] Loaded pose '{loaded.poseName}' from '{file}'");
                }
            }
        }
    }

    private PoseData LoadPoseFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            if (showDebugLogs)
                Debug.LogWarning($"[PoseCheckerMinigame] File not found at '{filePath}'!");
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
            Debug.LogError($"[PoseCheckerMinigame] Error parsing JSON at '{filePath}': {e}");
            return null;
        }
    }

    // -------------------------------------------------------------------------
    //  Pose Similarity Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns how similar the user's *current* pose is to the named saved pose
    /// (range 0..1). 0 = not similar, 1 = identical (under each landmark's tolerance).
    /// If poseName not found, returns 0.
    /// </summary>
    public float GetPoseSimilarity(string poseName)
    {
        // Find the saved pose data
        PoseData data = savedPoses.Find(p => p.poseName == poseName);
        if (data == null)
        {
            if (showDebugLogs) Debug.LogWarning($"[PoseCheckerMinigame] No saved poseData for name '{poseName}'!");
            return 0f;
        }

        Dictionary<int, Vector3> current = GetCurrentMediaPipeLandmarks();
        if (current == null || current.Count < LandmarkCount)
        {
            if (showDebugLogs) Debug.LogWarning("[PoseCheckerMinigame] Current landmarks not ready or incomplete!");
            return 0f;
        }

        float similarity = ComputePoseSimilarity(current, data.GetLandmarksAsDictionary(), data.tolerance);

        if (showDebugLogs)
        {
            Debug.Log($"[PoseCheckerMinigame] getPoseSimilarity('{poseName}') => {similarity:F3} (requires <= {data.tolerance:F2} per landmark)");
        }

        return similarity;
    }

    /// <summary>
    /// Counts how many landmarks are within 'poseTolerance' distance of the reference,
    /// then returns (count / totalLandmarks) as a fraction 0..1.
    /// </summary>
    private float ComputePoseSimilarity(
        Dictionary<int, Vector3> current,
        Dictionary<int, Vector3> reference,
        float poseTolerance
    )
    {
        if (current.Count != reference.Count) return 0f;
        int matchedCount = 0;

        // Let's also accumulate distances for debug if needed
        float totalDist = 0f;

        foreach (var kvp in reference)
        {
            int idx = kvp.Key;
            if (!current.ContainsKey(idx)) continue;

            float dist = Vector3.Distance(current[idx], kvp.Value);
            totalDist += dist;

            // If distance is within 'poseTolerance', we count it as matched
            if (dist <= poseTolerance)
            {
                matchedCount++;
            }
        }

        float avgDist = totalDist / reference.Count;
        float fractionMatched = (float)matchedCount / reference.Count;

        if (showDebugLogs)
        {
            Debug.Log($"[PoseCheckerMinigame] ComputePoseSimilarity: matched={matchedCount}/{reference.Count}," +
                      $" fraction={fractionMatched:F3}, avgDist={avgDist:F3}, tolerance={poseTolerance:F3}");
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
                Debug.LogWarning("[PoseCheckerMinigame] No PipeServer reference assigned!");
            return null;
        }

        if (pipeServer.body == null)
        {
            if (showDebugLogs)
                Debug.LogWarning("[PoseCheckerMinigame] PipeServer.body is null. Possibly not yet initialized?");
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
                    Debug.Log($"[PoseCheckerMinigame] Landmark index {i} is null on pipeServer.body.instances!");
            }
        }

        return dict;
    }
}


