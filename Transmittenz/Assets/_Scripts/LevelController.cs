using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class LevelController : MonoBehaviour {
    private static LevelController _singleton = null;

    GameObject levelTilemapGameObject;
    GameObject itemsTilemapGameObject;
    public Tilemap levelTilemap;
    public Tilemap itemsTilemap;
    public Image itemInventoryImage;
    public ItemController.Type itemInInventory;

    static public LevelController get() {
        return _singleton;
    }

    // Use this for initialization
    void Awake() {
        if (!_singleton) {
            _singleton = this;
        } else {
            Debug.Log("Warning: trying to create second instance of LevelController");
            return;
        }
        
        levelTilemapGameObject = GameObject.FindGameObjectsWithTag("levelTilemap")[0];
        itemsTilemapGameObject = GameObject.FindGameObjectsWithTag("itemsTilemap")[0];
        levelTilemap = levelTilemapGameObject.GetComponent<Tilemap>();
        itemsTilemap = itemsTilemapGameObject.GetComponent<Tilemap>();
    }
    
    static public GameTile tileAtTilePosition(Tilemap map, Vector3Int tilePos) {
        return (GameTile)map.GetTile(tilePos);
    }
    
    public GameTile levelTileAtTilePosition(Vector3Int tilePos) {
        return tileAtTilePosition(levelTilemap, tilePos);
    }
    
    public GameObject itemOverlappingBounds(Bounds bounds) {
        Debug.Log("itemOverlappingBounds");
         GameObject[] items = GameObject.FindGameObjectsWithTag("item");
         
         foreach(GameObject item in items) {
            Debug.Log("  item: " + item);
            BoxCollider2D collider = item.GetComponent<BoxCollider2D>();
            
            if (!collider) {
                continue;
            }
            
            Debug.Log("  item bounds: " + collider.bounds);
            Debug.Log("  in bounds: " + bounds);
            
            if (misc.boundsIntersects2D(collider.bounds, bounds)) {
                return item;
            }
         }
         
         return null;
    }
    
    public void setCurrentItemInInventory(GameObject item) {
        ItemController itemController = item.GetComponent<ItemController>();
        itemInInventory = itemController.itemType;
        Sprite s = ItemController.spriteForItemType(itemController.itemType);
        itemInventoryImage.sprite = s;
    }
    
    public void clearItemInInventory() {
        itemInInventory = ItemController.Type.None;
        itemInventoryImage.sprite = null;
    }
    
    public void spawnItemAtPositionAndVelocity(ItemController.Type type, Vector2 pos, Vector2 vel) {
        GameObject item = Instantiate(Resources.Load("Prefabs/item"), pos, Quaternion.identity) as GameObject;
        ItemController itemController = item.GetComponent<ItemController>();
        itemController.itemType = type;
        itemController.setVelocity(vel);
    }
    
	// Use this for initialization
	void Start () {
        for(int x = 0; x < itemsTilemap.size.x; ++x) {
            for(int y = 0; y < itemsTilemap.size.y; ++y) {
                Vector3Int tilePos = new Vector3Int(x + itemsTilemap.origin.x, y + itemsTilemap.origin.y, 0);
                Sprite sprite = itemsTilemap.GetSprite(tilePos);
                
                if (!sprite) {
                    continue;
                }
                
                itemsTilemap.SetTile(tilePos, null);
                Vector3 pos = itemsTilemap.CellToWorld(tilePos);
                spawnItemAtPositionAndVelocity(ItemController.stringToType(sprite.name), pos, Vector2.zero);
            }
        }
	}
	
}
