// #define FLIP // Comment out this line to flip the landmarks (internally) [technically need to flip here, but kept like this for backward compatibility].
// NOTE: image = cv2.flip(image, 1) in the Python side may also be of interest to you as well.

#if FLIP
public enum Landmark
{
    NOSE = 0,
    LEFT_EYE_INNER = 4,
    LEFT_EYE = 5,
    LEFT_EYE_OUTER = 6,
    RIGHT_EYE_INNER = 1,
    RIGHT_EYE = 2,
    RIGHT_EYE_OUTER = 3,
    LEFT_EAR = 8,
    RIGHT_EAR = 7,
    MOUTH_LEFT = 10,
    MOUTH_RIGHT = 9,
    LEFT_SHOULDER = 12,
    RIGHT_SHOULDER = 11,
    LEFT_ELBOW = 14,
    RIGHT_ELBOW = 13,
    LEFT_WRIST = 16,
    RIGHT_WRIST = 15,
    LEFT_PINKY = 18,
    RIGHT_PINKY = 17,
    LEFT_INDEX = 20,
    RIGHT_INDEX = 19,
    LEFT_THUMB = 22,
    RIGHT_THUMB = 21,
    LEFT_HIP = 24,
    RIGHT_HIP = 23,
    LEFT_KNEE = 26,
    RIGHT_KNEE = 25,
    LEFT_ANKLE = 28,
    RIGHT_ANKLE = 27,
    LEFT_HEEL = 30,
    RIGHT_HEEL = 29,
    LEFT_FOOT_INDEX = 32,
    RIGHT_FOOT_INDEX = 31,
}

#else
public enum Landmark
{
    NOSE = 0,
    LEFT_EYE_INNER = 1,
    LEFT_EYE = 2,
    LEFT_EYE_OUTER = 3,
    RIGHT_EYE_INNER = 4,
    RIGHT_EYE = 5,
    RIGHT_EYE_OUTER = 6,
    LEFT_EAR = 7,
    RIGHT_EAR = 8,
    LEFT_SHOULDER = 11,
    RIGHT_SHOULDER = 12,
    LEFT_ELBOW = 13,
    RIGHT_ELBOW = 14,
    LEFT_WRIST = 15,
    RIGHT_WRIST = 16,
    LEFT_HIP = 23,
    RIGHT_HIP = 24,
    LEFT_KNEE = 25,
    RIGHT_KNEE = 26,
    LEFT_ANKLE = 27,
    RIGHT_ANKLE = 28,
    // Add any additional custom landmarks here
}
public enum DummyLandmark
{
    FOREARM_RIGHT,
    WRIST_RIGHT,
    NECK,
    RIGHT_ELBOW,
    CHEST,
    HIPS,
    TORSO,
    RIGHT_SHOULDER,
    UPPER_LEG_RIGHT,
    LOWER_LEG_RIGHT,
    FEET_RIGHT,
    KNEE_RIGHT,
    UPPER_LEG_LEFT,
    LOWER_LEG_LEFT,
    KNEE_LEFT,
    FEET_LEFT,
    UPPER_LEFT_ARM,
    FOREARM_LEFT,
    LEFT_ELBOW,
    WRIST_LEFT,
    LEFT_SHOULDER,
    HEAD,
    UPPER_RIGHT_ARM
}

#endif