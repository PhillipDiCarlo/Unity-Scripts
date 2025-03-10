using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
using UnityEngine.Serialization;
using Cinemachine;
using Cinemachine.Utility;

/* Curve Follower Audio Source
 * Usage:
 * Create a Cinemachine Path on a seperate gameObject in the hierarchy. 
 * Then, assign it to the Path variable on this object.
 * This object will then track along the path to the player's head.
 */

public class CurveFollowAudioSource : UdonSharpBehaviour
{
    // The path to follow. This script uses Cinemachine paths because, as far as I know, 
	// it's faster to use the internal Cinemachine libraries than calculate our own path. 
    public CinemachinePathBase m_Path;

    // Where to put the follower relative to the path postion.  X is perpendicular 
    // to the path, Y is up, and Z is parallel to the path.
    public Vector3 m_PathOffset = Vector3.zero;

    // The audio source's current position on the path. 
    private float m_PathPosition;

    // The type of units used to measure the path position. 
    public CinemachinePathBase.PositionUnits m_PositionUnits = 
    	CinemachinePathBase.PositionUnits.PathUnits;

    // Offset, in current position units, from the closest point on the path to the follow target.
    public float m_PositionOffset;

    // Search up to this many waypoints on either side of the current position.  
    // Use 0 for the entire path.
    public int m_SearchRadius = 2;

    // Search between waypoints by dividing the segment into this many straight pieces.
    // The higher the number, the more accurate the result, but performance is
    // proportionally slower for higher numbers
    public int m_SearchResolution = 2;

    private float m_PreviousPathPosition = 0;
    Quaternion m_PreviousOrientation = Quaternion.identity;

    void Start()
    {
    	// Check if the path is valid and exit if it isn't.
        if (!Utilities.IsValid(m_Path)) this.gameObject.SetActive(false);
        // Curiously, it's actually possible to edit the paths and disable them at runtime. 
    }
        
    public void MoveAlongTrack()
    {
        // Init 
        if (!(enabled && Utilities.IsValid(m_Path)))
            return;

		// Get the player's position
        Vector3 listenerPos = Networking.LocalPlayer.GetTrackingData(
          VRCPlayerApi.TrackingDataType.Head).position;
        // Get the new ideal path base position
        {
            float prevPos = m_Path.ToNativePathUnits(m_PreviousPathPosition, m_PositionUnits);
            // This works in path units
            m_PathPosition = m_Path.FindClosestPoint(
                listenerPos,
                Mathf.FloorToInt(prevPos),
                (m_SearchRadius <= 0)
                    ? -1 : m_SearchRadius,
                m_SearchResolution);
            m_PathPosition = m_Path.FromPathNativeUnits(m_PathPosition, m_PositionUnits);

            // Apply the path position offset
            m_PathPosition += m_PositionOffset;
        }
        float newPathPosition = m_PathPosition;

        m_PreviousPathPosition = newPathPosition;
        Quaternion newPathOrientation = m_Path.EvaluateOrientationAtUnit(newPathPosition, m_PositionUnits);

        // Apply the offset to get the new position
        Vector3 newPosition = m_Path.EvaluatePositionAtUnit(newPathPosition, m_PositionUnits);
        Vector3 offsetX = newPathOrientation * Vector3.right;
        Vector3 offsetY = newPathOrientation * Vector3.up;
        Vector3 offsetZ = newPathOrientation * Vector3.forward;
        newPosition += m_PathOffset.x * offsetX;
        newPosition += m_PathOffset.y * offsetY;
        newPosition += m_PathOffset.z * offsetZ;

        // Get the orientation (though it's probably useless here)
        Quaternion newOrientation
            = Quaternion.LookRotation(newPathOrientation * Vector3.forward, Vector3.up);

        this.transform.position = newPosition;
        this.transform.rotation = newOrientation;
    }

    void LateUpdate()
    {
    	MoveAlongTrack();
    }
}
