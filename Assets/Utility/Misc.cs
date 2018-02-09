using UnityEngine;
using System.Collections.Generic;
using Util;

public static class UtilFuncs {
    public static SE.OpenSimplexNoise s = new SE.OpenSimplexNoise(7);

    public delegate float Sampler(float x, float y, float z);

    public static float Sample(float x, float y, float z) {
        float r = 0.034f;
		float result = 0f;
        result += -1.5f + y; 
		//result += Sphere(x, y, z);
        result += (float)s.Evaluate((double)x * r, (double)y * r, (double)z * r) * 15;
        return result;
    }

	public static float Sphere(float x, float y, float z) {
		float r = 0.5f;
		x-= 0.5f; y -= 0.5f; z -= 0.5f;
		return x * x + y * y + z * z - r * r;
	}

    public static Vector3 Lerp(float isolevel, Point point1, Point point2) {
        if (Mathf.Abs(isolevel-point1.density) < 0.00001)
            return(point1.position);
        if (Mathf.Abs(isolevel-point2.density) < 0.00001)
            return(point2.position);
        if (Mathf.Abs(point1.density-point2.density) < 0.00001)
            return(point2.position);
        float mu = (isolevel - point1.density) / (point2.density - point1.density); 
        return point1.position + mu * (point2.position - point1.position); 
    }
    
    public static Color SinColor(float value) {
        float frequency = 0.3f;
        float red   = Mathf.Sin(frequency*value + 0) * 0.5f + 0.5f;
        float green = Mathf.Sin(frequency*value + 2) * 0.5f + 0.5f;
        float blue  = Mathf.Sin(frequency*value + 4) * 0.5f + 0.5f;
        return new Color(red, green, blue);
    }
}

namespace Util {
    public struct Vector3i {
        public int x;
        public int y;
        public int z;

        public Vector3i(int x, int y, int z) { 
            this.x = x; this.y = y; this.z = z; 
        }
        public int getDimensionSigned(int dim) {
            switch(dim) {
                case 0: return -x;
                case 1: return x;
                case 2: return -y;
                case 3: return y;
                case 4: return -z;
                case 5: return z;
            }
            return -1;
        }
        public int getDimension(int dim) {
            switch(dim) {
                case 0: return x;
                case 1: return y;
                case 2: return z;
            }
            return -1;
        }
        public void setDimension(int dim, int val) {
            switch(dim) {
                case 0: x = val; break;
                case 1: y = val; break;
                case 2: z = val; break;
            }
        }
    }
    public struct GridCell {
        public Point[] points;
        public GridCell Clone() {
            GridCell c = new GridCell();
            c.points = new Point[points.Length];
            for(int i = 0; i < points.Length; i++) {
                c.points[i] = points[i];
            }
            return c;
        }
    }

    public struct Point {
        public Vector3 position;
        public float density;    
    }

    public class ExtractionResult {
        public Mesh mesh;
        public long time;
        public Vector3 offset;
    }
}