using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameTile : Tile {
    public enum Type {
        None, Obstacle, Ladder, Console, Station
    };
    
    public Type type = Type.None;
    public int something;
    
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
