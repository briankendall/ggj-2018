using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class LevelController : MonoBehaviour {
    private static LevelController _singleton = null;

    struct PersistentData {
        public List<Vector3Int> foundStations;
        public Dictionary<Vector3Int, ItemController.Type> stashedItems;
        
        public static PersistentData defaultData() {
            PersistentData result = new PersistentData();
            result.foundStations = new List<Vector3Int>();
            result.stashedItems = new Dictionary<Vector3Int, ItemController.Type>();
            return result;
        }
    }
    
    static PersistentData persistentData = PersistentData.defaultData();
    
    const float kDistanceForStationToDeposit = 6.5f;
    const float kDistanceForStationToRegister = 6.5f;
    
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
    public Tile[] openPanelTiles;
    public GameObject openPanelAnimationObject;
    public GameObject stationAnimationObject;
    public GameObject camera;
    public GameObject spotlightObject;
    
    public GameTile lightOnTile;
    public GameTile lightOffTile;

    Dictionary<Vector3Int, LinkData> links;
    Dictionary<Vector3Int, Transform> stationObjects;
    CameraController cameraController;
    Vector3Int selectedStation;
    Vector3Int startStation;
    HashSet<Vector3Int> stationsThatCantDepositItem;
    bool exploding = false;
    
    struct LinkData {
        public List<Vector3Int> sources;
        public List<Vector3Int> dests;
        
        public static LinkData createEmpty() {
            LinkData result;
            result.sources = new List<Vector3Int>();
            result.dests = new List<Vector3Int>();
            return result;
        }
    };

    static public LevelController get() {
        return _singleton;
    }

    void Awake() {
        if (_singleton) {
            Debug.Log("Warning: creating second instance of LevelController");
        }
        
        _singleton = this;
        
        levelTilemapGameObject = GameObject.FindGameObjectsWithTag("levelTilemap")[0];
        itemsTilemapGameObject = GameObject.FindGameObjectsWithTag("itemsTilemap")[0];
        interactablesTilemapGameObject = GameObject.FindGameObjectsWithTag("interactablesTilemap")[0];
        linkersTilemapGameObject = GameObject.FindGameObjectsWithTag("linkersTilemap")[0];
        levelTilemap = levelTilemapGameObject.GetComponent<Tilemap>();
        itemsTilemap = itemsTilemapGameObject.GetComponent<Tilemap>();
        interactablesTilemap = interactablesTilemapGameObject.GetComponent<Tilemap>();
        linkersTilemap = linkersTilemapGameObject.GetComponent<Tilemap>();
        cameraController = camera.GetComponent<CameraController>();
        stationObjects = new Dictionary<Vector3Int, Transform>();
        stationsThatCantDepositItem = new HashSet<Vector3Int>();
    }
    
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
        
        buildLinksData();
        setupStations();
        linkersTilemapGameObject.SetActive(false);
        
        if (persistentData.stashedItems.Count == 0) {
            Debug.Log("No stashed items!");
        } else {
            foreach(Vector3Int station in persistentData.stashedItems.Keys) {
                Debug.Log("Stashed item at: " + station + "... item: " + persistentData.stashedItems[station]);
            }
        }
    }
    
    static public GameTile tileAtTilePosition(Tilemap map, Vector3Int tilePos) {
        return (GameTile)map.GetTile(tilePos);
    }
    
    public GameTile levelTileAtTilePosition(Vector3Int tilePos) {
        return tileAtTilePosition(levelTilemap, tilePos);
    }
    
    public GameTile interactableTileAtTilePosition(Vector3Int tilePos) {
        return tileAtTilePosition(interactablesTilemap, tilePos);
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
    
    public void spawnItemAtPositionWithAnimationDelay(ItemController.Type type, Vector3 pos, float delay) {
        GameObject item = Instantiate(Resources.Load("Prefabs/item"), pos, Quaternion.identity) as GameObject;
        ItemController itemController = item.GetComponent<ItemController>();
        itemController.itemType = type;
        itemController.freezeForDuration(delay);
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
        Vector3Int lastTilePos = new Vector3Int();
        
        links = new Dictionary<Vector3Int, LinkData>();
        
        Dictionary<string, List<Vector3Int> > sources = new Dictionary<string, List<Vector3Int> >();
        Dictionary<string, List<Vector3Int> > dests = new Dictionary<string, List<Vector3Int> >();
        
        for(int y = 0; y < linkersTilemap.size.y; ++y) {
            for(int x = 0; x < linkersTilemap.size.x; ++x) {
                Vector3Int tilePos = new Vector3Int(x + linkersTilemap.origin.x, y + linkersTilemap.origin.y, 0);
                Sprite sprite = linkersTilemap.GetSprite(tilePos);
                
                //Debug.Log("tilePos: " + tilePos);
                
                if (!sprite) {
                    //Debug.Log("  no tile!");
                    tileLastTime = false;
                    continue;
                }
                
                tilePos = findUpperLeftOfInteractable(tilePos);
                
                string id = linkerIdForTileName(sprite.name);
                Dictionary<string, List<Vector3Int> > relevant;
                
                //Debug.Log(sprite.name);
                if (linkerTileNameIndicatesDest(sprite.name)) {
                    //Debug.Log("adding " + tilePos + " to dests");
                    relevant = dests;
                } else {
                    //Debug.Log("adding " + tilePos + " to sources");
                    relevant = sources;
                }
                
                if (!relevant.ContainsKey(id)) {
                    relevant[id] = new List<Vector3Int>();
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
            foreach(Vector3Int srcPos in sources[id]) {
                if (!links.ContainsKey(srcPos)) {
                    links[srcPos] = LinkData.createEmpty();
                }
                
                LinkData data = links[srcPos];
                data.dests = dests[id];
                links[srcPos] = data;
            }
        }
        
        foreach(string id in dests.Keys) {
            foreach(Vector3Int dstPos in dests[id]) {
                if (!links.ContainsKey(dstPos)) {
                    links[dstPos] = LinkData.createEmpty();
                }
                
                LinkData data = links[dstPos];
                data.sources = sources[id];
                links[dstPos] = data;
            }
        }
        
        //debugPrintLinkData();
    }
	
    void debugPrintLinkData() {
        Debug.Log("DEBUG links:");
        
        foreach(Vector3Int pos in links.Keys) {
            Debug.Log("  pos: " + pos);
            Debug.Log("  sources:");
            
            foreach(Vector3Int src in links[pos].sources) {
                Debug.Log("    " + src);
            }
            Debug.Log("  dests:");
            
            foreach(Vector3Int dst in links[pos].dests) {
                Debug.Log("    " + dst);
            }
        }
    }
    
    Vector3Int findUpperLeftOfInteractable(Vector3Int startPos) {
        Vector3Int pos = startPos;
        int finalX, finalY;
        
        //Debug.Log("findUpperLeftOfInteractable: " + startPos);
        //Debug.Log("       interactableTileAtTilePosition(pos): " + interactableTileAtTilePosition(pos));
        
        while(interactableTileAtTilePosition(pos) != null) {
            pos.x -= 1;
            //Debug.Log("      " + pos);
        }
        
        finalX = pos.x + 1;
        //Debug.Log("   finalX: " + finalX);
        pos = startPos;
        
        while(interactableTileAtTilePosition(pos) != null) {
            pos.y -= 1;
            //Debug.Log("      " + pos);
        }
        
        finalY = pos.y + 1;
        //Debug.Log("   finalY: " + finalY);
        
        return new Vector3Int(finalX, finalY, 0);
    }
    
    
    public void activateConsole(Vector3Int initialConsoleTilePos) {
        Vector3Int consoleTilePos = findUpperLeftOfInteractable(initialConsoleTilePos);
        triggerInteractableSource(consoleTilePos, false);
    }
    
    void triggerInteractableSource(Vector3Int srcPos, bool okayIfNotLinked) {
        if (!links.ContainsKey(srcPos)) {
            if (!okayIfNotLinked) {
                Debug.Log("Error! Non-linked source interactable!!");
            }
            
            return;
        }
        
        foreach(Vector3Int p in links[srcPos].dests) {
            toggleInteractable(p);
        }
    }
    
    void toggleInteractable(Vector3Int p) {
        GameTile tile = interactableTileAtTilePosition(p);
        
        if (!tile) {
            Debug.Log("Error! Tried to toggle non-existent interactable at: " + p);
            return;
        }
        
        if (tile.type == GameTile.Type.Light) {
            toggleLight(tile, p);
            return;
        }
        
        if (tile.type == GameTile.Type.Panel) {
            togglePanel(tile, p);
            return;
        }
        
        Debug.Log("Error! Tried to toggle invalid interactable!");
    }
    
    bool interactableSourceIsOn(Vector3Int p) {
        GameTile tile = interactableTileAtTilePosition(p);
        
        if (tile.type == GameTile.Type.Light) {
            return tile == lightOnTile;
        }
        
        return true;
    }
    
    void toggleLight(GameTile tile, Vector3Int p) {
        Debug.Log("Toggling light!");
        
        if (tile == lightOnTile) {
            Debug.Log("  off!");
            interactablesTilemap.SetTile(p, lightOffTile);
        } else {
            Debug.Log("  on!");
            interactablesTilemap.SetTile(p, lightOnTile);
        }
        
        triggerInteractableSource(p, true);
    }
    
    void togglePanel(GameTile tile, Vector3Int pos) {
        foreach(Vector3Int p in links[pos].sources) {
            if (!interactableSourceIsOn(p)) {
                return;
            }
        }
        
        interactablesTilemap.SetTile(pos + new Vector3Int(0, 1, 0), openPanelTiles[0]);
        interactablesTilemap.SetTile(pos + new Vector3Int(1, 1, 0), openPanelTiles[1]);
        interactablesTilemap.SetTile(pos + new Vector3Int(0, 0, 0), openPanelTiles[2]);
        interactablesTilemap.SetTile(pos + new Vector3Int(1, 0, 0), openPanelTiles[3]);
        
        Vector3 animatonPos = interactablesTilemap.CellToWorld(pos);
        animatonPos.y += 0.32f;
        animatonPos.z = 0f;
        Instantiate(openPanelAnimationObject, animatonPos, Quaternion.identity);
        spawnItemAtPositionWithAnimationDelay(ItemController.Type.GravityMittens, animatonPos + new Vector3(0.32f, 0f, -0.5f),
                                              (1f/24f * 15));
        
    }
    
    void setupStations() {
        HashSet<Vector3Int> processed = new HashSet<Vector3Int>();
        
        for(int x = 0; x < interactablesTilemap.size.x; ++x) {
            for(int y = 0; y < interactablesTilemap.size.y; ++y) {
                Vector3Int tilePos = new Vector3Int(x + interactablesTilemap.origin.x, y + interactablesTilemap.origin.y, 0);
                GameTile tile = interactableTileAtTilePosition(tilePos);
                
                if (!tile || tile.type != GameTile.Type.Station) {
                    continue;
                }
                
                tilePos = findUpperLeftOfInteractable(tilePos);
                
                if (processed.Contains(tilePos)) {
                    continue;
                }
                
                Vector3 pos = interactablesTilemap.CellToWorld(tilePos);
                pos.y += 1.28f;
                pos.z = -0.5f;
                GameObject obj = (GameObject)Instantiate(stationAnimationObject, pos, Quaternion.identity);
                processed.Add(tilePos);
                stationObjects[tilePos] = obj.transform;
            }
        }
    }
    
    Vector3 centerOfStation(Vector3Int pos) {
        return interactablesTilemap.CellToWorld(pos) + new Vector3(.48f, .64f, 0f);
    }
    
    void doFocusOnSelectedStation() {
        spotlightObject.SetActive(true);
        Vector3 focusPoint = centerOfStation(selectedStation);
        spotlightObject.transform.localPosition = new Vector3(focusPoint.x, focusPoint.y, spotlightObject.transform.localPosition.z);
        cameraController.followGlobalPos(focusPoint);
    }
    
    public void focusOnStation(Vector3Int stationPos) {
        Vector3Int pos = findUpperLeftOfInteractable(stationPos);
        startStation = selectedStation = pos;
        doFocusOnSelectedStation();
    }
    
    public void selectNextStation() {
        int index = persistentData.foundStations.IndexOf(selectedStation);
        index += 1;
        
        if (index >= persistentData.foundStations.Count) {
            index = 0;
        }
        
        selectedStation = persistentData.foundStations[index];
        doFocusOnSelectedStation();
    }
    
    public void selectPrevStation() {
        int index = persistentData.foundStations.IndexOf(selectedStation);
        index -= 1;
        
        if (index < 0) {
            index = persistentData.foundStations.Count-1;
        }
        
        selectedStation = persistentData.foundStations[index];
        doFocusOnSelectedStation();
    }
    
    public void cancelSelectingStation() {
        spotlightObject.SetActive(false);
        cameraController.followPlayer();
    }
    
    public void selectStation() {
        spotlightObject.SetActive(false);
        cameraController.followPlayer();
        
        Transform stationObj = stationObjects[startStation];
        Transform stationItemObj = stationObj.Find("stationItem");
        
        if (!stationItemObj) {
            Debug.Log("Couldn't find station item object?!?");
            return;
        }
        
        Animator animator = stationObj.GetComponent<Animator>();
        SpriteRenderer itemSpriteRenderer = stationItemObj.GetComponent<SpriteRenderer>();
        
        persistentData.stashedItems[selectedStation] = itemInInventory;
        stationsThatCantDepositItem.Add(selectedStation);
        itemSpriteRenderer.sprite = ItemController.spriteForItemType(itemInInventory);
        itemSpriteRenderer.enabled = true;
        animator.Play("receiveItem", -1, 0);
        clearItemInInventory();
    }
    
    public void reportPlayerPosition(Vector3 pos) {
        foreach(Vector3Int station in persistentData.stashedItems.Keys) {
            if (stationsThatCantDepositItem.Contains(station)) {
                continue;
            }
            
            if (!persistentData.stashedItems.ContainsKey(station)) {
                continue;
            }
            
            Vector3 stationPos = centerOfStation(station);
            double d = (stationPos - pos).magnitude;
            
            if (d <= kDistanceForStationToDeposit) {
                stationDepositItem(station);
            }
        }
        
        foreach(Vector3Int station in stationObjects.Keys) {
            if (persistentData.foundStations.Contains(station)) {
                continue;
            }
            
            Vector3 stationPos = centerOfStation(station);
            double d = (stationPos - pos).magnitude;
            
            if (d <= kDistanceForStationToRegister) {
                persistentData.foundStations.Add(station);
                persistentData.foundStations.Sort((a, b) => a.x.CompareTo(b.x));
                Debug.Log("Found station! " + station);
            }
        }
    }
    
    void stationDepositItem(Vector3Int station) {
        Transform stationObj = stationObjects[station];
        Animator animator = stationObj.GetComponent<Animator>();
        animator.Play("stationClose", -1, 0);
        stationsThatCantDepositItem.Add(station);
        
        Vector3 itemPos = centerOfStation(station) + new Vector3(0f, -0.24f, 0.0f);
        itemPos.z = -0.5f;
        
        StartCoroutine(Timer.create((1f/24f * 12), () => {
            spawnItemAtPositionWithAnimationDelay(persistentData.stashedItems[station], itemPos, (1f/24f * 24));
        }));
    }
    
    public void resetLevel() {
        exploding = true;
        
        StartCoroutine(Timer.create(2.0f, () => {
            Application.LoadLevel(0);
        }));
    }
    
    public void spawnExplosionAtPositionAndVelocity(Vector3 pos, Vector3 vel, Quaternion rot, Vector3 scale) {
        GameObject item = Instantiate(Resources.Load("Prefabs/explosion"), pos, rot) as GameObject;
        ExplosionController explosionController = item.GetComponent<ExplosionController>();
        explosionController.velocity = vel;
        explosionController.transform.localScale = scale;
    }
    
    void Update() {
        if (exploding) {
            for(int i = 0; i < 10; ++i) {
                Vector3 pos = camera.transform.localPosition;
                pos.x += Random.Range(-10, 10);
                pos.y += Random.Range(-6, 6);
                pos.z = -5f;
                
                Vector3 v = new Vector3(Random.value, Random.value);
                v.Normalize();
                v *= Random.Range(0, 5);
                
                Quaternion rot = Quaternion.AngleAxis(Random.Range(0, 360), new Vector3(0f, 0f, 1f));
                
                Vector3 scale = new Vector3(3f, 3f, 3f);
                
                spawnExplosionAtPositionAndVelocity(pos, v, rot, scale);
            }
        }
    }
}
