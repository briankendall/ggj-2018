using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExplosionController : MonoBehaviour {
    public Vector3 velocity = Vector3.zero;
    
	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		transform.localPosition = transform.localPosition + (velocity * Time.deltaTime);
	}
}
