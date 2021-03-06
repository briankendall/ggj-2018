﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Prime31;
using UnityEngine.UI;

public class ItemController : MonoBehaviour {
    const float kVerticalVelocityBounceFactor = -0.8f;
    const float kHorizontalVelocityBounceFactor = 0.75f;
    const float kHorizontalVelocityDampenFactor = 0.98f;
     
    public enum Type {
        None, RedHerring, EatenRedHerring, GravityMittens, MouseGrenades
    };
    
    static public Type stringToType(string s) {
        return (Type)System.Enum.Parse(typeof(Type), s, true);
    }
    
    struct ItemState {
        public Vector2 velocity;
        
        public static ItemState initialItemState() {
            ItemState result;
            result.velocity = Vector2.zero;
            return result;
        }
    };
    
    ItemState state = ItemState.initialItemState();
    ItemState previousState = ItemState.initialItemState();
    CharacterController2D controller;
    SpriteRenderer spriteRenderer;
    private Type _itemType;
    float awakeTime;
    float startTime;
    
	// Use this for initialization
	void Awake () {
		controller = GetComponent<CharacterController2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        awakeTime = Time.time;
        startTime = 0f;
	}
    
    public static Sprite spriteForItemType(Type type) {
        return Resources.Load<Sprite>("Items/" + type);
    }
    
    public Type itemType {
        get {
            return _itemType;
        }
        
        set {
            _itemType = value;
            Sprite s = spriteForItemType(_itemType);
            
            if (!s) {
                Debug.Log("sprite didn't load! :_itemType: " + _itemType);
            } else {
                spriteRenderer.sprite = s;
            }
        }
    }
    
    public void setVelocity(Vector2 vel) {
        state.velocity = vel;
    }
    
    public void freezeForDuration(float delay) {
        startTime = awakeTime + delay;
    }
	
	// Update is called once per frame
	void FixedUpdate () {
        if (Time.time < startTime) {
            return;
        }
        
        state.velocity.x *= kHorizontalVelocityDampenFactor;
        state.velocity.y += Constants.gravity * Time.deltaTime;
        state.velocity.y = Mathf.Clamp(state.velocity.y, -Constants.maxVerticalSpeed, Constants.maxVerticalSpeed);
        controller.move(state.velocity * Time.fixedDeltaTime);
        transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, -1.5f);
        
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
