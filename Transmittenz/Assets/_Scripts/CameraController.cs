using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour {
    const float kFollowSpeedFactor = 0.23f;
    
    const float kCameraViewWidth = 19.2f;
    const float kCameraViewHeight = 10.8f;
    
    public GameObject player;
    public Transform[] zoneTransforms;
    
    BoxCollider2D playerBoxCollider;
    bool isFollowingPlayer = true;
    Vector3 targetPoint;
    Bounds[] zones;
    int currentZone;
    
	// Use this for initialization
	void Start () {
		playerBoxCollider = player.GetComponent<BoxCollider2D>();
        zones = new Bounds[zoneTransforms.Length];
        
        for(int i = 0; i < zoneTransforms.Length; ++i) {
            zones[i] = new Bounds();
            zones[i].min = new Vector3(zoneTransforms[i].localPosition.x - zoneTransforms[i].localScale.x/2,
                                       zoneTransforms[i].localPosition.y - zoneTransforms[i].localScale.y/2,
                                       -10000f);
            zones[i].max = new Vector3(zoneTransforms[i].localPosition.x + zoneTransforms[i].localScale.x/2,
                                       zoneTransforms[i].localPosition.y + zoneTransforms[i].localScale.y/2,
                                       10000f);
        }
        
        currentZone = -1;
	}
	
    public void followPlayer() {
        isFollowingPlayer = true;
    }
    
    public void followGlobalPos(Vector3 pos) {
        isFollowingPlayer = false;
        targetPoint = pos;
    }
    
    int zoneContainingPoint(Vector3 pos) {
        for(int i = 0; i < zones.Length; ++i) {
            //Debug.Log(i + " " + zones[i].min + " " + zones[i].max + " " + pos);
            if (zones[i].Contains(pos)) {
                return i;
            }
        }
        
        return -1;
    }
    
    Vector3 handleNoZone() {
        return targetPoint;
    }
    
    Vector3 handleInZone() {
        Bounds zone = zones[currentZone];
        Vector3 result = targetPoint;
        
        result.x = Mathf.Clamp(result.x, zone.min.x + kCameraViewWidth/2, zone.max.x - kCameraViewWidth/2);
        result.y = Mathf.Clamp(result.y, zone.min.y + kCameraViewHeight/2, zone.max.y - kCameraViewHeight/2);
        
        return result;
    }
    
	// Update is called once per frame
	void Update () {
        if (isFollowingPlayer) {
            targetPoint = new Vector3(playerBoxCollider.bounds.center.x, playerBoxCollider.bounds.center.y,
                                              transform.localPosition.z);
        }
        
        if (currentZone == -1 || !zones[currentZone].Contains(targetPoint)) {
            int newZone = zoneContainingPoint(targetPoint);
            
            if (currentZone != newZone) {
                Debug.Log("camera switched to zone: " + newZone);
                currentZone = newZone;
            } 
        }
        
        Vector3 newPos;
        
        if (currentZone == -1) {
            newPos = handleNoZone();
        } else {
            newPos = handleInZone();
        }
        
        Vector3 oldPos = transform.localPosition;
        transform.localPosition = new Vector3(Mathf.Lerp(oldPos.x, newPos.x, kFollowSpeedFactor),
                                              Mathf.Lerp(oldPos.y, newPos.y, kFollowSpeedFactor),
                                              oldPos.z);
	}
}
