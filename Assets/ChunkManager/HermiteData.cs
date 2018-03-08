using System.Collections.Generic;
using UnityEngine;

public class HermiteData {
    byte[] data;
    int hermitePoint;
    int res1;
    int res2;

    public HermiteData(int resolution) {
        res1 = resolution + 1;
        res2 = res1 * res1;

        data = new byte[res1 * res1 * res1 * 16];

        hermitePoint = res1 * res1 * res1 * 4;
    }

    public float GetDistance(int x, int y, int z) {
        
        return (sbyte)data[(x * res2 + y * res1 + z) * 4];
    }

    public System.Tuple<Vector3, Vector3>[] GetCellEdgeCrossings(int x, int y, int z) {
        List<System.Tuple<Vector3, Vector3>> cellEdgeCrossings = new List<System.Tuple<Vector3, Vector3>>();

        foreach(Vector3Int corner in SE.DC.DCC.vioffsets) {
            var crossings = GetCornerHermiteData(x + corner.x, y + corner.y, z + corner.z);
            for(int i = 0; i < 3; i++) {
                Vector3 dir = SE.DC.DCC.vfdirs[i];
                Vector3 ad = corner + dir;
                if(ad.x > 1 || corner.y > 1 || corner.z > 1) {
                    continue;
                }
                cellEdgeCrossings.Add(crossings[i]);
            }
        }
        return cellEdgeCrossings.ToArray();
    }

    public System.Tuple<Vector3, Vector3>[] GetCornerHermiteData(int x, int y, int z) {
        System.Tuple<Vector3, Vector3>[] EdgeCrossings = new System.Tuple<Vector3, Vector3>[3];
        int begin = hermitePoint + x * res2 + y * res1 + z;
        
        Vector3 ppos = new Vector3(x, y, z);

        for(int i = 0; i < 3; i++) {
            byte dist = data[begin + i*4];
            if(dist == 0) continue;
            dist -= 1;

            Vector3 dir = SE.DC.DCC.vfdirs[i];
            Vector3 pos = ppos + ((float)dist/254f) * dir;
            float nx = (((float)data[begin + i*4 + 1] - 127f / 127f));
            float ny = (((float)data[begin + i*4 + 2] - 127f / 127f));
            float nz = (((float)data[begin + i*4 + 3] - 127f / 127f));
            Vector3 normal = new Vector3(nx, ny, nz);

            var point = new System.Tuple<Vector3, Vector3>(pos, normal);
            EdgeCrossings[i] = point;
        }
        return EdgeCrossings;
    }
}