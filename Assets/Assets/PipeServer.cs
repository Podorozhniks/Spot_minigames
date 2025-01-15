using System.Collections;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;

/*
 * PipeServer.cs
 * 
 * Extended version that:
 *  1) Reads 33 body landmarks from a named pipe ("UnityMediaPipeBody").
 *  2) Spawns debug spheres & lines for each Mediapipe landmark (unchanged).
 *  3) Drives a rig's bones (rigBones array) with those landmarks (basic position matching).
 *  4) Computes a "Chest" from shoulders, with chestOffset.
 *  5) Positions a "Head" bone by blending between Mediapipe's ear midpoint and the chest.
 *  6) Keeps line logic intact.
 *  7) **NEW**: Rotates the head (with an offset so it doesn't look down) 
 *     and in-between arm bones (so they don't go vertical when arms are extended).
 */

public class PipeServer : MonoBehaviour
{
    // ================
    //  RIG BONES
    // ================

    [Header("Rig Bones (index = Mediapipe landmark #)")]
    public Transform[] rigBones = new Transform[33];

    // ===============================
    //  CHEST & HEAD (Already in Code)
    // ===============================

    [Header("Chest Bone Computation")]
    public Transform chestBone;
    public Vector3 chestOffset = new Vector3(0f, 0.1f, 0f);

    [Header("Head Bone Computation")]
    public Transform headBone;
    [Range(0f, 1f)]
    public float headBlend = 0.7f;
    public Vector3 headOffset = new Vector3(0f, 0.2f, 0f);
    public bool rotateHead = false;

    // =========================
    //   IN-BETWEEN BONES
    // =========================

    [Header("Torso/Hips/Arms/Legs")]
    public Transform hipsBone;
    public Transform torsoBone;

    public Transform forearmRightBone;
    public Transform forearmLeftBone;
    public Transform upperRightArmBone;
    public Transform upperLeftArmBone;

    public Transform upperLegRightBone;
    public Transform lowerLegRightBone;
    public Transform upperLegLeftBone;
    public Transform lowerLegLeftBone;

    // =========================
    //   ROTATION OFFSETS
    // =========================

    [Header("Rotation Settings")]
    [Tooltip("If true, we attempt to rotate arms/forearms as well.")]
    public bool rotateArms = true;

    [Tooltip("Offset to fix the default head orientation (e.g. local +Z = forward).\nTry small adjustments like (-90,0,0) or (0,90,0).")]
    public Vector3 headRotationOffsetEuler = new Vector3(-90, 0, 0);

    [Tooltip("Offset for upper arms if they appear vertical when extended.\nTry small angles like (0,90,0).")]
    public Vector3 armRotationOffsetEuler = new Vector3(0, 90, 0);

    [Tooltip("Offset for forearms if needed.\nOften similar to armRotationOffsetEuler, e.g. (0,90,0).")]
    public Vector3 forearmRotationOffsetEuler = new Vector3(0, 90, 0);

    // ================================
    //   ORIGINAL FIELDS (Unmodified)
    // ================================

    public Transform parent;
    public GameObject landmarkPrefab;
    public GameObject linePrefab;
    public GameObject headPrefab;
    public bool anchoredBody = false;
    public bool enableHead = true;
    public float multiplier = 10f;
    public float landmarkScale = 1f;
    public float maxSpeed = 50f;
    public int samplesForPose = 1;

    public Body body;  // The "Body" instance that handles logic for 33 landmarks

    private NamedPipeServerStream server;

    const int LANDMARK_COUNT = 33;
    const int LINES_COUNT = 11;

    // =========================
    //        MONOBEHAVIOUR
    // =========================

    private void Start()
    {
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture =
            System.Globalization.CultureInfo.InvariantCulture;

        // Create a new Body instance for the 33 body landmarks
        body = new Body(parent, landmarkPrefab, linePrefab, landmarkScale, enableHead ? headPrefab : null);

        // Start the named pipe listener on a background thread
        Thread t = new Thread(new ThreadStart(Run));
        t.Start();
    }

    private void Update()
    {
        // Each frame, update the Body with the latest landmark positions
        UpdateBody(body);
    }

    private void OnDisable()
    {
        print("Client disconnected.");
        if (server != null)
        {
            server.Close();
            server.Dispose();
        }
    }

    // ===================================
    //      PIPE READING (Background)
    // ===================================

    private void Run()
    {
        System.Globalization.CultureInfo.CurrentCulture =
            System.Globalization.CultureInfo.InvariantCulture;

        server = new NamedPipeServerStream("UnityMediaPipeBody",
                                           PipeDirection.InOut,
                                           99,
                                           PipeTransmissionMode.Message);

        print("Waiting for connection...");
        server.WaitForConnection();
        print("Connected.");

        var br = new BinaryReader(server, Encoding.UTF8);

        while (true)
        {
            try
            {
                Body b = body;

                var len = (int)br.ReadUInt32();
                var str = new string(br.ReadChars(len));

                string[] lines = str.Split('\n');
                foreach (string l in lines)
                {
                    if (string.IsNullOrWhiteSpace(l))
                        continue;

                    string[] s = l.Split('|');
                    if (s.Length < 5) continue;
                    if (anchoredBody && s[0] != "ANCHORED") continue;
                    if (!anchoredBody && s[0] != "FREE") continue;

                    if (!int.TryParse(s[1], out int i)) continue;
                    float x = float.Parse(s[2]);
                    float y = float.Parse(s[3]);
                    float z = float.Parse(s[4]);

                    b.positionsBuffer[i].value += new Vector3(x, y, z);
                    b.positionsBuffer[i].accumulatedValuesCount += 1;
                    b.active = true;
                }
            }
            catch (EndOfStreamException)
            {
                break;
            }
        }
    }

    // =====================================
    //   MAIN LOGIC: UPDATING THE "BODY"
    // =====================================

    private void UpdateBody(Body b)
    {
        if (!b.active)
            return;

        // 1. Accumulate & average positions
        for (int i = 0; i < LANDMARK_COUNT; ++i)
        {
            if (b.positionsBuffer[i].accumulatedValuesCount < samplesForPose)
                continue;

            b.localPositionTargets[i] =
                (b.positionsBuffer[i].value / b.positionsBuffer[i].accumulatedValuesCount)
                * multiplier;

            b.positionsBuffer[i] = new AccumulatedBuffer(Vector3.zero, 0);
        }

        // 2. Calibrate once
        if (!b.setCalibration)
        {
            print("Set Calibration Data");
            b.Calibrate();
        }

        // 3. Move the debug spheres
        for (int i = 0; i < LANDMARK_COUNT; ++i)
        {
            Vector3 targetPos = b.localPositionTargets[i] + b.calibrationOffset;
            b.instances[i].transform.localPosition = Vector3.MoveTowards(
                b.instances[i].transform.localPosition,
                targetPos,
                Time.deltaTime * maxSpeed
            );
        }

        // 4. Update line renderers
        b.UpdateLines();

        // 5. Drive rig bones (direct 1:1)
        for (int i = 0; i < LANDMARK_COUNT; i++)
        {
            if (rigBones[i] != null)
            {
                rigBones[i].position = b.instances[i].transform.position;
            }
        }

        // ================
        //   CHEST LOGIC
        // ================
        Vector3 leftShoulderPos = b.Position(Landmark.LEFT_SHOULDER);
        Vector3 rightShoulderPos = b.Position(Landmark.RIGHT_SHOULDER);
        Vector3 computedChestPos = (leftShoulderPos + rightShoulderPos) * 0.5f + chestOffset;

        if (chestBone)
        {
            chestBone.position = computedChestPos;
        }

        // ================
        //   HEAD LOGIC
        // ================
        Vector3 leftEarPos = b.Position(Landmark.LEFT_EAR);
        Vector3 rightEarPos = b.Position(Landmark.RIGHT_EAR);
        Vector3 mediapipeHeadPos = (leftEarPos + rightEarPos) * 0.5f;

        if (headBone)
        {
            Vector3 blendPos = Vector3.Lerp(computedChestPos, mediapipeHeadPos, headBlend);
            blendPos += headOffset;
            headBone.position = blendPos;

            if (rotateHead)
            {
                // direction = cross(earVector, up)
                Vector3 earVector = (rightEarPos - leftEarPos).normalized;
                Vector3 forwardDir = Vector3.Cross(earVector, Vector3.up).normalized;
                Vector3 upDir = (headBone.position - computedChestPos).normalized;
                if (upDir.magnitude < 0.001f) upDir = Vector3.up;

                Quaternion rawRot = Quaternion.LookRotation(forwardDir, upDir);

                // Apply a local offset so it doesn't look down
                Quaternion headFix = Quaternion.Euler(headRotationOffsetEuler);
                headBone.rotation = rawRot * headFix;
            }
        }

        // ================
        //  MIDPOINT BONES
        // ================
        Vector3 leftHipPos = b.Position(Landmark.LEFT_HIP);
        Vector3 rightHipPos = b.Position(Landmark.RIGHT_HIP);
        Vector3 leftElbowPos = b.Position(Landmark.LEFT_ELBOW);
        Vector3 rightElbowPos = b.Position(Landmark.RIGHT_ELBOW);
        Vector3 leftWristPos = b.Position(Landmark.LEFT_WRIST);
        Vector3 rightWristPos = b.Position(Landmark.RIGHT_WRIST);
        Vector3 leftKneePos = b.Position(Landmark.LEFT_KNEE);
        Vector3 rightKneePos = b.Position(Landmark.RIGHT_KNEE);
        Vector3 leftAnklePos = b.Position(Landmark.LEFT_ANKLE);
        Vector3 rightAnklePos = b.Position(Landmark.RIGHT_ANKLE);

        // 1) Single hipsBone = midpoint of leftHip(#23) & rightHip(#24)
        if (hipsBone)
        {
            Vector3 hipsPos = (leftHipPos + rightHipPos) * 0.5f;
            hipsBone.position = Vector3.MoveTowards(hipsBone.position, hipsPos, Time.deltaTime * maxSpeed);
        }

        // 2) Torso = midpoint of chestBone & hipsBone
        if (torsoBone && hipsBone && chestBone)
        {
            Vector3 torsoPos = (hipsBone.position + chestBone.position) * 0.5f;
            torsoBone.position = Vector3.MoveTowards(torsoBone.position, torsoPos, Time.deltaTime * maxSpeed);
        }

        // 3) Forearms = midpoint (elbow->wrist)
        if (forearmRightBone)
        {
            Vector3 frPos = (rightElbowPos + rightWristPos) * 0.5f;
            forearmRightBone.position = Vector3.MoveTowards(forearmRightBone.position, frPos, Time.deltaTime * maxSpeed);

            if (rotateArms)
            {
                // direction from elbow to wrist
                Vector3 dir = (rightWristPos - rightElbowPos).normalized;
                // pick an "up" reference (could be chest->head or world up). We'll do world up for simplicity
                Quaternion rawRot = Quaternion.LookRotation(dir, Vector3.up);

                Quaternion forearmFix = Quaternion.Euler(forearmRotationOffsetEuler);
                forearmRightBone.rotation = rawRot * forearmFix;
            }
        }
        if (forearmLeftBone)
        {
            Vector3 flPos = (leftElbowPos + leftWristPos) * 0.5f;
            forearmLeftBone.position = Vector3.MoveTowards(forearmLeftBone.position, flPos, Time.deltaTime * maxSpeed);

            if (rotateArms)
            {
                Vector3 dir = (leftWristPos - leftElbowPos).normalized;
                Quaternion rawRot = Quaternion.LookRotation(dir, Vector3.up);

                Quaternion forearmFix = Quaternion.Euler(forearmRotationOffsetEuler);
                forearmLeftBone.rotation = rawRot * forearmFix;
            }
        }

        // 4) Upper arms = midpoint (shoulder->elbow)
        if (upperRightArmBone)
        {
            Vector3 uraPos = (rightShoulderPos + rightElbowPos) * 0.5f;
            upperRightArmBone.position = Vector3.MoveTowards(upperRightArmBone.position, uraPos, Time.deltaTime * maxSpeed);

            if (rotateArms)
            {
                Vector3 dir = (rightElbowPos - rightShoulderPos).normalized;
                Quaternion rawRot = Quaternion.LookRotation(dir, Vector3.up);

                Quaternion armFix = Quaternion.Euler(armRotationOffsetEuler);
                upperRightArmBone.rotation = rawRot * armFix;
            }
        }
        if (upperLeftArmBone)
        {
            Vector3 ulaPos = (leftShoulderPos + leftElbowPos) * 0.5f;
            upperLeftArmBone.position = Vector3.MoveTowards(upperLeftArmBone.position, ulaPos, Time.deltaTime * maxSpeed);

            if (rotateArms)
            {
                Vector3 dir = (leftElbowPos - leftShoulderPos).normalized;
                Quaternion rawRot = Quaternion.LookRotation(dir, Vector3.up);

                Quaternion armFix = Quaternion.Euler(armRotationOffsetEuler);
                upperLeftArmBone.rotation = rawRot * armFix;
            }
        }

        // 5) Upper legs = midpoint (hip->knee)
        if (upperLegRightBone)
        {
            Vector3 ulrPos = (rightHipPos + rightKneePos) * 0.5f;
            upperLegRightBone.position = Vector3.MoveTowards(upperLegRightBone.position, ulrPos, Time.deltaTime * maxSpeed);
        }
        if (upperLegLeftBone)
        {
            Vector3 ullPos = (leftHipPos + leftKneePos) * 0.5f;
            upperLegLeftBone.position = Vector3.MoveTowards(upperLegLeftBone.position, ullPos, Time.deltaTime * maxSpeed);
        }

        // 6) Lower legs = midpoint (knee->ankle)
        if (lowerLegRightBone)
        {
            Vector3 llrPos = (rightKneePos + rightAnklePos) * 0.5f;
            lowerLegRightBone.position = Vector3.MoveTowards(lowerLegRightBone.position, llrPos, Time.deltaTime * maxSpeed);
        }
        if (lowerLegLeftBone)
        {
            Vector3 lllPos = (leftKneePos + leftAnklePos) * 0.5f;
            lowerLegLeftBone.position = Vector3.MoveTowards(lowerLegLeftBone.position, lllPos, Time.deltaTime * maxSpeed);
        }
    }

    // =======================
    //   NESTED CLASSES/ENUM
    // =======================

    public struct AccumulatedBuffer
    {
        public Vector3 value;
        public int accumulatedValuesCount;
        public AccumulatedBuffer(Vector3 v, int ac)
        {
            value = v;
            accumulatedValuesCount = ac;
        }
    }

    public class Body
    {
        public Transform parent;
        public AccumulatedBuffer[] positionsBuffer = new AccumulatedBuffer[LANDMARK_COUNT];
        public Vector3[] localPositionTargets = new Vector3[LANDMARK_COUNT];
        public GameObject[] instances = new GameObject[LANDMARK_COUNT];
        public LineRenderer[] lines = new LineRenderer[LINES_COUNT];
        public GameObject head;

        public bool active;
        public bool setCalibration = false;
        public Vector3 calibrationOffset;

        public Body(
            Transform parent,
            GameObject landmarkPrefab,
            GameObject linePrefab,
            float sphereScale,
            GameObject headPrefab
        )
        {
            this.parent = parent;
            for (int i = 0; i < instances.Length; ++i)
            {
                instances[i] = Object.Instantiate(landmarkPrefab);
                instances[i].transform.localScale = Vector3.one * sphereScale;
                instances[i].transform.parent = parent;
                instances[i].name = ((Landmark)i).ToString();

                if (headPrefab && i >= 0 && i <= 10)
                {
                    instances[i].transform.localScale = Vector3.zero;
                }
            }

            for (int i = 0; i < lines.Length; ++i)
            {
                lines[i] = Object.Instantiate(linePrefab).GetComponent<LineRenderer>();
            }

            if (headPrefab)
            {
                head = Object.Instantiate(headPrefab);
                head.transform.localPosition = headPrefab.transform.position;
                head.transform.localRotation = headPrefab.transform.localRotation;
                head.transform.localScale = headPrefab.transform.localScale;
            }
        }

        public void UpdateLines()
        {
            lines[0].positionCount = 4;
            lines[0].SetPosition(0, Position((Landmark)32));
            lines[0].SetPosition(1, Position((Landmark)30));
            lines[0].SetPosition(2, Position((Landmark)28));
            lines[0].SetPosition(3, Position((Landmark)32));

            lines[1].positionCount = 4;
            lines[1].SetPosition(0, Position((Landmark)31));
            lines[1].SetPosition(1, Position((Landmark)29));
            lines[1].SetPosition(2, Position((Landmark)27));
            lines[1].SetPosition(3, Position((Landmark)31));

            lines[2].positionCount = 3;
            lines[2].SetPosition(0, Position((Landmark)28));
            lines[2].SetPosition(1, Position((Landmark)26));
            lines[2].SetPosition(2, Position((Landmark)24));

            lines[3].positionCount = 3;
            lines[3].SetPosition(0, Position((Landmark)27));
            lines[3].SetPosition(1, Position((Landmark)25));
            lines[3].SetPosition(2, Position((Landmark)23));

            lines[4].positionCount = 5;
            lines[4].SetPosition(0, Position((Landmark)24));
            lines[4].SetPosition(1, Position((Landmark)23));
            lines[4].SetPosition(2, Position((Landmark)11));
            lines[4].SetPosition(3, Position((Landmark)12));
            lines[4].SetPosition(4, Position((Landmark)24));

            lines[5].positionCount = 4;
            lines[5].SetPosition(0, Position((Landmark)12));
            lines[5].SetPosition(1, Position((Landmark)14));
            lines[5].SetPosition(2, Position((Landmark)16));
            lines[5].SetPosition(3, Position((Landmark)22));

            lines[6].positionCount = 4;
            lines[6].SetPosition(0, Position((Landmark)11));
            lines[6].SetPosition(1, Position((Landmark)13));
            lines[6].SetPosition(2, Position((Landmark)15));
            lines[6].SetPosition(3, Position((Landmark)21));

            lines[7].positionCount = 4;
            lines[7].SetPosition(0, Position((Landmark)16));
            lines[7].SetPosition(1, Position((Landmark)18));
            lines[7].SetPosition(2, Position((Landmark)20));
            lines[7].SetPosition(3, Position((Landmark)16));

            lines[8].positionCount = 4;
            lines[8].SetPosition(0, Position((Landmark)15));
            lines[8].SetPosition(1, Position((Landmark)17));
            lines[8].SetPosition(2, Position((Landmark)19));
            lines[8].SetPosition(3, Position((Landmark)15));

            if (!head)
            {
                lines[9].positionCount = 2;
                lines[9].SetPosition(0, Position((Landmark)10));
                lines[9].SetPosition(1, Position((Landmark)9));

                lines[10].positionCount = 5;
                lines[10].SetPosition(0, Position((Landmark)8));
                lines[10].SetPosition(1, Position((Landmark)5));
                lines[10].SetPosition(2, Position((Landmark)0));
                lines[10].SetPosition(3, Position((Landmark)2));
                lines[10].SetPosition(4, Position((Landmark)7));
            }
        }

        public void Calibrate()
        {
            Vector3 centre = (localPositionTargets[(int)Landmark.LEFT_HIP] +
                              localPositionTargets[(int)Landmark.RIGHT_HIP]) / 2f;
            calibrationOffset = -centre;
            setCalibration = true;
        }

        public Vector3 Position(Landmark Mark) =>
            instances[(int)Mark].transform.position;
    }
}


