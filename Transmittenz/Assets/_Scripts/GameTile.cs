using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameTile : Tile {
    public enum Type {
        None, Obstacle, Ladder, Console, Station, Light, Panel, Door, Platform, Wire, WirePowerSource, RotatingWirePlatform, WirePowerReceiver
    };
    
    public Type type = Type.None;
    
    public bool wireLeft;
    public bool wireUp;
    public bool wireRight;
    public bool wireDown;
    public bool isWirePowered;
    public bool isDestructable;
    
#if UNITY_EDITOR
        [MenuItem("Assets/Create/Game Tile")]
        public static void CreateAnimatedTile()
        {
            string path = EditorUtility.SaveFilePanelInProject("Save Game Tile", "New Game Tile", "asset", "Save Game Tile", "Assets");
            if (path == "")
                return;

            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameTile>(), path);
        }
#endif
}
