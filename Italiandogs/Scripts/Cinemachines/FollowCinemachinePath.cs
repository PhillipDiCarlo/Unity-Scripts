using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
using UnityEngine.Serialization;
using Cinemachine;
using Cinemachine.Utility;
public class FollowCinemachinePath : MonoBehaviour
{
    public CinemachineSmoothPath smoothPath; // Reference to your CinemachineSmoothPath
    [Range(0.1f, 10f)]
    public float speed = 1.0f;   // Speed of movement (default)
    private float pathPosition = 0f; // Current position on the path

    void Update()
    {
        if (smoothPath != null)
        {
            // Move along the path
            pathPosition += speed * Time.deltaTime;

            // Let Cinemachine handle the looping automatically
            if (pathPosition > smoothPath.PathLength)
            {
                pathPosition = 0f; // Reset to the start if it exceeds the path length
            }

            // Get position and orientation from the smooth path
            transform.position = smoothPath.EvaluatePositionAtUnit(pathPosition, CinemachinePathBase.PositionUnits.Distance);
            transform.rotation = smoothPath.EvaluateOrientationAtUnit(pathPosition, CinemachinePathBase.PositionUnits.Distance);
        }
    }

    // Public method to set the speed dynamically
    public void SetSpeed(float newSpeed)
    {
        speed = Mathf.Clamp(newSpeed, 0.1f, 10f); // Ensure the speed stays within a reasonable range
    }
}