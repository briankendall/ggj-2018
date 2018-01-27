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
    
    // Jumping
    float kJumpInitialVerticalVelocity = 7f;
    float kJumpMaxSpeedDuration = 0.30f;
    
    // Climbing
    float kClimbMaxVerticalSpeed = 4f;
    float kClimbVerticalAccel = 18f;
    float kClimbVerticalDecel = 50f;
    float kClimbHorizontalAdjustVelocity = 0.8f;
    
    // Dropping item
    float kDropItemHorizontalVelocity = 2f;
    float kDropItemVerticalVelocity = 12f;
    
    
    struct PlayerState {
        public float inputX;
        public float inputY;
        public int direction;
        public bool inputJump;
        public bool inputAction;
        public bool inputReset;
        public bool inputDrop;
        public bool isGrounded;
        public Vector2 velocity;
        public float jumpMaxVelocityEndTime;
        
        public bool isClimbing;
        public float climbingTargetX;
        
        public static PlayerState initialPlayerState() {
            PlayerState result;
            result.inputX = 0;
            result.inputY = 0;
            result.inputJump = false;
            result.inputAction = false;
            result.inputReset = false;
            result.inputDrop = false;
            result.isGrounded = false;
            result.direction = kDirectionRight;
            result.velocity = Vector2.zero;
            result.jumpMaxVelocityEndTime = 0;
            result.isClimbing = false;
            result.climbingTargetX = 0f;
            
            return result;
        }
    };
    
    CharacterController2D controller;
    PlayerState state, previousState;
    BoxCollider2D boxCollider;
    
	// Use this for initialization
	void Start () {
        controller = GetComponent<CharacterController2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        previousState = PlayerState.initialPlayerState();
	}
	
    Vector3 playerWorldPosition() {
        return boxCollider.bounds.center;
    }
    
    
    GameTile tileAtWorldPosition(Vector3 pos) {
        Vector2Int tilePos = misc.convert3ito2i(LevelController.get().levelTilemap.WorldToCell(pos));
        return LevelController.get().levelTileAtTilePosition(tilePos);
    }
    
    GameTile.Type tileTypeAtPlayerPosition() {
        GameTile tile = tileAtWorldPosition(playerWorldPosition());
        return tile ? tile.type : GameTile.Type.None;
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
        
        if (!controller.isGrounded) {
            if (state.inputJump && Time.time < state.jumpMaxVelocityEndTime) {
                applyGravity = false;
            }
        }
        
        if (applyGravity) {
            state.velocity.y += Constants.gravity * Time.deltaTime;
        }
        
        state.velocity.y = Mathf.Clamp(state.velocity.y, -Constants.maxVerticalSpeed, Constants.maxVerticalSpeed);
    }
    
    bool isStartingClimbing() {
        if (state.inputY == 0) {
            return false;
        }
        
        GameTile.Type tileType = tileTypeAtPlayerPosition();
        
        if (tileType == GameTile.Type.Ladder && !(state.isGrounded && state.inputY < 0)) {
            return true;
        } else {
            return false;
        }
    }
    
    bool aboutToReachEndOfLadder() {
        GameTile nextTile = tileAtWorldPosition(playerWorldPosition() + ((Vector3)state.velocity) * Time.deltaTime);
        return !nextTile || nextTile.type != GameTile.Type.Ladder;
    }
    
    void startClimbing() {
        state.isClimbing = true;
        state.velocity.x = 0f;
        state.velocity.y = 0f;
        
        GameTile leftTile, rightTile;
        Vector3 leftTilePos = Vector3.zero, rightTilePos = Vector3.zero;
        
        Vector2Int tilePos = misc.convert3ito2i(LevelController.get().levelTilemap.WorldToCell(playerWorldPosition()));
        leftTile = LevelController.get().levelTileAtTilePosition(new Vector2Int(tilePos.x-1, tilePos.y));
        rightTile = LevelController.get().levelTileAtTilePosition(new Vector2Int(tilePos.x+1, tilePos.y));
        
        if ((leftTile && leftTile.type == GameTile.Type.Ladder) && (rightTile && rightTile.type == GameTile.Type.Ladder)) {
            state.climbingTargetX = transform.localPosition.x;
            
        } else if (leftTile && leftTile.type == GameTile.Type.Ladder) {
            leftTilePos = LevelController.get().levelTilemap.CellToWorld(new Vector3Int(tilePos.x-1, tilePos.y, 0));
            rightTilePos = LevelController.get().levelTilemap.CellToWorld(new Vector3Int(tilePos.x+1, tilePos.y, 0));
        } else {
            leftTilePos = LevelController.get().levelTilemap.CellToWorld(new Vector3Int(tilePos.x, tilePos.y, 0));
            rightTilePos = LevelController.get().levelTilemap.CellToWorld(new Vector3Int(tilePos.x+2, tilePos.y, 0));
        }
        
        state.climbingTargetX = (leftTilePos.x + rightTilePos.x) / 2f;
    }
    
    void processClimbing() {
        if (state.inputJump && !previousState.inputJump) {
            state.isClimbing = false;
            return;
        }
        
        if (state.inputY > 0) {
            state.velocity.y += kClimbVerticalAccel * Time.deltaTime;
        } else if (state.inputY < 0) {
            state.velocity.y -= kClimbVerticalAccel * Time.deltaTime;
        } else {
            if (state.velocity.y > 0) {
                state.velocity.y = Mathf.Max(state.velocity.y - kClimbVerticalDecel, 0f);
            } else if (state.velocity.y < 0) {
                state.velocity.y = Mathf.Min(state.velocity.y + kClimbVerticalDecel, 0f);
            }
        }
        
        state.velocity.y = Mathf.Clamp(state.velocity.y, -kClimbMaxVerticalSpeed, kClimbMaxVerticalSpeed);
        
        if (transform.localPosition.x < state.climbingTargetX) {
            transform.localPosition = new Vector3(Mathf.Min(transform.localPosition.x + kClimbHorizontalAdjustVelocity * Time.deltaTime,
                                                            state.climbingTargetX),
                                                  transform.localPosition.y, transform.localPosition.z);
        } else if (transform.localPosition.x > state.climbingTargetX) {
            transform.localPosition = new Vector3(Mathf.Max(transform.localPosition.x - kClimbHorizontalAdjustVelocity * Time.deltaTime,
                                                            state.climbingTargetX),
                                                  transform.localPosition.y, transform.localPosition.z);
        }
        
        if (aboutToReachEndOfLadder()) {
            state.velocity.y = 0f;
        }
    }
    
    void landedOnGround() {
        state.isClimbing = false;
    }
    
    
    
    void performAction() {
        Vector3 actionPos = transform.localPosition;
        
        GameObject item = LevelController.get().itemOverlappingBounds(boxCollider.bounds);
        
        if (LevelController.get().itemInInventory == ItemController.Type.None && item) {
            ItemController itemController = item.GetComponent<ItemController>();
            LevelController.get().setCurrentItemInInventory(itemController.itemType);
            Destroy(item);
            return;
        }
        
        Vector2Int interactableTilePos = misc.convert3ito2i(LevelController.get().levelTilemap.WorldToCell(playerWorldPosition()));
        GameTile interactableTile = LevelController.get().interactableTileAtTilePosition(interactableTilePos);
        
        if (interactableTile && interactableTile.type == GameTile.Type.Console) {
            LevelController.get().activateConsole(interactableTilePos);
            return;
        }
        
        Debug.Log("Can't interact");
    }
    
    void dropItem() {
        Debug.Log("Drop!");
        if (LevelController.get().itemInInventory == ItemController.Type.None) {
            Debug.Log("No item!");
            return;
        }
        
        Vector2 itemVelocity;
        
        if (state.direction == kDirectionRight) {
            itemVelocity = new Vector3(-kDropItemHorizontalVelocity, kDropItemVerticalVelocity);
        } else {
            itemVelocity = new Vector3(kDropItemHorizontalVelocity, kDropItemVerticalVelocity);
        }
        
        LevelController.get().spawnItemAtPositionAndVelocity(LevelController.get().itemInInventory,
                                                             boxCollider.bounds.center,
                                                             itemVelocity);
        LevelController.get().clearItemInInventory();
    }
    
    void useItem() {
        if (LevelController.get().itemInInventory == ItemController.Type.None) {
            Debug.Log("No item!");
            return;
        }
        
        if (LevelController.get().itemInInventory == ItemController.Type.RedHerring) {
            eatHerring();
            return;
        }
        
        if (LevelController.get().itemInInventory == ItemController.Type.GravityMittens) {
            jump();
            return;
        }
        
        if (LevelController.get().itemInInventory == ItemController.Type.EatenRedHerring) {
            Debug.Log("Meow!");
            return;
        }
    }
    
    void eatHerring() {
        LevelController.get().setCurrentItemInInventory(ItemController.Type.EatenRedHerring);
    }
    
	// Update is called once per frame
	void FixedUpdate () {
        state.inputX = Input.GetAxis("Horizontal");
        state.inputY = Input.GetAxis("Vertical");
        state.inputJump = (Input.GetAxis("Jump") > 0.0);
        state.inputAction = (Input.GetAxis("Action") > 0.0);
        state.inputReset = (Input.GetAxis("Reset") > 0.0);
        state.inputDrop = (Input.GetAxis("Drop") > 0.0);
        state.isGrounded = controller.isGrounded;
        
        if (!state.isClimbing && isStartingClimbing()) {
            startClimbing();
        }
        
        if (state.isClimbing) {
            processClimbing();
        } else {
            processNormalMovement();
        }
        
        if (state.inputAction && !previousState.inputAction) {
            performAction();
        }
        
        if (state.inputDrop && !previousState.inputDrop) {
            dropItem();
        }
        
        if (state.inputJump && !previousState.inputJump) {
            useItem();
        }
        
        previousState.isGrounded = controller.isGrounded;
        controller.move(state.velocity * Time.fixedDeltaTime);
        
        if (controller.collisionState.left || controller.collisionState.right) {
            state.velocity.x = 0f;
        }
        
        if (controller.isGrounded && !state.isGrounded) {
            landedOnGround();
        }
        
        previousState = state;
	}
    
}
