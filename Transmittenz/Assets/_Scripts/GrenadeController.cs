using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Prime31;
using UnityEngine.UI;

public class GrenadeController : MonoBehaviour {
    const float kVerticalVelocityBounceFactor = -0.8f;
    const float kHorizontalVelocityBounceFactor = 0.75f;
    const float kHorizontalVelocityDampenFactor = 0.98f;
    const float kFuseDuration = 1.0f;
    const float kRotationalVelocity = 150f;
    
    struct GrenadeState {
        public Vector2 velocity;
        
        public static GrenadeState initialItemState() {
            GrenadeState result;
            result.velocity = Vector2.zero;
            return result;
        }
    };
    
    GrenadeState state = GrenadeState.initialItemState();
    GrenadeState previousState = GrenadeState.initialItemState();
    CharacterController2D controller;
    float startTime;
    
    // Use this for initialization
    void Awake () {
        controller = GetComponent<CharacterController2D>();
        startTime = Time.time;
    }
    
    public void setVelocity(Vector2 vel) {
        state.velocity = vel;
    }
    
    // Update is called once per frame
    void FixedUpdate () {
        if (Time.time - startTime >= kFuseDuration) {
            Quaternion rot = Quaternion.AngleAxis(Random.Range(0, 360), new Vector3(0f, 0f, 1f));
            Vector3 pos = transform.localPosition;
            pos.z = -5;
            LevelController.get().spawnExplosionAtPositionAndVelocity(pos, Vector3.zero,
                                                                      rot, new Vector3(2f, 2f, 2f));
            LevelController.get().explodeBlocksAt(transform.localPosition);
            Destroy(transform.gameObject);
            return;
        }
        
        state.velocity.x *= kHorizontalVelocityDampenFactor;
        state.velocity.y += Constants.gravity * Time.deltaTime;
        state.velocity.y = Mathf.Clamp(state.velocity.y, -Constants.maxVerticalSpeed, Constants.maxVerticalSpeed);
        controller.move(state.velocity * Time.fixedDeltaTime);
        transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, -1f);
        
        transform.Rotate(new Vector3(0, 0, 1f), kRotationalVelocity * Time.fixedDeltaTime);
        
        
        if (controller.collisionState.left || controller.collisionState.right) {
            state.velocity.x *= -1.0f;
        }
        
        if (controller.collisionState.above || controller.collisionState.below) {
            state.velocity.x *= kHorizontalVelocityBounceFactor;
            state.velocity.y *= kVerticalVelocityBounceFactor;
        }
        
        previousState = state;
    }
}