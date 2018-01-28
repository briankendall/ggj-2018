using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour {
    const float kFollowSpeedFactor = 0.8f;
    
    public GameObject player;
    
    BoxCollider2D playerBoxCollider;
    bool isFollowingPlayer = true;
    Vector3 targetPoint;
    
	// Use this for initialization
	void Start () {
		playerBoxCollider = player.GetComponent<BoxCollider2D>();
	}
	
    public void followPlayer() {
        isFollowingPlayer = true;
    }
    
    public void followGlobalPos(Vector3 pos) {
        isFollowingPlayer = false;
        targetPoint = pos;
    }
    
	// Update is called once per frame
	void Update () {
        if (isFollowingPlayer) {
            targetPoint = new Vector3(playerBoxCollider.bounds.center.x, playerBoxCollider.bounds.center.y,
                                              transform.localPosition.z);
        }
        
        Vector3 oldPos = transform.localPosition;
        transform.localPosition = new Vector3(Mathf.Lerp(oldPos.x, targetPoint.x, kFollowSpeedFactor),
                                              Mathf.Lerp(oldPos.y, targetPoint.y, kFollowSpeedFactor),
                                              oldPos.z);
	}
}
