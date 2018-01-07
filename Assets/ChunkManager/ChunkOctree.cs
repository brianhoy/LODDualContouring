
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace SE.DC.Chunks {
public class ChunkNode {
    public ChunkNode[] Children;
    public ChunkNode Parent;
    public Vector3 Position;
    public uint ID;
    public float Size;
    public bool IsLeaf;
    public int Depth;
    public Vector4 Key;
    public OctreeNode octree;
	public OctreeDrawInfo[,,] drawInfos;
};
public class Root {
    public ChunkNode RootNode;
    public Dictionary<uint, ChunkNode> IDNodes; // index: ID | value: Node
    public Dictionary<Vector4, ChunkNode> Nodes; //index: Vector4 (xyz=position, w=Depth) | value: Node
}

public static class ChunkOctree {
    public static uint lastID = 1;
    public static int RESOLUTION = 16; 
    public static int SAMPLE_FUNC = 0;

    // returns new octree from [-1, -1, -1] to [1, 1, 1]
    public static Root Create(int resolution) {
		RESOLUTION = resolution;
        Root root = new Root();
        root.Nodes = new Dictionary<Vector4, ChunkNode>();
        root.IDNodes = new Dictionary<uint, ChunkNode>();

        ChunkNode rootNode = new ChunkNode();
        rootNode.Position = new Vector3(-1, -1, -1);
        rootNode.Size = 2;
        rootNode.IsLeaf = true;
        rootNode.ID = 0;
        rootNode.Depth = 0;
        rootNode.Key = new Vector4(rootNode.Position.x, rootNode.Position.y, rootNode.Position.z, rootNode.Depth);
        root.IDNodes.Add(rootNode.ID, rootNode);
        SplitNode(root, rootNode);
        root.RootNode = rootNode;
        return root;
    }

    public static void SplitNode(Root root, ChunkNode node) {
        Debug.Assert(node.IsLeaf);

        node.IsLeaf = false;
        node.Children = new ChunkNode[8];

        for(int i = 0; i < 8; i++) {
            ChunkNode n = new ChunkNode();
            node.Children[i] = n;
            n.Size = node.Size / 2f;
            n.ID = lastID;
            n.Depth = node.Depth + 1;
            lastID++;
            n.Parent = node;
            n.Position = node.Position + (DCC.vfoffsets[i] * n.Size);
            n.IsLeaf = true;
            n.Key = new Vector4(n.Position.x, n.Position.y, n.Position.z, n.Depth);
            root.Nodes.Add(n.Key, n);
            root.IDNodes.Add(n.ID, n);
        }
    }

    public static void CoarsenNode(Root root, ChunkNode node) {
        UnityEngine.Debug.Assert(node.ID != 0);
        
        for(int i = 0; i < 8; i++) {
            if(!node.Children[i].IsLeaf) {
                Debug.LogWarning("Coarsening node whose children isn't a leaf. ID: " + node.ID);
                CoarsenNode(root, node.Children[i]);
            }
        }
        for(int i = 0; i < 8; i++) {
            Debug.Assert(root.Nodes.Remove(node.Children[i].Key));
            Debug.Assert(root.IDNodes.Remove(node.Children[i].ID));
        }
        node.Children = null;
        node.IsLeaf = true;
    }

    // make sure position is between [-1, -1, -1] and [1, 1, 1]
    public static void Adapt(Root root, Vector3 position, int maxDepth, int maxIterations) {
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        LoopRefine(root, position, maxDepth, maxIterations);
        sw.Stop(); Debug.Log("BENCH-ADAPT: LoopRefine time: " + (float)sw.ElapsedMilliseconds/1000f + " seconds.");
        sw.Reset(); sw.Start();
        LoopCoarsen(root, position, maxIterations);
        sw.Stop(); Debug.Log("BENCH-ADAPT: LoopCoarsen time: " + (float)sw.ElapsedMilliseconds/1000f + " seconds.");
        sw.Reset(); sw.Start();
        LoopMakeConforming(root, maxIterations);
        sw.Stop(); Debug.Log("BENCH-ADAPT: LoopMakeConforming time: " + (float)sw.ElapsedMilliseconds/1000f + " seconds.");
    }

    public static void LoopRefine(Root root, Vector3 position, int maxDepth, int maxIterations) {
        for(int i = 0; i < maxIterations; i++) {
           RecursiveRefine(root, root.RootNode, position, maxDepth);
        }
    }

    public static void LoopCoarsen(Root root, Vector3 position, int maxIterations) {
        for(int i = 0; i < maxIterations; i++) {
           if(RecursiveCoarsen(root, root.RootNode, position)) {
               break;
           }
           if(i == maxIterations - 1) {
                Debug.LogWarning("Maximum LoopCoarsen iterations reached at " + maxIterations);
           }
        }
    }

    public static void LoopMakeConforming(Root root, int maxIterations) {
        Debug.Log("LoopMakeConforming called");


        for(int i = 0; i < maxIterations; i++) {
            Hashtable splitList = new Hashtable();
            bool result = RecursiveMakeConforming(root, root.RootNode, splitList);
            foreach(ChunkNode n in splitList.Values) {
                if(n.IsLeaf) {
                    SplitNode(root, n);
                }
            }

            if(result) {
                break;
            }

            if(i == maxIterations - 1) {
                Debug.LogWarning("Maximum LoopMakeConforming iterations reached at " + maxIterations);
            }
        }
    }

    public static bool RecursiveMakeConforming(Root root, ChunkNode node, Hashtable splitList) {
        bool returning = true;
        if(node.IsLeaf) {
            ChunkNode[] neighbors = FindNeighbors(root, node);
            //Debug.Log("neighbors length: " + neighbors.Count);
            foreach(ChunkNode neighbor in neighbors) {
                if(neighbor == null) continue;
                if(node.Depth - 1 > neighbor.Depth && neighbor.IsLeaf) {
                    //Debug.Assert(neighbor.IsLeaf);
                    //Debug.LogWarning("Splitting node " + neighbor.ID + " to make octree conforming");
                    if(!splitList.ContainsKey(neighbor.Key)) {
                        splitList.Add(neighbor.Key, neighbor);
                    }
                    //SplitNode(root, neighbor);
                    returning = false;
                }
            }
        }
        else {
            for(int i = 0; i < 8; i++) {
                if(!RecursiveMakeConforming(root, node.Children[i], splitList)) {
                    returning = false;
                }
            }
        }
        return returning;
    }

    public static readonly Vector3[] Directions = { 
        new Vector3(-1, 0, 0), new Vector3(1, 0, 0), 
        new Vector3(0, -1, 0), new Vector3(0, 1, 0), 
        new Vector3(0, 0, -1), new Vector3(0, 0, 1) };
	public static readonly Vector3[] SeamDirections = {
        new Vector3(1, 0, 0), new Vector3(0, 1, 0), 
        new Vector3(1, 1, 0), new Vector3(0, 0, 1), 
        new Vector3(1, 0, 1), new Vector3(0, 1, 1), 
		new Vector3(1, 1, 1)};
    public static ChunkNode[] FindNeighbors(Root root, ChunkNode node) {
        ChunkNode[] neighbors = new ChunkNode[6];
        for(int i = 0; i < 6; i++) {
            Vector3 dir = ChunkOctree.Directions[i];
            neighbors[i] = RecursiveGetNeighbor(root, node, dir);
        }
        return neighbors;
    }
	public static ChunkNode[] FindSeamNodes(Root root, ChunkNode node) {
		ChunkNode[] seamNodes = new ChunkNode[7];
        for(int i = 0; i < 7; i++) {
            Vector3 dir = ChunkOctree.SeamDirections[i];
            seamNodes[i] = RecursiveGetNeighbor(root, node, dir);
        }
        return seamNodes;
	}

    public static ChunkNode RecursiveGetNeighbor(Root root, ChunkNode node, Vector3 direction) {
        if(node.Depth == 0) {
            return null;
        }
        Vector4 code = GetCollapsedCode(node, direction);
        if(root.Nodes.ContainsKey(code)) {
            return root.Nodes[code];
        }
        else {
            return RecursiveGetNeighbor(root, node.Parent, direction);
        }
    }

    public static Vector4 GetCollapsedCode(ChunkNode node, Vector3 direction) {
        Vector3 scaled = (direction*2)/Mathf.Pow(2, node.Depth);
        Vector3 newPos = node.Position + scaled;

        return new Vector4(newPos.x, newPos.y, newPos.z, node.Depth);
    }


    public static void RecursiveRefine(Root root, ChunkNode node, Vector3 position, int maxDepth) {
        //Debug.Log("Recursive refine at level " + node.Depth + ". PointInNode: " + PointInNode(node, position));
        if(node.IsLeaf) {
            if(node.Depth < maxDepth && PointInNode(node, position)) {
                SplitNode(root, node);
            }
        }
        else {
            for(int i = 0; i < 8; i++) {
                RecursiveRefine(root, node.Children[i], position, maxDepth);
            }
        }
    }

    public static bool RecursiveCoarsen(Root root, ChunkNode node, Vector3 position) {
        bool returning = true;
        if(node.IsLeaf) {
            if(!PointInNode(node.Parent, position) && node.Depth != 0) {
                CoarsenNode(root, node.Parent);
                returning = false;
            }
        }
        else {
            for(int i = 0; i < 8 && !node.IsLeaf; i++) {
                if(!RecursiveCoarsen(root, node.Children[i], position)) {
                    returning = false;
                }
            }  
        }
        return returning;
    }

    public static bool PointInNode(ChunkNode node, Vector3 point) {
        return (point.x >= 
                node.Position.x && 
                point.y >= 
                node.Position.y && 
                point.z >= 
                node.Position.z &&
                
                point.x <= (node.Position.x + node.Size) && 
                point.y <= (node.Position.y + node.Size) && 
                point.z <= (node.Position.z + node.Size));
    }

    public static void DrawGizmos(ChunkNode octree, float WorldSize) {
        DrawGizmosRecursive(octree, WorldSize);
    }

    public static void DrawGizmosRecursive(ChunkNode node, float WorldSize) {
        if(!node.IsLeaf) {
            for(int i = 0; i < 8; i++) {
                DrawGizmosRecursive(node.Children[i], WorldSize);
            }
        }
        DrawNode(node, WorldSize);
    }

    public static void DrawNode(ChunkNode node, float WorldSize) {
        Gizmos.color = UtilFuncs.SinColor( ((float)(node.Depth) * 15f));
        UnityEngine.Gizmos.DrawWireCube( (node.Position + new Vector3(node.Size / 2f, node.Size / 2f, node.Size / 2f)) * WorldSize, node.Size * Vector3.one * WorldSize);
    }

    public static float minX = float.MaxValue;
    public static float maxX = float.MinValue;

    public static Mesh PolyganizeNode(Root root, ChunkNode node, float WorldSize) {
        float mul =  Mathf.Pow(2, -node.Depth);
        float nodeSize = mul * WorldSize;
        
		float size = WorldSize / (Mathf.Pow(2, node.Depth)) * 4;

		//Debug.Log("nodeSize: " + size);

		float factor = Mathf.Pow(2, -node.Depth + 1) * (1f/(float)RESOLUTION);

		UtilFuncs.Sampler samp = (float x, float y, float z) => UtilFuncs.Sample((x * factor + node.Position.x)  * WorldSize, (y * factor + node.Position.y) * WorldSize, (z * factor + node.Position.z) * WorldSize);

		Mesh m = Algorithm2.Run(RESOLUTION, samp, node, 1f);
		//Debug.Log("Got here");

        /*ExtractionInput input = new ExtractionInput();
        input.Isovalue = 0f;
        input.Resolution = new Util.Vector3i(16, 16, 16);
        float size = WorldSize / (Mathf.Pow(2, node.Depth));
        input.Size = new Vector3(node.Size/16f, node.Size/16f, node.Size/16f);
        input.LODSides = new byte();*/
                


        //Node[] seamNodes = FindSeamNodes(root, node);

        int currentSide = 1;

        return m;
    }

	public static Mesh PolyganizeNodeSeam(Root root, ChunkNode node) {
		Debug.Assert(node != null && node.IsLeaf);

		ChunkNode[] seamNodes = FindSeamNodes(root, node);
		Mesh m = Algorithm2.GenSeamMesh(RESOLUTION, node, seamNodes);

		return m;
	}

    public static ChunkNode GetSeamNodes(Root root, ChunkNode node) {


        return null;
    }
}
}