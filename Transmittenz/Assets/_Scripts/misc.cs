using System.Collections;
using System.Collections.Generic;
using UnityEngine;

static public class misc : object {
    public static bool boundsIntersects2D(Bounds a, Bounds b) {
        a.center = new Vector3(a.center.x, a.center.y, 0);
        b.center = new Vector3(b.center.x, b.center.y, 0);
        return a.Intersects(b);
    }
}
