using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExplosionController : MonoBehaviour {
    public Vector3 velocity = Vector3.zero;
    Animator animator;
    
	// Use this for initialization
	void Start () {
		animator = GetComponent<Animator>();
	}
	
    public void freeze() {
        animator.speed = 0.001f;
        velocity = Vector3.zero;
    }
    
	// Update is called once per frame
	void Update () {
		transform.localPosition = transform.localPosition + (velocity * Time.deltaTime);
	}
}
