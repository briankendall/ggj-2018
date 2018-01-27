using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class LevelController : MonoBehaviour {
    private static LevelController _singleton = null;

    GameObject levelTilemapGameObject;
    GameObject itemsTilemapGameObject;
    GameObject interactablesTilemapGameObject;
    GameObject linkersTilemapGameObject;
    public Tilemap levelTilemap;
    public Tilemap itemsTilemap;
    public Tilemap interactablesTilemap;
    public Tilemap linkersTilemap;
    public Image itemInventoryImage;
    public ItemController.Type itemInInventory;

    Dictionary<Vector2Int, LinkData> links;

    struct LinkData {
        public List<Vector2Int> sources;
        public List<Vector2Int> dests;
        
        public static LinkData createEmpty() {
            LinkData result;
            result.sources = new List<Vector2Int>();
            result.dests = new List<Vector2Int>();
            return result;
        }
    };

    static public LevelController get() {
        return _singleton;
    }

    void Awake() {
        if (!_singleton) {
            _singleton = this;
        } else {
            Debug.Log("Warning: trying to create second instance of LevelController");
            return;
        }
        
        levelTilemapGameObject = GameObject.FindGameObjectsWithTag("levelTilemap")[0];
        itemsTilemapGameObject = GameObject.FindGameObjectsWithTag("itemsTilemap")[0];
        interactablesTilemapGameObject = GameObject.FindGameObjectsWithTag("interactablesTilemap")[0];
        linkersTilemapGameObject = GameObject.FindGameObjectsWithTag("linkersTilemap")[0];
        levelTilemap = levelTilemapGameObject.GetComponent<Tilemap>();
        itemsTilemap = itemsTilemapGameObject.GetComponent<Tilemap>();
        interactablesTilemap = interactablesTilemapGameObject.GetComponent<Tilemap>();
        linkersTilemap = linkersTilemapGameObject.GetComponent<Tilemap>();
    }
    
    void Start () {
        for(int x = 0; x < itemsTilemap.size.x; ++x) {
            for(int y = 0; y < itemsTilemap.size.y; ++y) {
                Vector2Int tilePos = new Vector2Int(x + itemsTilemap.origin.x, y + itemsTilemap.origin.y);
                Sprite sprite = itemsTilemap.GetSprite(misc.convert2ito3i(tilePos));
                
                if (!sprite) {
                    continue;
                }
                
                itemsTilemap.SetTile(misc.convert2ito3i(tilePos), null);
                Vector3 pos = itemsTilemap.CellToWorld(misc.convert2ito3i(tilePos));
                spawnItemAtPositionAndVelocity(ItemController.stringToType(sprite.name), pos, Vector2.zero);
            }
        }
        
        buildLinksData();
    }
    
    static public GameTile tileAtTilePosition(Tilemap map, Vector2Int tilePos) {
        return (GameTile)map.GetTile(misc.convert2ito3i(tilePos));
    }
    
    public GameTile levelTileAtTilePosition(Vector2Int tilePos) {
        return tileAtTilePosition(levelTilemap, tilePos);
    }
    
    public GameTile interactableTileAtTilePosition(Vector2Int tilePos) {
        return tileAtTilePosition(levelTilemap, tilePos);
    }
    
    public GameObject itemOverlappingBounds(Bounds bounds) {
        //Debug.Log("itemOverlappingBounds");
        GameObject[] items = GameObject.FindGameObjectsWithTag("item");
         
         foreach(GameObject item in items) {
            //Debug.Log("  item: " + item);
            BoxCollider2D collider = item.GetComponent<BoxCollider2D>();
            
            if (!collider) {
                continue;
            }
            
            //Debug.Log("  item bounds: " + collider.bounds);
            //Debug.Log("  in bounds: " + bounds);
            
            if (misc.boundsIntersects2D(collider.bounds, bounds)) {
                return item;
            }
         }
         
         return null;
    }
    
    public void setCurrentItemInInventory(ItemController.Type type) {
        itemInInventory = type;
        Sprite s = ItemController.spriteForItemType(type);
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
    
    string linkerIdForTileName(string name) {
        string[] parts = name.Split('_');
        return parts[parts.Length-1];
    }
    
    bool linkerTileNameIndicatesDest(string name) {
        return name.StartsWith("numbersDest");
    }
    
    void buildLinksData() {
        bool tileLastTime = false;
        Vector2Int lastTilePos = new Vector2Int();
        
        links = new Dictionary<Vector2Int, LinkData>();
        
        Dictionary<string, List<Vector2Int> > sources = new Dictionary<string, List<Vector2Int> >();
        Dictionary<string, List<Vector2Int> > dests = new Dictionary<string, List<Vector2Int> >();
        
        for(int y = 0; y < linkersTilemap.size.y; ++y) {
            for(int x = 0; x < linkersTilemap.size.x; ++x) {
                Vector2Int tilePos = new Vector2Int(x + linkersTilemap.origin.x, y + linkersTilemap.origin.y);
                Vector3Int asdfafergfergre = new Vector3Int(tilePos.x, tilePos.y, 0);
                Sprite sprite = linkersTilemap.GetSprite(asdfafergfergre);
                //Debug.Log("tilePos: " + tilePos);
                
                if (!sprite) {
                    //Debug.Log("  no tile!");
                    tileLastTime = false;
                    continue;
                }
                
                string id = linkerIdForTileName(sprite.name);
                Dictionary<string, List<Vector2Int> > relevant;
                
                //Debug.Log(sprite.name);
                if (linkerTileNameIndicatesDest(sprite.name)) {
                    //Debug.Log("adding " + tilePos + " to dests");
                    relevant = dests;
                } else {
                    //Debug.Log("adding " + tilePos + " to sources");
                    relevant = sources;
                }
                
                if (!relevant.ContainsKey(id)) {
                    relevant[id] = new List<Vector2Int>();
                }
                
                if (!tileLastTime) {
                    relevant[id].Add(tilePos);
                    tileLastTime = true;
                    lastTilePos = tilePos;
                } else {
                    relevant[id].Add(lastTilePos);
                }
            }
        }
        
        foreach(string id in sources.Keys) {
            foreach(Vector2Int srcPos in sources[id]) {
                if (!links.ContainsKey(srcPos)) {
                    links[srcPos] = LinkData.createEmpty();
                }
                
                LinkData data = links[srcPos];
                data.dests = dests[id];
                links[srcPos] = data;
            }
        }
        
        foreach(string id in dests.Keys) {
            foreach(Vector2Int dstPos in dests[id]) {
                if (!links.ContainsKey(dstPos)) {
                    links[dstPos] = LinkData.createEmpty();
                }
                
                LinkData data = links[dstPos];
                data.sources = sources[id];
                links[dstPos] = data;
            }
        }
        
        /*
        Debug.Log("links:");
        
        foreach(Vector2Int pos in links.Keys) {
            Debug.Log("  pos: " + pos);
            Debug.Log("  sources:");
            
            foreach(Vector2Int src in links[pos].sources) {
                Debug.Log("    " + src);
            }
            Debug.Log("  dests:");
            
            foreach(Vector2Int dst in links[pos].dests) {
                Debug.Log("    " + dst);
            }
        }
        */
    }
	
    Vector2Int findUpperLeftOfInteractable(Vector2Int pos) {
        while(interactableTileAtTilePosition(pos) != null) {
            pos.x -= 1;
        }
        
        while(interactableTileAtTilePosition(pos) != null) {
            pos.y -= 1;
        }
        
        return new Vector2Int(pos.x+1, pos.y+1);
    }
    
    
    public void activateConsole(Vector2Int initialConsoleTilePos) {
        Vector2Int consoleTilePos = findUpperLeftOfInteractable(initialConsoleTilePos);
        
        Debug.Log("actual console pos: " + consoleTilePos);
    }
    
}
