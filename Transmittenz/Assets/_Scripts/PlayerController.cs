using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Prime31;

public class PlayerController : MonoBehaviour {
    const int kDirectionNone = -1;
    const int kDirectionLeft = 0;
    const int kDirectionRight = 1;
    const int kDirectionUp = 2;
    const int kDirectionDown = 3;

    float kMaxHorizontalSpeed = 6.0f;
    float kHorizontalAccel = 20f;
    float kMaxVerticalSpeed = 7f;
    float kGravity = -30f;
    float kJumpInitialVerticalVelocity = 7f;
    float kJumpMaxSpeedDuration = 0.30f;
    
    struct PlayerState {
        public float inputX;
        public float inputY;
        public int direction;
        public bool inputJump;
        public bool inputAction;
        public bool inputReset;
        public bool isGrounded;
        
        public Vector2 velocity;
        public float jumpMaxVelocityEndTime;
        
        public static PlayerState initialPlayerState() {
            PlayerState result;
            result.inputX = 0;
            result.inputY = 0;
            result.inputJump = false;
            result.inputAction = false;
            result.inputReset = false;
            result.isGrounded = false;
            result.direction = kDirectionRight;
            result.velocity = Vector2.zero;
            result.jumpMaxVelocityEndTime = 0;
            
            return result;
        }
    };
    
    CharacterController2D controller;
    BoxCollider2D boxCollider;
    PlayerState state, previousState;
    
	// Use this for initialization
	void Start () {
        controller = GetComponent<CharacterController2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        previousState = PlayerState.initialPlayerState();
	}
	
    void jump() {
        state.jumpMaxVelocityEndTime = Time.time + kJumpMaxSpeedDuration;
        state.velocity.y = kJumpInitialVerticalVelocity;
    }
    
    void processNormalMovement() {
        bool applyGravity = true;
        float targetVelocityX = 0.0f;
        
        if (state.inputX > 0) {
            targetVelocityX = kMaxHorizontalSpeed * state.inputX;
            state.direction = kDirectionRight;
        } else if (state.inputX < 0) {
            targetVelocityX = kMaxHorizontalSpeed * state.inputX;
            state.direction = kDirectionLeft;
        }
        
        if (state.velocity.x > targetVelocityX) {
            state.velocity.x = Mathf.Max(state.velocity.x - kHorizontalAccel*Time.deltaTime, targetVelocityX);
        } else if (state.velocity.x < targetVelocityX) {
            state.velocity.x = Mathf.Min(state.velocity.x + kHorizontalAccel*Time.deltaTime, targetVelocityX);
        }
        
        if (state.inputJump && !previousState.inputJump) {
            if (controller.isGrounded) {
                jump();
                applyGravity = false;
            }
        }
        
        if (!controller.isGrounded) {
            if (state.inputJump && Time.time < state.jumpMaxVelocityEndTime) {
                applyGravity = false;
            }
        }
        
        if (applyGravity) {
            state.velocity.y += kGravity * Time.deltaTime;
        }
        
        state.velocity.y = Mathf.Clamp(state.velocity.y, -kMaxVerticalSpeed, kMaxVerticalSpeed);
    }
    
	// Update is called once per frame
	void FixedUpdate () {
        state.inputX = Input.GetAxis("Horizontal");
        state.inputY = Input.GetAxis("Vertical");
        state.inputJump = (Input.GetAxis("Jump") > 0.0);
        state.inputAction = (Input.GetAxis("Action") > 0.0);
        state.inputReset = (Input.GetAxis("Reset") > 0.0);
        state.isGrounded = controller.isGrounded;
        
        processNormalMovement();
        
        previousState.isGrounded = controller.isGrounded;
        controller.move(state.velocity * Time.fixedDeltaTime);
        
        if (controller.collisionState.left || controller.collisionState.right) {
            state.velocity.x = 0f;
        }
        
        previousState = state;
	}
    
}
