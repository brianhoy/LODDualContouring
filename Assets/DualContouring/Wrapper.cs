using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace SE.DC {
public class Wrapper {
    public Transform Parent;
    public GameObject ChunkPrefab;

    public OctreeNode root;

    public Wrapper(Transform parent, GameObject chunkPrefab, float worldSize, int maxDepth, int resolution) {
        Parent = parent;
        ChunkPrefab = chunkPrefab;

        MeshedNodes = new List<Chunks.Node>();
        UnityObjects = new Hashtable();
        WorldSize = worldSize;
        MaxDepth = maxDepth;
        Resolution = resolution;
        ChunkRoot = Chunks.Ops.Create();

		RunTest();
    }

	private void RunTest() {
		int resolution = 32;
		sbyte[] data = GetData(UtilFuncs.Sample, resolution, 0, 0, 0, 16);
        Mesh m = Algorithm2.Run(32, data);//GenerateMesh(32, 0, sampleFuncs[1], false);
        InstantiateMesh(m, Vector3.zero);
	}

    private Mesh GenerateMesh(int resolution, float isovalue, UtilFuncs.Sampler sample, bool flatShading) {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        List<Vector3> vertices = new List<Vector3>();
        OctreeNode octree = new OctreeNode();
        root = octree;
        octree.size = resolution;
        octree.sample = (float x, float y, float z) => { return -sample(x, y, z); };
        octree.isovalue = isovalue;
        Algorithm.ConstructOctreeNodes(octree);
		Algorithm.SimplifyOctree(octree, 0.1f);

        UnityEngine.Debug.Log("Octree Construction time: " + sw.ElapsedMilliseconds);
        UnityEngine.Debug.Log("Num min nodes: " + OctreeNode.numMinNodes);
        sw.Stop();

        return GenerateMeshFromOctree(octree);

    }

    private Mesh GenerateMeshFromOctree(OctreeNode node)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> indices = new List<int>();

        if (node == null) return null;

		//Debug.Log("HGSDFgot here");

        Algorithm.GenerateVertexIndices(node, vertices, normals);
        Algorithm.ContourCellProc(node, indices);

		Debug.Log("numVertices: " + vertices.Count);

        Mesh m = new Mesh();
        m.vertices = vertices.ToArray();
        m.normals = normals.ToArray();
        m.triangles = indices.ToArray();
        return m;
    }

    Chunks.Root ChunkRoot;
    List<Chunks.Node> MeshedNodes; 
    Hashtable UnityObjects;
    public float WorldSize;
    public int MaxDepth;
    public int Resolution;

    public void Update(Vector3 position) {
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        //Chunks.Ops.Adapt(ChunkRoot, position / WorldSize, MaxDepth, 15);
        sw.Stop(); //Debug.Log("BENCH-UPDATE: SE.Octree.Ops.Adapt time: " + (float)sw.ElapsedMilliseconds/1000f + " seconds.");
        sw.Reset(); sw.Start();
        //Mesh();
        sw.Stop(); //Debug.Log("BENCH-UPDATE: Mesh time: " + (float)sw.ElapsedMilliseconds/1000f + " seconds.");
    }

    public void MakeConforming() {
        //Ops.LoopMakeConforming(Root, 2);
    }

    public void DrawGizmos() {
        Chunks.Ops.DrawGizmos(ChunkRoot.RootNode);
        //Debugger.DrawGizmos();
    }

    public void Mesh() {
        List<Chunks.Node> newLeafNodes = new List<Chunks.Node>();
        PopulateLeafNodeList(ChunkRoot.RootNode, newLeafNodes);

        float totalPolyganizeNodeTime = 0f;
        float totalAllBeforeTime = 0f;

        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

        foreach(Chunks.Node n in MeshedNodes.Except(newLeafNodes)) {
            Object.Destroy((GameObject)UnityObjects[n.ID]);
            UnityObjects.Remove(n.ID);
        }
        foreach(Chunks.Node n in newLeafNodes.Except(MeshedNodes)) {
            MeshNode(n, ref totalPolyganizeNodeTime, ref totalAllBeforeTime, sw);
        }

        Debug.Log("BENCH-MESH: AllBefore time: " + totalAllBeforeTime + " seconds.");
        Debug.Log("BENCH-MESH: PolyganizeNode time: " + totalPolyganizeNodeTime + " seconds.");

        MeshedNodes = newLeafNodes;
    }

	public sbyte[] GetData(UtilFuncs.Sampler sample, int resolution, float xOff, float yOff, float zOff, float scale) {
		int res1 = resolution + 1;

		sbyte[] data = new sbyte[res1 * res1 * res1 * 4];
		

		float factor = scale / res1;
		int index = 0;
		float f = 0.01f;

		for(int x = 0; x < res1; x++) {
			for(int y = 0; y < res1; y++) {
				for(int z = 0; z < res1; z++) {
					index++;
					float nx = (float)x * factor;
					float ny = (float)y * factor;
					float nz = (float)z * factor;
					float density = Mathf.Clamp(sample(nx, ny, nz) * 128, -127, 128);
					float dx = sample(nx+f, ny, nz) - sample(nx-f, ny, nz);
					float dy = sample(nx, ny+f, nz) - sample(nx, ny-f, nz);
					float dz = sample(nx, ny, nz+f) - sample(nx, ny, nz-f);

					float total = (dx*dx) + (dy*dy) + (dz*dz); total = Mathf.Sqrt(total);

					dx /= total; dx *= 127;
					dy /= total; dy *= 127;
					dz /= total; dz *= 127;

					data[index] = (sbyte)density;
					data[index + 1] = (sbyte)dx;
					data[index + 2] = (sbyte)dy;
					data[index + 3] = (sbyte)dz;
				}
			}
		}

		return data;
	}

	public static Vector3 CalculateSurfaceNormal(Vector3 p, UtilFuncs.Sampler sample)
	{
		const float H = 0.001f;
		float dx = sample(p.x + H, p.y, p.z) - sample(p.x - H, p.y, p.z);
		float dy = sample(p.x, p.y + H, p.z) - sample(p.x, p.y - H, p.z);
		float dz = sample(p.x, p.y, p.z + H) - sample(p.x, p.y, p.z - H);

		return new Vector3(dx, dy, dz).normalized;
	}

    public void PopulateLeafNodeList(Chunks.Node node, List<Chunks.Node> leafNodes) {
        if(node.IsLeaf) {
            leafNodes.Add(node);
        }
        else {
            for(int i = 0; i < node.Children.Length; i++) {
                PopulateLeafNodeList(node.Children[i], leafNodes);
            }
        }
    }

    public void MeshNode(Chunks.Node node, ref float totalPolyganizeNodeTime, ref float totalAllBeforeTime, System.Diagnostics.Stopwatch sw) {
        sw.Start();
        GameObject clone = Object.Instantiate(ChunkPrefab, new Vector3(0, 0, 0), Quaternion.identity);
        Color c = UtilFuncs.SinColor(node.Depth * 3f);
        clone.GetComponent<MeshRenderer>().material.color = new Color(c.r, c.g, c.b, 0.9f);
        clone.transform.localScale = Vector3.one * (WorldSize / Resolution) * Mathf.Pow(2f, -node.Depth + 1);
        clone.name = "Node " + node.ID + ", Depth " + node.Depth;
        

        MeshFilter mf = clone.GetComponent<MeshFilter>();
        sw.Stop();
        totalAllBeforeTime += (float)sw.ElapsedMilliseconds/1000f;
        sw.Reset(); sw.Start();
        mf.mesh = Chunks.Ops.PolyganizeNode(ChunkRoot, node, WorldSize);
        sw.Stop();
        totalPolyganizeNodeTime += (float)sw.ElapsedMilliseconds/1000f;
        clone.GetComponent<Transform>().SetParent(Parent);
        clone.GetComponent<Transform>().SetPositionAndRotation(node.Position * WorldSize, Quaternion.identity);

        UnityObjects[node.ID] = clone;
    }


    private void InstantiateMesh(UnityEngine.Mesh m, Vector3 offset) {
		GameObject isosurfaceMesh = Object.Instantiate(ChunkPrefab, offset, Quaternion.identity);
		Debug.Log("Instantiate called");
        isosurfaceMesh.GetComponent<Transform>().SetParent(Parent);
		//Meshes.Add(isosurfaceMesh);

		Material mat = isosurfaceMesh.GetComponent<Renderer>().materials[0];
		MeshFilter mf = isosurfaceMesh.GetComponent<MeshFilter>();
		MeshCollider mc = isosurfaceMesh.GetComponent<MeshCollider>();

		mf.mesh = m;
		mc.sharedMesh = mf.mesh;
		mf.mesh.normals = m.normals;
		//mf.mesh.RecalculateNormals();
		mf.mesh.RecalculateBounds();
    }

    public static SE.OpenSimplexNoise s = new SE.OpenSimplexNoise();
    public static UtilFuncs.Sampler[] sampleFuncs = {
        (float x, float y, float z) => {
            float r = 0.54f;
            float result = 0.5f + y;
            result += (float)s.Evaluate((double)x * r, (double)y * r, (double)z * r) * 15;
            return result;
        },
        (float x, float y, float z) => {
            float r = 0.54f;
            float result = -0.5f + y;
            result += (float)s.Evaluate((double)x * r, (double)y * r, (double)z * r) * 75f;
            return result;
        }

    };

}
}