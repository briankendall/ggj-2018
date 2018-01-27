using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour {
    public GameObject player;
    
    BoxCollider2D playerBoxCollider;
    
	// Use this for initialization
	void Start () {
		playerBoxCollider = player.GetComponent<BoxCollider2D>();
	}
	
	// Update is called once per frame
	void Update () {
		transform.localPosition = new Vector3(playerBoxCollider.bounds.center.x, playerBoxCollider.bounds.center.y,
                                              transform.localPosition.z);
	}
}
