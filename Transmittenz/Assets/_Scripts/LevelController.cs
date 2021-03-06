﻿using System.Collections;
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
    
    const int kDirectionNone = -1;
    const int kDirectionLeft = 0;
    const int kDirectionRight = 1;
    const int kDirectionUp = 2;
    const int kDirectionDown = 3;
    
    GameObject levelTilemapGameObject;
    GameObject itemsTilemapGameObject;
    GameObject interactablesTilemapGameObject;
    GameObject linkersTilemapGameObject;
    GameObject wiresGameObject;
    public Tilemap levelTilemap;
    public Tilemap itemsTilemap;
    public Tilemap interactablesTilemap;
    public Tilemap linkersTilemap;
    public Tilemap wiresTilemap;
    public Image itemInventoryImage;
    public ItemController.Type itemInInventory = ItemController.Type.None;
    public Tile[] openPanelTiles;
    public GameObject openPanelAnimationObject;
    public GameObject stationAnimationObject;
    public GameObject camera;
    public GameObject spotlightObject;
    
    public GameTile lightOnTile;
    public GameTile lightOffTile;
    public GameTile extendedPlatformTile;
    public GameTile retractedPlatformTile;
    public GameTile invisiblePlatformBarrierTile;
    
    public GameTile[] poweredWires;
    public GameTile[] unpoweredWires;

    Dictionary<Vector3Int, LinkData> links;
    Dictionary<Vector3Int, Transform> stationObjects;
    CameraController cameraController;
    Vector3Int selectedStation;
    Vector3Int startStation;
    HashSet<Vector3Int> stationsThatCantDepositItem;
    bool exploding = false;
    int explosionCount = 0;
    List<Vector3Int> wireTiles;
    List<Vector3Int> wirePowerSources;
    List<AudioSource> oneShotAudioSources;
    int previouslyUsedOneShotAudioSourceIndex;
    float nextExplosionSoundTime = 0f;
    bool resetingLevel = false;
    
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
        levelTilemap = levelTilemapGameObject.GetComponent<Tilemap>();
        
        itemsTilemapGameObject = GameObject.FindGameObjectsWithTag("itemsTilemap")[0];
        itemsTilemap = itemsTilemapGameObject.GetComponent<Tilemap>();
        
        interactablesTilemapGameObject = GameObject.FindGameObjectsWithTag("interactablesTilemap")[0];
        interactablesTilemap = interactablesTilemapGameObject.GetComponent<Tilemap>();
        
        linkersTilemapGameObject = GameObject.FindGameObjectsWithTag("linkersTilemap")[0];
        linkersTilemap = linkersTilemapGameObject.GetComponent<Tilemap>();
        
        wiresGameObject = GameObject.FindGameObjectsWithTag("wiresTilemap")[0];
        wiresTilemap = wiresGameObject.GetComponent<Tilemap>();
        
        cameraController = camera.GetComponent<CameraController>();
        stationObjects = new Dictionary<Vector3Int, Transform>();
        stationsThatCantDepositItem = new HashSet<Vector3Int>();
        wireTiles = new List<Vector3Int>();
        wirePowerSources = new List<Vector3Int>();
        
        oneShotAudioSources = new List<AudioSource>(10);
        
        for(int i = 0; i < oneShotAudioSources.Capacity; ++i) {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            initializeOneShotAudioSource(source);
            oneShotAudioSources.Add(source);
        }
        previouslyUsedOneShotAudioSourceIndex = 0;
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
                pos.z = -1.5f;
                spawnItemAtPositionAndVelocity(ItemController.stringToType(sprite.name), pos, Vector2.zero);
            }
        }
        
        clearItemInInventory();
        buildLinksData();
        setupStations();
        setupExtendingPlatforms();
        setupWires();
        linkersTilemapGameObject.SetActive(false);
        
        if (persistentData.stashedItems.Count == 0) {
            Debug.Log("No stashed items!");
        } else {
            foreach(Vector3Int station in persistentData.stashedItems.Keys) {
                Debug.Log("Stashed item at: " + station + "... item: " + persistentData.stashedItems[station]);
            }
        }
        
        GameObject o = GameObject.Find("cameraZones");
        
        if (o) {
            o.SetActive(false);
        } else {
            Debug.Log("Couldn't set cameraZones to inactive");
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
        
        //Debug.Log("items: " + items);
         
        foreach(GameObject item in items) {
            //Debug.Log("  item: " + item);
            BoxCollider2D collider = item.GetComponent<BoxCollider2D>();
            
            if (!collider) {
                Debug.Log("No box collider on item?!");
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
    
    void Update() {
        if (exploding) {
            for(int i = 0; i < 5; ++i) {
                Vector3 pos = camera.transform.localPosition;
                pos.x += Random.Range(-10, 10);
                pos.y += Random.Range(-6, 6);
                pos.z = -5f - explosionCount * 0.01f;
                
                Vector3 v = new Vector3(Random.value, Random.value);
                v.Normalize();
                v *= Random.Range(0, 5);
                
                Quaternion rot = Quaternion.AngleAxis(Random.Range(0, 360), new Vector3(0f, 0f, 1f));
                
                Vector3 scale = new Vector3(3f, 3f, 3f);
                
                spawnExplosionAtPositionAndVelocity(pos, v, rot, scale);
                ++explosionCount;
            }
        }
    }
    
    public void spawnExplosionAtPositionAndVelocity(Vector3 pos, Vector3 vel, Quaternion rot, Vector3 scale) {
        GameObject item = Instantiate(Resources.Load("Prefabs/explosion"), pos, rot) as GameObject;
        ExplosionController explosionController = item.GetComponent<ExplosionController>();
        explosionController.velocity = vel;
        explosionController.transform.localScale = scale;
        
        if (nextExplosionSoundTime == 0f || Time.time >= nextExplosionSoundTime) {
            playSound("Explosion", 0.3f);
            nextExplosionSoundTime = Time.time + Random.Range(0.08f, 0.12f);
        }
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
    
    public void spawnMouseGrenade(Vector2 pos, Quaternion rot, Vector2 vel) {
        GameObject asdf = Instantiate(Resources.Load("Prefabs/mouseGrenadeGroup"), pos, Quaternion.identity) as GameObject;
        GrenadeController controller = asdf.GetComponent<GrenadeController>();
        controller.setVelocity(vel);
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
        Vector3Int lastInteractablePos = new Vector3Int();
        
        links = new Dictionary<Vector3Int, LinkData>();
        
        Dictionary<string, List<Vector3Int> > sources = new Dictionary<string, List<Vector3Int> >();
        Dictionary<string, List<Vector3Int> > dests = new Dictionary<string, List<Vector3Int> >();
        
        for(int y = 0; y < linkersTilemap.size.y; ++y) {
            for(int x = 0; x < linkersTilemap.size.x; ++x) {
                Vector3Int numberPos = new Vector3Int(x + linkersTilemap.origin.x, y + linkersTilemap.origin.y, 0);
                Sprite sprite = linkersTilemap.GetSprite(numberPos);
                
                if (!sprite) {
                    //Debug.Log("  no tile!");
                    tileLastTime = false;
                    continue;
                }
                
                Vector3Int interactablePos = findUpperLeftOfInteractable(numberPos);
                
                string id = linkerIdForTileName(sprite.name);
                Dictionary<string, List<Vector3Int> > relevant;
                
                //Debug.Log("numberPos: " + numberPos + " id: " + id + "    name: " + sprite.name);
                
                if (linkerTileNameIndicatesDest(sprite.name)) {
                    //Debug.Log("  adding to dests");
                    relevant = dests;
                } else {
                    //Debug.Log("  adding to sources");
                    relevant = sources;
                }
                
                if (!relevant.ContainsKey(id)) {
                    relevant[id] = new List<Vector3Int>();
                }
                
                if (!tileLastTime) {
                    //Debug.Log("  .... adding " + interactablePos);
                    relevant[id].Add(interactablePos);
                    tileLastTime = true;
                    lastInteractablePos = interactablePos;
                } else {
                    ///Debug.Log("  .... adding " + lastInteractablePos);
                    relevant[id].Add(lastInteractablePos);
                }
            }
        }
        
        foreach(string id in sources.Keys) {
            foreach(Vector3Int srcPos in sources[id]) {
                if (!links.ContainsKey(srcPos)) {
                    links[srcPos] = LinkData.createEmpty();
                }
                
                LinkData data = links[srcPos];
                
                foreach(Vector3Int blah in dests[id]) {
                    data.dests.Add(blah);
                }
                
                links[srcPos] = data;
            }
        }
        
        foreach(string id in dests.Keys) {
            foreach(Vector3Int dstPos in dests[id]) {
                if (!links.ContainsKey(dstPos)) {
                    links[dstPos] = LinkData.createEmpty();
                }
                
                LinkData data = links[dstPos];
                
                foreach(Vector3Int blah in sources[id]) {
                    data.sources.Add(blah);
                }
                
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
            //Debug.Log("  toggling: " + p);
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
        
        if (tile.type == GameTile.Type.Platform) {
            togglePlatform(tile, p);
            return;
        }
        
        if (tile.type == GameTile.Type.RotatingWirePlatform) {
            toggleRotatingWire(tile, p);
            return;
        }
        
        if (tile.type == GameTile.Type.WirePowerReceiver) {
            //togglePowerReceiver(tile, p);
            return;
        }
        
        Debug.Log("Error! Tried to toggle invalid interactable!");
    }
    
    bool interactableSourceIsOn(Vector3Int p) {
        GameTile tile = interactableTileAtTilePosition(p);
        
        if (tile.type == GameTile.Type.Light) {
            return tile == lightOnTile;
        } else  if (tile.type == GameTile.Type.WirePowerReceiver) {
            GameTile wireTile = (GameTile)wiresTilemap.GetTile(p);
            
            if (!wireTile) {
                Debug.Log("No corresponding wire tile?!");
                return false;
            }
            
            Debug.Log(" .. .. considering wire receiver: " + p + " ... powered: " + wireTile.isWirePowered);
            return wireTile.isWirePowered;
        }
        
        return true;
    }
    
    void toggleLight(GameTile tile, Vector3Int p) {
        //Debug.Log("Toggling light!");
        
        if (tile == lightOnTile) {
            //Debug.Log("  off!");
            interactablesTilemap.SetTile(p, lightOffTile);
        } else {
            //Debug.Log("  on!");
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
        spawnItemAtPositionWithAnimationDelay(ItemController.Type.GravityMittens, animatonPos + new Vector3(0.32f, 0f, -1.5f),
                                              (1f/24f * 15));
        
    }
    
    int platformLength(Vector3Int pos) {
        int length = 0;
        
        while(interactableTileAtTilePosition(pos) != null) {
            ++length;
            pos.x += 1;
        }
        
        return length;
    }
    
    void setupExtendingPlatforms() {
        for(int x = 0; x < interactablesTilemap.size.x; ++x) {
            for(int y = 0; y < interactablesTilemap.size.y; ++y) {
                Vector3Int tilePos = new Vector3Int(x + interactablesTilemap.origin.x, y + interactablesTilemap.origin.y, 0);
                GameTile tile = interactableTileAtTilePosition(tilePos);
                
                if (!tile || tile != extendedPlatformTile) {
                    continue;
                }
                
                //Debug.Log("tile: " + tile + " ... setting barrier at: " + tilePos);
                levelTilemap.SetTile(tilePos, invisiblePlatformBarrierTile);
            }
        }
    }
    
    void togglePlatform(GameTile tile, Vector3Int pos) {
        Debug.Log("toggling platform! " + pos);
        foreach(Vector3Int p in links[pos].sources) {
            Debug.Log("... source: " + p);
            if (!interactableSourceIsOn(p)) {
                Debug.Log(" .. source is not on: " + p);
                return;
            }
        }
        
        Debug.Log("platform activated!");
        
        int length = platformLength(pos);
        GameTile newTile;
        
        if (tile == extendedPlatformTile) {
            newTile = retractedPlatformTile;
        } else {
            newTile = extendedPlatformTile;
        }
        
        for(int x = 0; x < length; ++x) {
            interactablesTilemap.SetTile(new Vector3Int(pos.x + x, pos.y, 0), newTile);
            
            if (newTile == extendedPlatformTile) {
                levelTilemap.SetTile(new Vector3Int(pos.x + x, pos.y, 0), invisiblePlatformBarrierTile);
            } else {
                levelTilemap.SetTile(new Vector3Int(pos.x + x, pos.y, 0), null);
            }
        }
        
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
        playSound("Teleport_1");
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
        itemPos.z = -1.5f;
        
        StartCoroutine(Timer.create((1f/24f * 12), () => {
            playSound("Teleport_2");
            spawnItemAtPositionWithAnimationDelay(persistentData.stashedItems[station], itemPos, (1f/24f * 24));
        }));
    }
    
    public void resetLevel() {
        if (resetingLevel) {
            return;
        }
        
        resetingLevel = true;
        playSound("Warning Klaxons");
        playSound("meow");
        exploding = true;
        
        StartCoroutine(Timer.create(2.0f, () => {
            /*GameObject[] explosions = GameObject.FindGameObjectsWithTag("explosion");
            Debug.Log("count: " + explosions.Length);
            foreach(GameObject explosion in explosions) {
                ExplosionController e = explosion.GetComponent<ExplosionController>();
                e.freeze();
            }*/
            exploding = false;
            
            Application.LoadLevel(0);
        }));
    }
    
    public void sendToOtherEndOfDoor(Transform t, Vector3Int doorPos) {
        doorPos = findUpperLeftOfInteractable(doorPos);
        
        if (!links.ContainsKey(doorPos)) {
            Debug.Log("Error! Non-linked door!!");
            return;
        }
        
        Vector3 srcDoorWorldPos = interactablesTilemap.CellToWorld(doorPos);
        Vector3 dstDoorWorldPos = interactablesTilemap.CellToWorld(links[doorPos].dests[0]);
        
        t.localPosition = new Vector3(dstDoorWorldPos.x + .32f,
                                      t.localPosition.y - srcDoorWorldPos.y + dstDoorWorldPos.y,
                                      t.localPosition.z);
        
    }
    
    void setupWires() {
        for(int x = 0; x < wiresTilemap.size.x; ++x) {
            for(int y = 0; y < wiresTilemap.size.y; ++y) {
                Vector3Int tilePos = new Vector3Int(x + wiresTilemap.origin.x, y + wiresTilemap.origin.y, 0);
                GameTile tile = (GameTile)wiresTilemap.GetTile(tilePos);
                
                if (!tile) {
                    continue;
                }
                
                if (tile.type == GameTile.Type.WirePowerSource) {
                    //Debug.Log("found power source!" + tilePos);
                    wirePowerSources.Add(tilePos);
                } else {
                    //Debug.Log("found wire!" + tilePos);
                    wireTiles.Add(tilePos);
                }
            }
        }
        
        resetWires();
        powerWires();
    }
    
    int indexOfTile(GameTile[] array, GameTile x) {
        for(int i = 0; i < array.Length; ++i) {
            if (array[i] == x) {
                return i;
            }
        }
        
        return -1;
    }
    
    GameTile powerToUnpowered(GameTile powered) {
        int index = indexOfTile(poweredWires, powered);
        
        if (index < 0) {
            Debug.Log("Could not go power to unpowered!!");
            return null;
        }
        
        return unpoweredWires[index];
    }
    
    GameTile unpoweredToPowered(GameTile unpowered) {
        int index = indexOfTile(unpoweredWires, unpowered);
        
        if (index < 0) {
            Debug.Log("Could not go unpowered to powered!!");
            return null;
        }
        
        return poweredWires[index];
    }
    
    void resetWires() {
        foreach(Vector3Int wirePos in wireTiles) {
            GameTile orgWireTile = (GameTile)wiresTilemap.GetTile(wirePos);
            
            if (orgWireTile.isWirePowered) {
                wiresTilemap.SetTile(wirePos, powerToUnpowered(orgWireTile));
            }
        }
    }
    
    void powerWires() {
        HashSet<Vector3Int> shitToTrigger = new HashSet<Vector3Int>();
        
        foreach(Vector3Int powerSourcePos in wirePowerSources) {
            recursivelyPowerWires(powerSourcePos + new Vector3Int(0, 1, 0), kDirectionUp, shitToTrigger);
        }
        
        foreach(Vector3Int pos in shitToTrigger) {
            triggerInteractableSource(pos, false);
        }
    }
    
    void recursivelyPowerWires(Vector3Int wirePos, int travelDirection, HashSet<Vector3Int> shitToTrigger) {
        //Debug.Log("recursivelyPowerWires: " + wirePos);
        GameTile tile = (GameTile)wiresTilemap.GetTile(wirePos);
        
        if (!tile) {
            return;
        }
        
        if (tile.isWirePowered) {
            //Debug.Log(" ... already powered");
            return;
        }
        
        if (travelDirection == kDirectionLeft && !tile.wireRight) {
            //Debug.Log(" ... can't travel left");
            return;
        }
        
        if (travelDirection == kDirectionRight && !tile.wireLeft) {
            //Debug.Log(" ... can't travel right");
            return;
        }
        
        if (travelDirection == kDirectionUp && !tile.wireDown) {
            //Debug.Log(" ... can't travel up");
            return;
        }
        
        if (travelDirection == kDirectionDown && !tile.wireUp) {
            //Debug.Log(" ... can't travel down");
            return;
        }
        
        wiresTilemap.SetTile(wirePos, unpoweredToPowered(tile));
        
        if (tile.type == GameTile.Type.WirePowerReceiver) {
            //triggerInteractableSource(wirePos, false);
            shitToTrigger.Add(wirePos);
            
        } else {
        
            if (tile.wireLeft) {
                recursivelyPowerWires(wirePos + new Vector3Int(-1, 0, 0), kDirectionLeft, shitToTrigger);
            }
            
            if (tile.wireRight) {
                recursivelyPowerWires(wirePos + new Vector3Int(1, 0, 0), kDirectionRight, shitToTrigger);
            }
            
            if (tile.wireUp) {
                recursivelyPowerWires(wirePos + new Vector3Int(0, 1, 0), kDirectionUp, shitToTrigger);
            }
            
            if (tile.wireDown) {
                recursivelyPowerWires(wirePos + new Vector3Int(0, -1, 0), kDirectionDown, shitToTrigger);
            }
        }
    }
    
    GameTile rotateWireTile(GameTile tile) {
        if (tile.wireLeft && tile.wireRight && !tile.wireUp && !tile.wireDown) {
            return unpoweredWires[9];
        } else if (tile.wireUp && tile.wireDown && !tile.wireLeft && !tile.wireRight) {
            return unpoweredWires[0];
        } else if (tile.wireLeft && tile.wireUp && !tile.wireRight && !tile.wireDown) {
            return unpoweredWires[4];
        } else if (tile.wireUp && tile.wireRight && !tile.wireLeft && !tile.wireDown) {
            return unpoweredWires[3];
        } else if (tile.wireRight && tile.wireDown && !tile.wireLeft && !tile.wireUp) {
            return unpoweredWires[1];
        } else if (tile.wireDown && tile.wireLeft && !tile.wireRight && !tile.wireUp) {
            return unpoweredWires[2];
        } else if (tile.wireLeft && tile.wireUp && tile.wireRight) {
            return unpoweredWires[7];
        } else if (tile.wireUp && tile.wireRight && tile.wireDown) {
            return unpoweredWires[5];
        } else if (tile.wireRight && tile.wireDown && tile.wireLeft) {
            return unpoweredWires[6];
        } else if (tile.wireDown && tile.wireLeft && tile.wireUp) {
            return unpoweredWires[8];
        } else {
            Debug.Log("Invalid wire tile for rotate!");
            return null;
        }
    }
    
    void toggleRotatingWire(GameTile rotatingTile, Vector3Int wirePos) {
        resetWires();
        GameTile orgTile = (GameTile)wiresTilemap.GetTile(wirePos);
        wiresTilemap.SetTile(wirePos, rotateWireTile(orgTile));
        powerWires();
    }
    
    public void explodeBlocksAt(Vector3 pos) {
        Vector3Int tilePos = levelTilemap.WorldToCell(pos);
        
        for(int x = -3; x < 5; ++x) {
            for(int y = -3; y < 5; ++y) {
                maybeDestroyBlock(tilePos + new Vector3Int(x, y, 0));
            }
        }
    }
    
    void maybeDestroyBlock(Vector3Int tilePos) {
        TileBase tile = levelTilemap.GetTile(tilePos);
        
        if (!tile) {
            return;
        }
        
        if (!(tile is GameTile)) {
            return;
        }
        
        GameTile gameTile = (GameTile)tile;
        
        if (!gameTile.isDestructable) {
            return;
        }
        
        levelTilemap.SetTile(tilePos, null);
    }
    
    void initializeOneShotAudioSource(AudioSource source) {
        source.loop = false;
        source.playOnAwake = false;
        source.bypassEffects = true;
        source.bypassListenerEffects = true;
        source.bypassReverbZones = true;
        source.spatialBlend = 0;
        source.priority = 1;
        source.mute = false;
        source.volume = 1;
        source.pitch = 1;
        source.panStereo = 0;
    }
    
    AudioSource nextAvailableSource()
    {
        int index = (previouslyUsedOneShotAudioSourceIndex+1) % oneShotAudioSources.Count;
        AudioSource result = null;
        
        for(int count = 0; count < oneShotAudioSources.Count; ++count) {
            if (!oneShotAudioSources[index].isPlaying) {
                previouslyUsedOneShotAudioSourceIndex = index;
                result = oneShotAudioSources[index];
                break;
            }
            
            index = (index+1) % oneShotAudioSources.Count;
        }
        
        if (!result) {
            /*
            index = previouslyUsedOneShotAudioSourceIndex-1;
            
            if (index < 0) {
                index = oneShotAudioSources.Count-1;
            }
            
            return oneShotAudioSources[index];
            */
            Debug.Log("Adding new one shot audio source");
            result = gameObject.AddComponent<AudioSource>();
            initializeOneShotAudioSource(result);
            oneShotAudioSources.Add(result);
            previouslyUsedOneShotAudioSourceIndex = oneShotAudioSources.Count-1;
            return result;
        } else {
            return result;
        }
    }
    
    public void playSound(string name, float volume=1) {
        AudioSource source = nextAvailableSource();
        AudioClip clip = (AudioClip)Resources.Load("sounds/" + name);
        source.clip = clip;
        source.volume = volume;
        source.Play();
    }
}
