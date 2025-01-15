using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class PoseHandler : MonoBehaviour
{
    [System.Serializable]
    public class Pose
    {
        public string poseName;
        public List<int> landmarkKeys;
        public List<Vector3> landmarkValues;
        public float tolerance;

        public Pose(string poseName, Dictionary<int, Vector3> landmarks, float tolerance)
        {
            this.poseName = poseName;
            this.landmarkKeys = new List<int>();
            this.landmarkValues = new List<Vector3>();

            if (landmarks != null)
            {
                foreach (var kvp in landmarks)
                {
                    this.landmarkKeys.Add(kvp.Key);
                    this.landmarkValues.Add(kvp.Value);
                }
            }
            else
            {
                Debug.LogError($"Pose '{poseName}' initialized with null landmarks!");
            }

            this.tolerance = tolerance;
        }

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

    public Transform poseBodyParent;
    public float matchTolerance = 0.1f;
    public string saveDirectory = "Assets/Poses/";
    private List<Pose> savedPoses = new List<Pose>();

    private const int LandmarkCount = 33;

    private void Start()
    {
        if (!Directory.Exists(saveDirectory))
            Directory.CreateDirectory(saveDirectory);

        LoadAllSavedPoses();

        Debug.Log($"Loaded {savedPoses.Count} poses into memory.");
        foreach (Pose pose in savedPoses)
        {
            Debug.Log($"Pose '{pose.poseName}' has {pose.landmarkKeys?.Count ?? 0} landmarks.");
        }
    }

    public void RecordPose(string poseName)
    {
        Dictionary<int, Vector3> currentLandmarks = GetCurrentLandmarks();

        if (currentLandmarks == null || currentLandmarks.Count == 0)
        {
            Debug.LogError("Failed to record pose: Landmarks are null or empty!");
            return;
        }

        Pose newPose = new Pose(poseName, currentLandmarks, matchTolerance);
        savedPoses.Add(newPose);
        SavePoseToFile(newPose);

        Debug.Log($"Pose '{poseName}' recorded and saved successfully.");
    }

    private Dictionary<int, Vector3> GetCurrentLandmarks()
    {
        if (poseBodyParent == null)
        {
            Debug.LogError("poseBodyParent is not assigned in the Inspector!");
            return null;
        }

        Dictionary<int, Vector3> landmarks = new Dictionary<int, Vector3>();

        for (int i = 0; i < LandmarkCount; i++)
        {
            Transform landmarkTransform = poseBodyParent.GetChild(i);
            if (landmarkTransform != null)
            {
                landmarks[i] = landmarkTransform.position;
            }
            else
            {
                Debug.LogWarning($"Landmark {i} is missing or null in poseBodyParent.");
            }
        }

        return landmarks;
    }

    private void SavePoseToFile(Pose pose)
    {
        string filePath = Path.Combine(saveDirectory, $"{pose.poseName}.json");
        string json = JsonUtility.ToJson(pose, true);
        File.WriteAllText(filePath, json);
    }

    private void LoadAllSavedPoses()
    {
        string[] poseFiles = Directory.GetFiles(saveDirectory, "*.json");
        foreach (string filePath in poseFiles)
        {
            Pose loadedPose = LoadPoseFromFile(filePath);
            if (loadedPose != null)
            {
                savedPoses.Add(loadedPose);
            }
        }
    }

    private Pose LoadPoseFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"Pose file '{filePath}' does not exist!");
            return null;
        }

        string json = File.ReadAllText(filePath);
        return JsonUtility.FromJson<Pose>(json);
    }
}

