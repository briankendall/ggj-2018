using System.Collections;
using System.Collections.Generic;
using UnityEngine;

static public class misc : object {
    public static bool boundsIntersects2D(Bounds a, Bounds b) {
        a.center = new Vector3(a.center.x, a.center.y, 0);
        b.center = new Vector3(b.center.x, b.center.y, 0);
        return a.Intersects(b);
    }
    
    public static Vector3Int convert2ito3i(Vector2Int a) {
        return new Vector3Int(a.x, a.y, 0);
    }
    
    public static Vector2Int convert3ito2i(Vector3Int a) {
        return new Vector2Int(a.x, a.y);
    }
}
